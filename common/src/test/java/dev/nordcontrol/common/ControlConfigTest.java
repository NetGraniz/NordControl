package dev.nordcontrol.common;

import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;

import java.nio.file.Files;
import java.nio.file.Path;
import java.util.List;

import static org.junit.jupiter.api.Assertions.*;

class ControlConfigTest {
    @TempDir Path directory;

    @Test void createsSecureDefaultsAndReloadsThem() throws Exception {
        Path file = directory.resolve("config.json");
        ControlConfig created = ControlConfig.loadOrCreate(file, 8765);
        assertTrue(Files.exists(file));
        assertEquals("127.0.0.1", created.bindAddress());
        assertEquals(8765, created.port());
        assertTrue(created.token().length() >= 32);
        assertEquals(created, ControlConfig.loadOrCreate(file, 9999));
    }

    @Test void rejectsUnsafeValues() {
        var invalid = new ControlConfig(true, "127.0.0.1", 0, "short", List.of(), 0, 0);
        assertThrows(java.io.IOException.class, () -> ControlConfig.validate(invalid));
    }
}
