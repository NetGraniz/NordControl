# NordControl

NordControl is a lightweight, vendor-neutral [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) bridge for Minecraft. It lets an authorized AI agent inspect and administer a Paper/Folia server or Velocity proxy through Minecraft's own console command API.

It does **not** expose a shell, process execution, arbitrary Java evaluation, or arbitrary file access. Full control means full **Minecraft console** privileges only.

## Platforms

- `NordControl-Paper-<version>.jar` — Paper and Folia
- `NordControl-Velocity-<version>.jar` — Velocity
- Java 21 or newer

Put the appropriate JAR in the platform's `plugins` directory and start it once. Each platform creates its own `plugins/NordControl/config.json` with a random 256-bit bearer token.

## MCP endpoint

```text
http://127.0.0.1:8765/mcp  # Paper/Folia
http://127.0.0.1:8766/mcp  # Velocity
```

Send the generated token only in the `Authorization: Bearer <token>` header. For a remote agent, keep `bindAddress` on `127.0.0.1` and use an SSH tunnel:

```bash
ssh -L 8765:127.0.0.1:8765 user@minecraft-host
```

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

See [SECURITY.md](SECURITY.md) before exposing the service beyond loopback and [BUILDING.md](BUILDING.md) to build from source.

Licensed under MIT.
