package dev.nordcontrol.common;

import com.google.gson.JsonArray;
import com.google.gson.JsonObject;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayDeque;
import java.util.Locale;
import java.util.regex.Pattern;

final class LogReader {
    private static final Pattern IPV4 = Pattern.compile("(?<![\\d.])(?:\\d{1,3}\\.){3}\\d{1,3}(?![\\d.])");

    static JsonObject read(Path serverDirectory, int requested, int maximum, boolean errorsOnly) throws IOException {
        int count = Math.max(1, Math.min(requested, maximum));
        Path logs = serverDirectory.toAbsolutePath().normalize().resolve("logs").resolve("latest.log").normalize();
        Path allowedRoot = serverDirectory.toAbsolutePath().normalize();
        if (!logs.startsWith(allowedRoot)) throw new IOException("Resolved log path escaped the server directory");
        ArrayDeque<String> tail = new ArrayDeque<>(count);
        if (Files.exists(logs)) {
            try (var lines = Files.lines(logs, StandardCharsets.UTF_8)) {
                lines.filter(line -> !errorsOnly || isError(line)).forEach(line -> {
                    if (tail.size() == count) tail.removeFirst();
                    tail.addLast(IPV4.matcher(line).replaceAll("<ip>"));
                });
            }
        }
        JsonArray result = new JsonArray();
        tail.forEach(result::add);
        JsonObject object = new JsonObject();
        object.addProperty("source", "logs/latest.log");
        object.addProperty("errorsOnly", errorsOnly);
        object.add("lines", result);
        return object;
    }

    private static boolean isError(String line) {
        String lower = line.toLowerCase(Locale.ROOT);
        return lower.contains("error") || lower.contains("exception") || lower.contains("severe") || lower.contains("warn");
    }
}
