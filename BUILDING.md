# Building

Requirements: JDK 25, Apache Maven 3.9+, and .NET SDK 8+ for the optional Windows UI. The current Paper API dependency is compiled for Java 25 even though NordControl's own bytecode target remains Java 21.

```bash
mvn clean verify
```

Executable artifacts:

- `paper/target/NordControl-Paper-0.2.1.jar`
- `velocity/target/NordControl-Velocity-0.2.1.jar`

The build compiles the shared server once, shades it and Gson into each platform artifact, and leaves the Paper/Velocity APIs provided by the host.

Run the UI checks and publish a self-contained Windows executable:

```powershell
dotnet run --project ui/NordControlUi.Tests/NordControlUi.Tests.csproj -c Release
dotnet publish ui/NordControlUi/NordControlUi.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o artifacts/ui
```

The published `artifacts/ui/NordControlUi.exe` does not require a separately installed .NET runtime.
