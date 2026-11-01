# Building

Requirements: JDK 21+ and Apache Maven 3.9+.

```bash
mvn clean verify
```

Executable artifacts:

- `paper/target/NordControl-Paper-0.1.0.jar`
- `velocity/target/NordControl-Velocity-0.1.0.jar`

The build compiles the shared server once, shades it and Gson into each platform artifact, and leaves the Paper/Velocity APIs provided by the host.
