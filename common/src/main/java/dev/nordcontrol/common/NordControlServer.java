package dev.nordcontrol.common;

import com.google.gson.Gson;
import com.google.gson.JsonArray;
import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;
import com.sun.net.httpserver.HttpExchange;
import com.sun.net.httpserver.HttpServer;

import java.io.IOException;
import java.net.InetSocketAddress;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.time.Instant;
import java.util.List;
import java.util.Locale;
import java.util.Objects;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicReference;

public final class NordControlServer implements AutoCloseable {
    private static final Gson GSON = new Gson();
    private static final int MAX_REQUEST_BYTES = 1024 * 1024;
    private final ControlPlatform platform;
    private final AtomicReference<ControlConfig> config;
    private final AuditLog audit;
    private final HttpServer server;

    public NordControlServer(ControlPlatform platform, ControlConfig initialConfig, java.nio.file.Path dataDirectory) throws IOException {
        this.platform = Objects.requireNonNull(platform);
        this.config = new AtomicReference<>(Objects.requireNonNull(initialConfig));
        this.audit = new AuditLog(dataDirectory.resolve("audit.jsonl"));
        this.server = HttpServer.create(new InetSocketAddress(initialConfig.bindAddress(), initialConfig.port()), 0);
        server.createContext("/health", this::handleHealth);
        server.createContext("/mcp", this::handleMcp);
        server.setExecutor(Executors.newVirtualThreadPerTaskExecutor());
    }

    public void start() {
        server.start();
        platform.logInfo("NordControl MCP listening on " + config.get().bindAddress() + ":" + config.get().port());
    }

    public ControlConfig config() { return config.get(); }

    public void applyReload(ControlConfig replacement) throws IOException {
        ControlConfig current = config.get();
        if (!current.bindAddress().equals(replacement.bindAddress()) || current.port() != replacement.port()) {
            throw new IOException("bindAddress and port cannot be changed by live reload; restore them or restart the plugin/server");
        }
        config.set(replacement);
    }

    @Override public void close() { server.stop(1); }

    private void handleHealth(HttpExchange exchange) throws IOException {
        if (!authorize(exchange)) return;
        if (!"GET".equalsIgnoreCase(exchange.getRequestMethod())) { sendEmpty(exchange, 405); return; }
        JsonObject body = new JsonObject();
        body.addProperty("status", "ok");
        body.addProperty("platform", platform.platformName());
        body.addProperty("time", Instant.now().toString());
        sendJson(exchange, 200, body);
    }

    private void handleMcp(HttpExchange exchange) throws IOException {
        if (!authorize(exchange)) return;
        if (!"POST".equalsIgnoreCase(exchange.getRequestMethod())) { sendEmpty(exchange, 405); return; }
        byte[] body = exchange.getRequestBody().readNBytes(MAX_REQUEST_BYTES + 1);
        if (body.length > MAX_REQUEST_BYTES) { sendEmpty(exchange, 413); return; }
        JsonObject request;
        try {
            request = JsonParser.parseString(new String(body, StandardCharsets.UTF_8)).getAsJsonObject();
        } catch (RuntimeException exception) {
            sendJson(exchange, 400, rpcError(null, -32700, "Parse error"));
            return;
        }
        JsonElement id = request.get("id");
        String method = string(request, "method", "");
        if (id == null && method.startsWith("notifications/")) { sendEmpty(exchange, 202); return; }
        try {
            switch (method) {
                case "initialize" -> sendJson(exchange, 200, initialize(id));
                case "ping" -> sendJson(exchange, 200, rpcResult(id, new JsonObject()));
                case "tools/list" -> sendJson(exchange, 200, rpcResult(id, toolsList()));
                case "tools/call" -> handleToolCall(exchange, id, request.getAsJsonObject("params"));
                default -> sendJson(exchange, 200, rpcError(id, -32601, "Method not found"));
            }
        } catch (Exception exception) {
            platform.logWarning("MCP request failed", exception);
            sendJson(exchange, 200, rpcError(id, -32603, "Internal error"));
        }
    }

    private void handleToolCall(HttpExchange exchange, JsonElement id, JsonObject params) throws Exception {
        if (params == null) { sendJson(exchange, 200, rpcError(id, -32602, "Missing params")); return; }
        String name = string(params, "name", "");
        JsonObject arguments = params.has("arguments") && params.get("arguments").isJsonObject()
                ? params.getAsJsonObject("arguments") : new JsonObject();
        String remote = remote(exchange);
        CompletableFuture<JsonObject> future;
        String detail = name;
        switch (name) {
            case "get_server_status" -> future = platform.serverStatus();
            case "get_performance" -> future = platform.performance();
            case "list_plugins" -> future = platform.plugins();
            case "list_players" -> future = platform.players();
            case "list_available_commands" -> future = platform.commands();
            case "get_recent_logs" -> future = CompletableFuture.supplyAsync(() -> readLogs(arguments, false));
            case "get_recent_errors" -> future = CompletableFuture.supplyAsync(() -> readLogs(arguments, true));
            case "run_console_command" -> {
                String command = string(arguments, "command", "").trim();
                if (command.startsWith("/")) command = command.substring(1);
                if (command.isBlank() || command.length() > 2048 || command.indexOf('\n') >= 0 || command.indexOf('\r') >= 0 || command.indexOf('\0') >= 0) {
                    sendJson(exchange, 200, toolResult(id, errorObject("Command must be 1-2048 characters and contain no control line breaks"), true));
                    audit.record(remote, name, "invalid command", false);
                    return;
                }
                detail = command;
                future = platform.executeCommand(command);
            }
            default -> { sendJson(exchange, 200, rpcError(id, -32602, "Unknown tool")); return; }
        }
        try {
            JsonObject result = future.get(config.get().commandTimeoutSeconds(), TimeUnit.SECONDS);
            audit.record(remote, name, detail, true);
            sendJson(exchange, 200, toolResult(id, result, false));
        } catch (Exception exception) {
            audit.record(remote, name, detail, false);
            sendJson(exchange, 200, toolResult(id, errorObject("Tool failed or timed out: " + safeMessage(exception)), true));
        }
    }

    private JsonObject readLogs(JsonObject arguments, boolean errorsOnly) {
        try {
            int lines = arguments.has("lines") ? arguments.get("lines").getAsInt() : 100;
            return LogReader.read(platform.serverDirectory(), lines, config.get().maxLogLines(), errorsOnly);
        } catch (Exception exception) {
            throw new IllegalStateException(exception);
        }
    }

    private boolean authorize(HttpExchange exchange) throws IOException {
        ControlConfig active = config.get();
        String remote = remote(exchange);
        if (!active.allowedAddresses().contains(remote)) {
            audit.record(remote, "authorize", "address rejected", false);
            sendEmpty(exchange, 403);
            return false;
        }
        String origin = exchange.getRequestHeaders().getFirst("Origin");
        if (origin != null && !allowedOrigin(origin)) {
            audit.record(remote, "authorize", "origin rejected", false);
            sendEmpty(exchange, 403);
            return false;
        }
        String authorization = exchange.getRequestHeaders().getFirst("Authorization");
        String supplied = authorization != null && authorization.regionMatches(true, 0, "Bearer ", 0, 7)
                ? authorization.substring(7) : "";
        if (!MessageDigest.isEqual(active.token().getBytes(StandardCharsets.UTF_8), supplied.getBytes(StandardCharsets.UTF_8))) {
            audit.record(remote, "authorize", "token rejected", false);
            exchange.getResponseHeaders().set("WWW-Authenticate", "Bearer");
            sendEmpty(exchange, 401);
            return false;
        }
        return true;
    }

    private static boolean allowedOrigin(String origin) {
        String lower = origin.toLowerCase(Locale.ROOT);
        return lower.matches("https?://(localhost|127\\.0\\.0\\.1|\\[::1])(:\\d+)?");
    }

    private static String remote(HttpExchange exchange) {
        return exchange.getRemoteAddress().getAddress().getHostAddress().split("%")[0];
    }

    private static JsonObject initialize(JsonElement id) {
        JsonObject result = new JsonObject();
        result.addProperty("protocolVersion", "2025-06-18");
        JsonObject capabilities = new JsonObject();
        JsonObject tools = new JsonObject();
        tools.addProperty("listChanged", false);
        capabilities.add("tools", tools);
        result.add("capabilities", capabilities);
        JsonObject info = new JsonObject();
        info.addProperty("name", "NordControl");
        info.addProperty("version", "0.2.0");
        result.add("serverInfo", info);
        result.addProperty("instructions", "Controls Minecraft only. Console commands run with full Minecraft console privileges and are written to the audit log.");
        return rpcResult(id, result);
    }

    private static JsonObject toolsList() {
        JsonArray tools = new JsonArray();
        tools.add(tool("get_server_status", "Get Minecraft server/proxy status", objectSchema()));
        tools.add(tool("get_performance", "Get JVM and platform performance metrics", objectSchema()));
        tools.add(tool("list_plugins", "List loaded Minecraft plugins", objectSchema()));
        tools.add(tool("list_players", "List connected players", objectSchema()));
        tools.add(tool("list_available_commands", "List commands visible to the console", objectSchema()));
        tools.add(tool("get_recent_logs", "Read the tail of logs/latest.log with IP addresses redacted", linesSchema()));
        tools.add(tool("get_recent_errors", "Read recent warning/error lines with IP addresses redacted", linesSchema()));
        JsonObject commandSchema = objectSchema();
        commandSchema.getAsJsonObject("properties").add("command", property("string", "Minecraft console command without the leading slash"));
        JsonArray required = new JsonArray(); required.add("command"); commandSchema.add("required", required);
        tools.add(tool("run_console_command", "Run any Minecraft console command. This cannot execute operating-system commands.", commandSchema));
        JsonObject result = new JsonObject(); result.add("tools", tools); return result;
    }

    private static JsonObject linesSchema() {
        JsonObject schema = objectSchema();
        JsonObject lines = property("integer", "Number of matching lines to return");
        lines.addProperty("minimum", 1); lines.addProperty("maximum", 5000); lines.addProperty("default", 100);
        schema.getAsJsonObject("properties").add("lines", lines);
        return schema;
    }

    private static JsonObject objectSchema() {
        JsonObject schema = new JsonObject(); schema.addProperty("type", "object");
        schema.add("properties", new JsonObject()); schema.addProperty("additionalProperties", false); return schema;
    }

    private static JsonObject property(String type, String description) {
        JsonObject property = new JsonObject(); property.addProperty("type", type); property.addProperty("description", description); return property;
    }

    private static JsonObject tool(String name, String description, JsonObject schema) {
        JsonObject tool = new JsonObject(); tool.addProperty("name", name); tool.addProperty("description", description); tool.add("inputSchema", schema); return tool;
    }

    private static JsonObject toolResult(JsonElement id, JsonObject data, boolean error) {
        JsonArray content = new JsonArray(); JsonObject text = new JsonObject(); text.addProperty("type", "text"); text.addProperty("text", GSON.toJson(data)); content.add(text);
        JsonObject result = new JsonObject(); result.add("content", content); result.add("structuredContent", data); result.addProperty("isError", error);
        return rpcResult(id, result);
    }

    private static JsonObject rpcResult(JsonElement id, JsonObject result) {
        JsonObject response = base(id); response.add("result", result); return response;
    }

    private static JsonObject rpcError(JsonElement id, int code, String message) {
        JsonObject error = new JsonObject(); error.addProperty("code", code); error.addProperty("message", message);
        JsonObject response = base(id); response.add("error", error); return response;
    }

    private static JsonObject base(JsonElement id) {
        JsonObject response = new JsonObject(); response.addProperty("jsonrpc", "2.0");
        response.add("id", id == null ? com.google.gson.JsonNull.INSTANCE : id.deepCopy()); return response;
    }

    private static JsonObject errorObject(String message) { JsonObject object = new JsonObject(); object.addProperty("error", message); return object; }
    private static String safeMessage(Throwable error) { Throwable cause = error.getCause() == null ? error : error.getCause(); return cause.getMessage() == null ? cause.getClass().getSimpleName() : cause.getMessage(); }
    private static String string(JsonObject object, String key, String fallback) { return object != null && object.has(key) && object.get(key).isJsonPrimitive() ? object.get(key).getAsString() : fallback; }

    private static void sendJson(HttpExchange exchange, int status, JsonObject body) throws IOException {
        byte[] bytes = GSON.toJson(body).getBytes(StandardCharsets.UTF_8);
        exchange.getResponseHeaders().set("Content-Type", "application/json; charset=utf-8");
        exchange.getResponseHeaders().set("Cache-Control", "no-store");
        exchange.sendResponseHeaders(status, bytes.length);
        try (var output = exchange.getResponseBody()) { output.write(bytes); }
    }

    private static void sendEmpty(HttpExchange exchange, int status) throws IOException {
        exchange.sendResponseHeaders(status, -1); exchange.close();
    }
}
