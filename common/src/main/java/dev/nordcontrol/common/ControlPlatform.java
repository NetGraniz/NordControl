package dev.nordcontrol.common;

import com.google.gson.JsonObject;

import java.nio.file.Path;
import java.util.concurrent.CompletableFuture;

public interface ControlPlatform {
    String platformName();
    Path serverDirectory();
    CompletableFuture<JsonObject> serverStatus();
    CompletableFuture<JsonObject> performance();
    CompletableFuture<JsonObject> plugins();
    CompletableFuture<JsonObject> players();
    CompletableFuture<JsonObject> commands();
    CompletableFuture<JsonObject> executeCommand(String command);
    void logInfo(String message);
    void logWarning(String message, Throwable error);
}
