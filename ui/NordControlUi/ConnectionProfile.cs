using System.Text.Json;
using System.Text.Json.Serialization;

namespace NordControlUi;

public sealed record ConnectionProfile(string DisplayName, Uri McpEndpoint, Uri HealthEndpoint, string Token);

public static class ConnectionProfileLoader
{
    public static ConnectionProfile Load(string displayName, string configPath)
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException("NordControl configuration was not found.", configPath);
        }

        return Parse(displayName, File.ReadAllText(configPath));
    }

    public static ConnectionProfile Parse(string displayName, string json)
    {
        PluginConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<PluginConfig>(json);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("NordControl configuration contains invalid JSON.", exception);
        }

        if (config is null || !config.Enabled)
        {
            throw new InvalidDataException("NordControl is disabled in this configuration.");
        }

        if (config.Port is < 1 or > 65535)
        {
            throw new InvalidDataException("NordControl port must be between 1 and 65,535.");
        }

        if (string.IsNullOrWhiteSpace(config.Token) || config.Token.Length < 32)
        {
            throw new InvalidDataException("NordControl token is missing or invalid.");
        }

        var host = config.BindAddress switch
        {
            "127.0.0.1" => "127.0.0.1",
            "::1" or "0:0:0:0:0:0:0:1" => "[::1]",
            _ => throw new InvalidDataException(
                "NordControl UI only connects to a loopback listener. Keep bindAddress on 127.0.0.1 and use an SSH tunnel for remote access.")
        };

        var baseAddress = $"http://{host}:{config.Port}";
        return new ConnectionProfile(
            displayName,
            new Uri(baseAddress + "/mcp"),
            new Uri(baseAddress + "/health"),
            config.Token);
    }

    private sealed class PluginConfig
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; init; }

        [JsonPropertyName("bindAddress")]
        public string? BindAddress { get; init; }

        [JsonPropertyName("port")]
        public int Port { get; init; }

        [JsonPropertyName("token")]
        public string? Token { get; init; }
    }
}
