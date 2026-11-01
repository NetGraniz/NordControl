package dev.nordcontrol.paper;

import com.google.gson.JsonArray;
import com.google.gson.JsonObject;
import dev.nordcontrol.common.ControlPlatform;
import dev.nordcontrol.common.NordControlRuntime;
import org.bukkit.Bukkit;
import org.bukkit.command.Command;
import org.bukkit.command.CommandSender;
import org.bukkit.plugin.java.JavaPlugin;

import java.lang.management.ManagementFactory;
import java.nio.file.Path;
import java.util.Arrays;
import java.util.Comparator;
import java.util.concurrent.CompletableFuture;

public final class PaperNordControlPlugin extends JavaPlugin implements ControlPlatform {
    private NordControlRuntime runtime;

    @Override public void onEnable() {
        runtime = new NordControlRuntime(this, getDataFolder().toPath(), 8765);
        try {
            runtime.start();
        } catch (Exception exception) {
            getLogger().severe("NordControl could not start: " + exception.getMessage());
            getServer().getPluginManager().disablePlugin(this);
        }
    }

    @Override public void onDisable() { if (runtime != null) runtime.close(); }

    @Override public boolean onCommand(CommandSender sender, Command command, String label, String[] args) {
        if (args.length == 0 || args[0].equalsIgnoreCase("status")) {
            var active = runtime.config();
            sender.sendMessage(active == null ? "NordControl is disabled." : "NordControl is listening on " + active.bindAddress() + ":" + active.port());
            return true;
        }
        if (args[0].equalsIgnoreCase("reload")) {
            getServer().getAsyncScheduler().runNow(this, task -> {
                try { reply(sender, runtime.reload()); }
                catch (Exception exception) { reply(sender, "NordControl reload failed; previous configuration remains active: " + exception.getMessage()); }
            });
            return true;
        }
        sender.sendMessage("Usage: /nordcontrol <status|reload>");
        return true;
    }

    private void reply(CommandSender sender, String message) {
        getServer().getGlobalRegionScheduler().execute(this, () -> sender.sendMessage(message));
    }

    @Override public String platformName() { return "Paper/Folia"; }
    @Override public Path serverDirectory() { return getServer().getWorldContainer().toPath().toAbsolutePath().normalize(); }

    @Override public CompletableFuture<JsonObject> serverStatus() {
        return onGlobal(() -> {
            JsonObject result = new JsonObject();
            result.addProperty("platform", getServer().getName());
            result.addProperty("version", getServer().getVersion());
            result.addProperty("minecraftVersion", getServer().getMinecraftVersion());
            result.addProperty("onlinePlayers", getServer().getOnlinePlayers().size());
            result.addProperty("maxPlayers", getServer().getMaxPlayers());
            return result;
        });
    }

    @Override public CompletableFuture<JsonObject> performance() {
        return onGlobal(() -> {
            JsonObject result = new JsonObject();
            JsonArray tps = new JsonArray(); Arrays.stream(Bukkit.getTPS()).forEach(tps::add);
            result.add("tps", tps);
            result.addProperty("averageTickTimeMs", Bukkit.getAverageTickTime());
            Runtime jvm = Runtime.getRuntime();
            result.addProperty("heapUsedBytes", jvm.totalMemory() - jvm.freeMemory());
            result.addProperty("heapCommittedBytes", jvm.totalMemory());
            result.addProperty("heapMaxBytes", jvm.maxMemory());
            result.addProperty("availableProcessors", jvm.availableProcessors());
            result.addProperty("uptimeMillis", ManagementFactory.getRuntimeMXBean().getUptime());
            return result;
        });
    }

    @Override public CompletableFuture<JsonObject> plugins() {
        return onGlobal(() -> {
            JsonArray entries = new JsonArray();
            Arrays.stream(getServer().getPluginManager().getPlugins()).sorted(Comparator.comparing(plugin -> plugin.getName().toLowerCase())).forEach(plugin -> {
                JsonObject item = new JsonObject(); item.addProperty("name", plugin.getName()); item.addProperty("version", plugin.getPluginMeta().getVersion()); item.addProperty("enabled", plugin.isEnabled()); entries.add(item);
            });
            JsonObject result = new JsonObject(); result.add("plugins", entries); return result;
        });
    }

    @Override public CompletableFuture<JsonObject> players() {
        return onGlobal(() -> {
            JsonArray entries = new JsonArray();
            getServer().getOnlinePlayers().stream().sorted(Comparator.comparing(player -> player.getName().toLowerCase())).forEach(player -> {
                JsonObject item = new JsonObject(); item.addProperty("name", player.getName()); item.addProperty("uuid", player.getUniqueId().toString()); entries.add(item);
            });
            JsonObject result = new JsonObject(); result.add("players", entries); return result;
        });
    }

    @Override public CompletableFuture<JsonObject> commands() {
        return onGlobal(() -> {
            JsonArray entries = new JsonArray();
            getServer().getCommandMap().getKnownCommands().keySet().stream().sorted().forEach(entries::add);
            JsonObject result = new JsonObject(); result.add("commands", entries); return result;
        });
    }

    @Override public CompletableFuture<JsonObject> executeCommand(String command) {
        return onGlobal(() -> {
            boolean dispatched = getServer().dispatchCommand(getServer().getConsoleSender(), command);
            JsonObject result = new JsonObject();
            result.addProperty("command", command);
            result.addProperty("dispatched", dispatched);
            result.addProperty("note", "Command output is written by Minecraft/plugins to the server log; use get_recent_logs to inspect it.");
            return result;
        });
    }

    private CompletableFuture<JsonObject> onGlobal(java.util.concurrent.Callable<JsonObject> work) {
        CompletableFuture<JsonObject> future = new CompletableFuture<>();
        getServer().getGlobalRegionScheduler().execute(this, () -> {
            try { future.complete(work.call()); } catch (Throwable error) { future.completeExceptionally(error); }
        });
        return future;
    }

    @Override public void logInfo(String message) { getLogger().info(message); }
    @Override public void logWarning(String message, Throwable error) { getLogger().warning(message + ": " + error.getMessage()); }
}
