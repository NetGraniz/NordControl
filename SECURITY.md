# Security

NordControl grants its bearer-token holder the same Minecraft command authority as the server or proxy console. Treat the token as an administrator credential.

- Keep the listener on loopback and connect through SSH or a private authenticated network.
- Keep `allowedAddresses` narrow even when binding to another interface.
- Never put the token in a URL, chat message, command, source file, or public issue.
- Rotate the token in `config.json` and run `nordcontrol reload` if it may have leaked.
- Restrict operating-system permissions on the plugin data directory and audit logs.
- Do not place an untrusted reverse proxy in front of NordControl; authorization uses the direct TCP peer address and ignores forwarded-IP headers.

The recommended remote-access path is an SSH local-forwarding tunnel while NordControl remains bound to `127.0.0.1`. In this arrangement, the plugin correctly sees the SSH server's loopback address as its direct peer; adding the agent computer's LAN/public IP to `allowedAddresses` is unnecessary. See [CONNECTING.md](CONNECTING.md) for the tested topology and commands.

The API has no operating-system command tool, no arbitrary file-path parameter, no reflection/evaluation tool, and no endpoint that returns the bearer token. Minecraft commands can still be destructive and other plugins may expose powerful commands, so audit and scope agent credentials accordingly.

NordControl UI reads the token directly from the selected local plugin configuration each time it connects. It stores only configuration file paths under `%LOCALAPPDATA%\NordControlUi`; it does not display or persist a token. Run the UI only under a trusted Windows account that already has permission to read the relevant plugin directory. The UI refuses non-loopback `bindAddress` values so it cannot silently send an administrator token across the network.

Please report vulnerabilities privately to the repository owner instead of opening a public exploit report.
