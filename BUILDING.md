# Building

Requirements: JDK 25, Apache Maven 3.9+, and .NET SDK 8+ for the optional Windows UI. The current Paper API dependency is compiled for Java 25 even though NordControl's own bytecode target remains Java 21.

```bash
mvn clean verify
```

Release artifact:

- `universal/target/NordControl-0.3.1.jar`

The build compiles both platform adapters and packages them, the shared server, and Gson into one universal JAR. Paper and Velocity APIs remain provided by the host.

Publish a self-contained Windows executable:

```powershell
dotnet publish ui/NordControlUi/NordControlUi.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o artifacts/ui
```

The published `artifacts/ui/NordControlUi.exe` does not require a separately installed .NET runtime.
