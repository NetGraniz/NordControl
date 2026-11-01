package dev.nordcontrol.common;

import com.google.gson.Gson;
import com.google.gson.JsonObject;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardCopyOption;
import java.time.Instant;

final class AuditLog {
    private static final long MAX_SIZE = 10L * 1024L * 1024L;
    private static final Gson GSON = new Gson();
    private final Path file;

    AuditLog(Path file) { this.file = file; }

    synchronized void record(String remote, String action, String detail, boolean success) {
        try {
            Files.createDirectories(file.getParent());
            if (Files.exists(file) && Files.size(file) >= MAX_SIZE) {
                Files.move(file, file.resolveSibling("audit.previous.jsonl"), StandardCopyOption.REPLACE_EXISTING);
            }
            JsonObject event = new JsonObject();
            event.addProperty("time", Instant.now().toString());
            event.addProperty("remote", remote);
            event.addProperty("action", action);
            event.addProperty("detail", detail);
            event.addProperty("success", success);
            Files.writeString(file, GSON.toJson(event) + System.lineSeparator(), StandardCharsets.UTF_8,
                    java.nio.file.StandardOpenOption.CREATE, java.nio.file.StandardOpenOption.APPEND);
        } catch (IOException ignored) {
            // Audit failures are surfaced by the caller's normal plugin logger when practical.
        }
    }
}
