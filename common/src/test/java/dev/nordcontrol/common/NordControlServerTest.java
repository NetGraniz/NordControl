package dev.nordcontrol.common;

import com.google.gson.JsonObject;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;

import java.net.ServerSocket;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.file.Path;
import java.util.List;
import java.util.concurrent.CompletableFuture;

import static org.junit.jupiter.api.Assertions.*;

class NordControlServerTest {
    @TempDir Path directory;
    private NordControlServer server;

    @AfterEach void stop() { if (server != null) server.close(); }

    @Test void authenticatesAndServesMcpTools() throws Exception {
        int port;
        try (ServerSocket socket = new ServerSocket(0)) { port = socket.getLocalPort(); }
        var config = new ControlConfig(true, "127.0.0.1", port, "01234567890123456789012345678901", List.of("127.0.0.1"), 5, 100);
        server = new NordControlServer(new FakePlatform(), config, directory.resolve("data"));
        server.start();
        HttpClient client = HttpClient.newHttpClient();
        URI endpoint = URI.create("http://127.0.0.1:" + port + "/mcp");
        String initialize = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{}}";
        var denied = client.send(HttpRequest.newBuilder(endpoint).POST(HttpRequest.BodyPublishers.ofString(initialize)).build(), HttpResponse.BodyHandlers.ofString());
        assertEquals(401, denied.statusCode());
        var accepted = client.send(request(endpoint, initialize), HttpResponse.BodyHandlers.ofString());
        assertEquals(200, accepted.statusCode());
        assertTrue(accepted.body().contains("2025-06-18"));
        var listed = client.send(request(endpoint, "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\"}"), HttpResponse.BodyHandlers.ofString());
        assertTrue(listed.body().contains("run_console_command"));
        var called = client.send(request(endpoint, "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/call\",\"params\":{\"name\":\"run_console_command\",\"arguments\":{\"command\":\"list\"}}}"), HttpResponse.BodyHandlers.ofString());
        assertTrue(called.body().contains("dispatched"));
        assertTrue(java.nio.file.Files.exists(directory.resolve("data/audit.jsonl")));
    }

    private static HttpRequest request(URI endpoint, String body) {
        return HttpRequest.newBuilder(endpoint).header("Authorization", "Bearer 01234567890123456789012345678901").header("Content-Type", "application/json").POST(HttpRequest.BodyPublishers.ofString(body)).build();
    }

    private final class FakePlatform implements ControlPlatform {
        @Override public String platformName() { return "test"; }
        @Override public Path serverDirectory() { return directory; }
        @Override public CompletableFuture<JsonObject> serverStatus() { return result("status", "ok"); }
        @Override public CompletableFuture<JsonObject> performance() { return result("tps", "20"); }
        @Override public CompletableFuture<JsonObject> plugins() { return result("plugins", "test"); }
        @Override public CompletableFuture<JsonObject> players() { return result("players", "none"); }
        @Override public CompletableFuture<JsonObject> commands() { return result("commands", "list"); }
        @Override public CompletableFuture<JsonObject> executeCommand(String command) { JsonObject value = new JsonObject(); value.addProperty("dispatched", true); return CompletableFuture.completedFuture(value); }
        @Override public void logInfo(String message) { }
        @Override public void logWarning(String message, Throwable error) { }
        private CompletableFuture<JsonObject> result(String key, String value) { JsonObject object = new JsonObject(); object.addProperty(key, value); return CompletableFuture.completedFuture(object); }
    }
}
