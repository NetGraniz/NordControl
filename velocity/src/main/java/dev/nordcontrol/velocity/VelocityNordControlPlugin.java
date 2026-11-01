package dev.nordcontrol.velocity;

import com.google.gson.JsonArray;
import com.google.gson.JsonObject;
import com.google.inject.Inject;
import com.velocitypowered.api.command.SimpleCommand;
import com.velocitypowered.api.event.Subscribe;
import com.velocitypowered.api.event.proxy.ProxyInitializeEvent;
import com.velocitypowered.api.event.proxy.ProxyShutdownEvent;
import com.velocitypowered.api.plugin.Plugin;
import com.velocitypowered.api.plugin.annotation.DataDirectory;
import com.velocitypowered.api.proxy.ProxyServer;
import dev.nordcontrol.common.ControlPlatform;
import dev.nordcontrol.common.NordControlRuntime;
import net.kyori.adventure.text.Component;
import org.slf4j.Logger;

import java.lang.management.ManagementFactory;
import java.nio.file.Path;
import java.util.Comparator;
import java.util.concurrent.CompletableFuture;

@Plugin(id = "nordcontrol", name = "NordControl", version = "0.1.0", description = "Secure MCP control and monitoring bridge for Velocity", authors = {"NetGraniz"}, url = "https://github.com/NetGraniz/NordControl")
public final class VelocityNordControlPlugin implements ControlPlatform {
    private final ProxyServer proxy;
    private final Logger logger;
    private final Path dataDirectory;
    private NordControlRuntime runtime;

    @Inject public VelocityNordControlPlugin(ProxyServer proxy, Logger logger, @DataDirectory Path dataDirectory) {
        this.proxy = proxy; this.logger = logger; this.dataDirectory = dataDirectory;
    }

    @Subscribe public void onInitialize(ProxyInitializeEvent event) {
        runtime = new NordControlRuntime(this, dataDirectory, 8766);
        proxy.getCommandManager().register(proxy.getCommandManager().metaBuilder("nordcontrol").plugin(this).build(), new AdminCommand());
        try { runtime.start(); }
        catch (Exception exception) { logger.error("NordControl could not start", exception); }
    }

    @Subscribe public void onShutdown(ProxyShutdownEvent event) { if (runtime != null) runtime.close(); }

    private final class AdminCommand implements SimpleCommand {
        @Override public void execute(Invocation invocation) {
            String[] args = invocation.arguments();
            if (args.length == 0 || args[0].equalsIgnoreCase("status")) {
                var active = runtime.config();
                invocation.source().sendMessage(Component.text(active == null ? "NordControl is disabled." : "NordControl is listening on " + active.bindAddress() + ":" + active.port()));
                return;
            }
            if (args[0].equalsIgnoreCase("reload")) {
                proxy.getScheduler().buildTask(VelocityNordControlPlugin.this, () -> {
                    try { invocation.source().sendMessage(Component.text(runtime.reload())); }
                    catch (Exception exception) { invocation.source().sendMessage(Component.text("NordControl reload failed; previous configuration remains active: " + exception.getMessage())); }
                }).schedule();
                return;
            }
            invocation.source().sendMessage(Component.text("Usage: /nordcontrol <status|reload>"));
        }
        @Override public boolean hasPermission(Invocation invocation) { return invocation.source() == proxy.getConsoleCommandSource() || invocation.source().hasPermission("nordcontrol.admin"); }
    }

    @Override public String platformName() { return "Velocity"; }
    @Override public Path serverDirectory() { return Path.of("").toAbsolutePath().normalize(); }

    @Override public CompletableFuture<JsonObject> serverStatus() {
        JsonObject result = new JsonObject();
        result.addProperty("platform", "Velocity");
        result.addProperty("version", proxy.getVersion().getVersion());
        result.addProperty("vendor", proxy.getVersion().getVendor());
        result.addProperty("onlinePlayers", proxy.getPlayerCount());
        result.addProperty("registeredServers", proxy.getAllServers().size());
        result.addProperty("shuttingDown", proxy.isShuttingDown());
        return CompletableFuture.completedFuture(result);
    }

    @Override public CompletableFuture<JsonObject> performance() {
        Runtime jvm = Runtime.getRuntime(); JsonObject result = new JsonObject();
        result.addProperty("heapUsedBytes", jvm.totalMemory() - jvm.freeMemory());
        result.addProperty("heapCommittedBytes", jvm.totalMemory());
        result.addProperty("heapMaxBytes", jvm.maxMemory());
        result.addProperty("availableProcessors", jvm.availableProcessors());
        result.addProperty("uptimeMillis", ManagementFactory.getRuntimeMXBean().getUptime());
        result.addProperty("note", "Velocity does not expose a TPS metric; profile event-loop and plugin load with spark when deeper attribution is needed.");
        return CompletableFuture.completedFuture(result);
    }

    @Override public CompletableFuture<JsonObject> plugins() {
        JsonArray entries = new JsonArray();
        proxy.getPluginManager().getPlugins().stream().sorted(Comparator.comparing(plugin -> plugin.getDescription().getId())).forEach(plugin -> {
            JsonObject item = new JsonObject();
            item.addProperty("id", plugin.getDescription().getId());
            plugin.getDescription().getName().ifPresent(value -> item.addProperty("name", value));
            plugin.getDescription().getVersion().ifPresent(value -> item.addProperty("version", value));
            entries.add(item);
        });
        JsonObject result = new JsonObject(); result.add("plugins", entries); return CompletableFuture.completedFuture(result);
    }

    @Override public CompletableFuture<JsonObject> players() {
        JsonArray entries = new JsonArray();
        proxy.getAllPlayers().stream().sorted(Comparator.comparing(player -> player.getUsername().toLowerCase())).forEach(player -> {
            JsonObject item = new JsonObject(); item.addProperty("name", player.getUsername()); item.addProperty("uuid", player.getUniqueId().toString());
            player.getCurrentServer().ifPresent(server -> item.addProperty("server", server.getServerInfo().getName())); entries.add(item);
        });
        JsonObject result = new JsonObject(); result.add("players", entries); return CompletableFuture.completedFuture(result);
    }

    @Override public CompletableFuture<JsonObject> commands() {
        JsonArray entries = new JsonArray(); proxy.getCommandManager().getAliases().stream().sorted().forEach(entries::add);
        JsonObject result = new JsonObject(); result.add("commands", entries); return CompletableFuture.completedFuture(result);
    }

    @Override public CompletableFuture<JsonObject> executeCommand(String command) {
        return proxy.getCommandManager().executeAsync(proxy.getConsoleCommandSource(), command).thenApply(dispatched -> {
            JsonObject result = new JsonObject(); result.addProperty("command", command); result.addProperty("dispatched", dispatched);
            result.addProperty("note", "Command output is written by Velocity/plugins to the proxy log; use get_recent_logs to inspect it."); return result;
        });
    }

    @Override public void logInfo(String message) { logger.info(message); }
    @Override public void logWarning(String message, Throwable error) { logger.warn(message, error); }
}
