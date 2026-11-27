# NordControl

NordControl is a lightweight, vendor-neutral [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) bridge for Minecraft. It lets an authorized AI agent inspect and administer a Paper/Folia server or Velocity proxy through Minecraft's own console command API.

It does **not** expose a shell, process execution, arbitrary Java evaluation, or arbitrary file access. Full control means full **Minecraft console** privileges only.

## Platforms

- `NordControl-<version>.jar` — the same universal JAR for Paper, Folia, and Velocity
- `NordControlUi-<version>-win-x64.exe` — local Windows console-command client
- Java 21 or newer

Put the universal JAR in each platform's `plugins` directory and start it once. Each platform keeps its own `plugins/NordControl/config.json`, including its own port and random 256-bit bearer token.

## MCP endpoint

```text
http://127.0.0.1:8765/mcp  # Paper/Folia
http://127.0.0.1:8766/mcp  # Velocity
```

Send the generated token only in the `Authorization: Bearer <token>` header. For a remote agent, keep `bindAddress` on `127.0.0.1` and use an SSH tunnel:

```bash
ssh -N \
  -L 18765:127.0.0.1:8765 \
  -L 18766:127.0.0.1:8766 \
  user@minecraft-host
```

The agent then connects to `http://127.0.0.1:18765/mcp` for Paper/Folia or `http://127.0.0.1:18766/mcp` for Velocity. The Minecraft host sees the tunneled connection as loopback, so you do not need to add the agent computer's LAN or public IP to `allowedAddresses`.

See [CONNECTING.md](CONNECTING.md) for the complete architecture, Windows and Linux/macOS commands, MCP client configuration, verification, and troubleshooting.

## Tools

- `get_server_status`
- `get_performance`
- `list_plugins`
- `list_players`
- `list_available_commands`
- `get_recent_logs`
- `get_recent_errors`
- `run_console_command`

Command execution is asynchronous at the platform boundary. The tool reports whether the command was dispatched; use `get_recent_logs` to inspect output emitted by Minecraft or another plugin.

## Configuration

```json
{
  "enabled": true,
  "bindAddress": "127.0.0.1",
  "port": 8765,
  "token": "generated-secret",
  "allowedAddresses": ["127.0.0.1", "::1", "0:0:0:0:0:0:0:1"],
  "commandTimeoutSeconds": 15,
  "maxLogLines": 500
}
```

Run `nordcontrol reload` from the console, or `/nordcontrol reload` as an authorized player, after editing the file. The new file is fully validated before the active immutable configuration is replaced. A failed reload leaves the previous configuration active.

Token, IP allowlist, timeout, and log limit update live. Changing `bindAddress`, `port`, or `enabled` requires a normal server/proxy restart because it changes the listening socket.

## Auditing

Every tool call, command, and rejected authorization attempt is appended to `plugins/NordControl/audit.jsonl`. It rotates to `audit.previous.jsonl` at 10 MiB. Tokens are never logged. Log-reading tools can only access `logs/latest.log` and redact IPv4 addresses.

See [SECURITY.md](SECURITY.md) before exposing the service beyond loopback, [CONNECTING.md](CONNECTING.md) to connect an agent, and [BUILDING.md](BUILDING.md) to build from source.

## NordControl UI

`NordControlUi` is an optional self-contained Windows application for running Minecraft console commands without opening a server terminal. It supports Folia/Paper and Velocity, dispatches commands directly to the selected target, displays recent logs, and uses the same NordControl audit trail as other clients.

The UI keeps its controls on the left and a full-height log panel on the right. Log highlight rules can match text case-insensitively and color the complete matching line with one of 16 built-in colors.

On first launch, use **Settings** to select the Folia/Paper and Velocity `config.json` files. NordControl UI ships without server-specific paths. Only the paths you select are stored in `%LOCALAPPDATA%\NordControlUi\settings.json`; bearer tokens stay in the plugin configuration files and are neither displayed nor copied into the UI settings.

Run the application on the Minecraft host. NordControl UI deliberately connects only to loopback addresses. For use from another computer, keep NordControl bound to loopback and create an SSH tunnel first; do not expose the MCP port directly to the internet.

NordControlUi is published independently under `ui-v*` tags. Plugin releases under `v*` contain only the universal server JAR.

## License

Current development and new releases are licensed under [GNU GPL v3.0 only](LICENSE).
