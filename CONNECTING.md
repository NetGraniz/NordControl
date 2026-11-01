# Connecting an AI agent

The recommended deployment keeps NordControl private on the Minecraft host and carries MCP traffic through SSH. This works with any MCP client that supports Streamable HTTP and custom request headers; it is not tied to a specific AI vendor.

## Connection architecture

```text
AI agent computer
  127.0.0.1:18765 ─┐
                    ├─ encrypted SSH connection ─► Minecraft host
  127.0.0.1:18766 ─┘                              ├─ 127.0.0.1:8765 (Paper/Folia)
                                                   └─ 127.0.0.1:8766 (Velocity)
```

There are two independent security checks:

1. SSH authenticates the computer/user with an SSH key and encrypts the connection.
2. NordControl checks the direct peer address and the `Authorization: Bearer ...` token.

Because the last hop originates on the Minecraft host, NordControl sees it as `127.0.0.1`. Keep the default loopback entries in `allowedAddresses`; do not add the agent computer's LAN or public address when using this design.

## 1. Install and start NordControl

Install the matching JAR and start the server/proxy once. The generated files are normally:

```text
Paper/Folia: plugins/NordControl/config.json
Velocity:    plugins/nordcontrol/config.json
```

Windows treats those two directory names as equivalent. Linux hosts may preserve the platform-specific casing shown above.

Keep these defaults:

```json
{
  "bindAddress": "127.0.0.1",
  "allowedAddresses": ["127.0.0.1", "::1", "0:0:0:0:0:0:0:1"]
}
```

Paper/Folia uses port `8765`; Velocity uses port `8766`. Each instance generates a separate token. Do not commit or send either token in chat.

## 2. Configure SSH key access

The agent computer must be able to log in to the Minecraft host with a dedicated SSH key. Test that first:

```bash
ssh -i ~/.ssh/minecraft_agent_ed25519 minecraft-agent@minecraft-host
```

Use an unprivileged operating-system account when possible. NordControl itself never uses the SSH account and cannot execute operating-system commands; SSH is only the encrypted carrier in this architecture.

## 3. Open the tunnel

Linux, macOS, and Windows OpenSSH:

```bash
ssh -N \
  -i ~/.ssh/minecraft_agent_ed25519 \
  -o ExitOnForwardFailure=yes \
  -o ServerAliveInterval=30 \
  -L 18765:127.0.0.1:8765 \
  -L 18766:127.0.0.1:8766 \
  minecraft-agent@minecraft-host
```

PowerShell uses the same OpenSSH client. A single-line form is convenient on Windows:

```powershell
ssh.exe -N -i "$HOME\.ssh\minecraft_agent_ed25519" -o ExitOnForwardFailure=yes -o ServerAliveInterval=30 -L 18765:127.0.0.1:8765 -L 18766:127.0.0.1:8766 minecraft-agent@minecraft-host
```

`-N` means “do not open a remote shell”; only forward the declared ports. The terminal remains occupied while the tunnel is active. Local ports `18765` and `18766` are intentionally different from the remote defaults, but you may choose other unused local ports.

If you run only one platform, include only its `-L` argument.

## 4. Configure the MCP client

Use one MCP server entry per Minecraft platform:

| Platform | MCP URL | Token source |
| --- | --- | --- |
| Paper/Folia | `http://127.0.0.1:18765/mcp` | Paper/Folia `config.json` |
| Velocity | `http://127.0.0.1:18766/mcp` | Velocity `config.json` |

The conceptual client configuration is:

```json
{
  "transport": "streamable-http",
  "url": "http://127.0.0.1:18765/mcp",
  "headers": {
    "Authorization": "Bearer ${NORDCONTROL_PAPER_TOKEN}"
  }
}
```

Exact field names and environment-variable syntax vary between MCP clients. Store the token in the client's secret/environment facility rather than directly in a version-controlled configuration file.

## 5. Verify without running a command

The authenticated health endpoint confirms the tunnel and token without changing Minecraft state.

PowerShell:

```powershell
$headers = @{ Authorization = "Bearer $env:NORDCONTROL_PAPER_TOKEN" }
Invoke-RestMethod http://127.0.0.1:18765/health -Headers $headers
```

Linux/macOS:

```bash
curl --fail --silent \
  -H "Authorization: Bearer $NORDCONTROL_PAPER_TOKEN" \
  http://127.0.0.1:18765/health
```

A successful response contains `"status":"ok"`. You can then let the MCP client call `tools/list` or the read-only `get_server_status` tool.

## What the agent can control

The `run_console_command` tool dispatches commands as the Minecraft console and therefore has full authority over commands registered on that Paper/Folia server or Velocity proxy. The agent can also inspect status, JVM/platform performance, loaded plugins, players, available commands, and recent logs/errors.

The tool returns whether Minecraft accepted the dispatch. Many commands write their human-readable result only to the server log, so the agent can call `get_recent_logs` afterward.

NordControl does not provide shell commands, process creation, arbitrary Java execution, or arbitrary file reads. Other Minecraft plugins may expose powerful console commands of their own, so review the audit log and protect the token as an administrator credential.

## Reloading configuration

After changing the token, IP allowlist, timeout, or log limit, run this in the corresponding Paper/Folia or Velocity console:

```text
nordcontrol reload
```

Each platform has its own configuration and must be reloaded separately. A validation failure keeps the previous configuration active.

Changing `bindAddress`, `port`, or `enabled` changes the listener lifecycle and requires a normal server/proxy restart. An SSH tunnel does not require changing any of those settings.

## Troubleshooting

| Symptom | Likely cause | Check |
| --- | --- | --- |
| Local connection refused | Tunnel is not running or local port is wrong | Restart SSH with `ExitOnForwardFailure=yes` |
| SSH reports remote connection refused | NordControl is not listening on the host | Check plugin startup log and `config.json` port |
| HTTP 401 | Missing/wrong token | Use the token belonging to that specific platform |
| HTTP 403 | Peer IP or browser Origin rejected | Restore loopback allowlist; use a non-browser MCP client through SSH |
| Health works but MCP client does not | Wrong transport or header configuration | Select Streamable HTTP and add the Bearer header |
| Reload reports bind/port error | Listener setting was edited | Restore the active value or perform a planned restart |

Do not solve connectivity problems by binding to `0.0.0.0` unless you deliberately want a network-facing administrator API and have added an appropriate firewall and transport-encryption layer.
