package dev.nordcontrol.common;

import com.google.gson.Gson;
import com.google.gson.GsonBuilder;
import com.google.gson.JsonParseException;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardCopyOption;
import java.security.SecureRandom;
import java.util.Base64;
import java.util.List;

public record ControlConfig(
        boolean enabled,
        String bindAddress,
        int port,
        String token,
        List<String> allowedAddresses,
        int commandTimeoutSeconds,
        int maxLogLines) {
    private static final Gson GSON = new GsonBuilder().setPrettyPrinting().create();

    public static ControlConfig loadOrCreate(Path file, int defaultPort) throws IOException {
        if (!Files.exists(file)) {
            Files.createDirectories(file.getParent());
            var config = defaults(defaultPort);
            writeAtomic(file, config);
            return config;
        }
        try {
            var config = GSON.fromJson(Files.readString(file, StandardCharsets.UTF_8), ControlConfig.class);
            validate(config);
            return new ControlConfig(config.enabled, config.bindAddress, config.port, config.token,
                    List.copyOf(config.allowedAddresses), config.commandTimeoutSeconds, config.maxLogLines);
        } catch (JsonParseException | NullPointerException exception) {
            throw new IOException("Invalid config.json: " + exception.getMessage(), exception);
        }
    }

    public static ControlConfig defaults(int port) {
        byte[] bytes = new byte[32];
        new SecureRandom().nextBytes(bytes);
        return new ControlConfig(true, "127.0.0.1", port,
                Base64.getUrlEncoder().withoutPadding().encodeToString(bytes),
                List.of("127.0.0.1", "::1", "0:0:0:0:0:0:0:1"), 15, 500);
    }

    public static void validate(ControlConfig config) throws IOException {
        if (config == null) throw new IOException("Configuration is empty");
        if (config.bindAddress == null || config.bindAddress.isBlank()) throw new IOException("bindAddress is required");
        if (config.port < 1 || config.port > 65535) throw new IOException("port must be between 1 and 65535");
        if (config.token == null || config.token.length() < 32) throw new IOException("token must contain at least 32 characters");
        if (config.allowedAddresses == null || config.allowedAddresses.isEmpty()) throw new IOException("allowedAddresses must not be empty");
        if (config.allowedAddresses.stream().anyMatch(value -> value == null || value.isBlank())) throw new IOException("allowedAddresses contains an empty value");
        if (config.commandTimeoutSeconds < 1 || config.commandTimeoutSeconds > 120) throw new IOException("commandTimeoutSeconds must be between 1 and 120");
        if (config.maxLogLines < 1 || config.maxLogLines > 5000) throw new IOException("maxLogLines must be between 1 and 5000");
    }

    private static void writeAtomic(Path file, ControlConfig config) throws IOException {
        Path temporary = file.resolveSibling(file.getFileName() + ".tmp");
        Files.writeString(temporary, GSON.toJson(config), StandardCharsets.UTF_8);
        try {
            Files.move(temporary, file, StandardCopyOption.ATOMIC_MOVE, StandardCopyOption.REPLACE_EXISTING);
        } catch (java.nio.file.AtomicMoveNotSupportedException exception) {
            Files.move(temporary, file, StandardCopyOption.REPLACE_EXISTING);
        }
    }
}
