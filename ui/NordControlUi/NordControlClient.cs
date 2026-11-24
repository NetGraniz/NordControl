using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace NordControlUi;

public sealed record ToolCallResult(bool IsError, JsonElement StructuredContent)
{
    public string Format() => JsonSerializer.Serialize(StructuredContent, new JsonSerializerOptions { WriteIndented = true });
}

public sealed class NordControlClient : IDisposable
{
    private readonly HttpClient httpClient;

    public NordControlClient()
    {
        httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    }

    public async Task CheckHealthAsync(ConnectionProfile profile, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, profile.HealthEndpoint, profile.Token);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public Task<ToolCallResult> RunCommandAsync(
        ConnectionProfile profile,
        string command,
        CancellationToken cancellationToken = default) =>
        CallToolAsync(profile, "run_console_command", new { command }, cancellationToken);

    public Task<ToolCallResult> GetRecentLogsAsync(
        ConnectionProfile profile,
        int lines = 80,
        CancellationToken cancellationToken = default) =>
        CallToolAsync(profile, "get_recent_logs", new { lines }, cancellationToken);

    public static ToolCallResult ParseToolResponse(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;
        if (root.TryGetProperty("error", out var error))
        {
            throw new InvalidOperationException("NordControl returned an RPC error: " + error.GetRawText());
        }

        if (!root.TryGetProperty("result", out var result))
        {
            throw new InvalidDataException("NordControl response does not contain an RPC result.");
        }

        var isError = result.TryGetProperty("isError", out var errorFlag) && errorFlag.GetBoolean();
        var structuredContent = result.TryGetProperty("structuredContent", out var content)
            ? content.Clone()
            : result.Clone();
        return new ToolCallResult(isError, structuredContent);
    }

    public void Dispose() => httpClient.Dispose();

    private async Task<ToolCallResult> CallToolAsync(
        ConnectionProfile profile,
        string toolName,
        object arguments,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = Guid.NewGuid().ToString("N"),
            method = "tools/call",
            @params = new { name = toolName, arguments }
        });

        using var request = CreateRequest(HttpMethod.Post, profile.McpEndpoint, profile.Token);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken, responseBody);
        return ParseToolResponse(responseBody);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, Uri endpoint, string token)
    {
        var request = new HttpRequestMessage(method, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken,
        string? responseBody = null)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        responseBody ??= await response.Content.ReadAsStringAsync(cancellationToken);
        var message = response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => "NordControl rejected the token. Check that the selected configuration belongs to this server.",
            System.Net.HttpStatusCode.Forbidden => "NordControl rejected this computer's address.",
            _ => $"NordControl returned HTTP {(int)response.StatusCode}. {responseBody}"
        };
        throw new HttpRequestException(message, null, response.StatusCode);
    }
}
