package dev.nordcontrol.common;

import java.io.IOException;
import java.nio.file.Path;

public final class NordControlRuntime implements AutoCloseable {
    private final ControlPlatform platform;
    private final Path dataDirectory;
    private final Path configFile;
    private final int defaultPort;
    private NordControlServer server;

    public NordControlRuntime(ControlPlatform platform, Path dataDirectory, int defaultPort) {
        this.platform = platform;
        this.dataDirectory = dataDirectory;
        this.configFile = dataDirectory.resolve("config.json");
        this.defaultPort = defaultPort;
    }

    public synchronized void start() throws IOException {
        ControlConfig config = ControlConfig.loadOrCreate(configFile, defaultPort);
        if (!config.enabled()) {
            platform.logInfo("NordControl is disabled in config.json");
            return;
        }
        server = new NordControlServer(platform, config, dataDirectory);
        server.start();
    }

    public synchronized String reload() throws IOException {
        ControlConfig replacement = ControlConfig.loadOrCreate(configFile, defaultPort);
        if (server == null) {
            if (!replacement.enabled()) return "Configuration valid; NordControl remains disabled (restart required to enable)";
            throw new IOException("NordControl HTTP server is not running; restart is required to enable it");
        }
        if (!replacement.enabled()) throw new IOException("Disabling the HTTP listener requires a plugin/server restart");
        server.applyReload(replacement);
        return "Configuration reloaded; token, IP allowlist and limits are active";
    }

    public synchronized ControlConfig config() { return server == null ? null : server.config(); }

    @Override public synchronized void close() { if (server != null) { server.close(); server = null; } }
}
