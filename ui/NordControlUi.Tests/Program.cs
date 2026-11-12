using NordControlUi;

var failures = new List<string>();

Check("normalizes a leading slash", () => Equal("list", CommandValidator.Normalize(" /list ")));
Check("rejects a multiline command", () => Throws<ArgumentException>(() => CommandValidator.Normalize("list\nstop")));
Check("loads a loopback profile", () =>
{
    var profile = ConnectionProfileLoader.Parse("Folia / Paper", Config("127.0.0.1", 8765));
    Equal("http://127.0.0.1:8765/mcp", profile.McpEndpoint.ToString());
});
Check("rejects a remote listener", () =>
    Throws<InvalidDataException>(() => ConnectionProfileLoader.Parse("Remote", Config("192.0.2.10", 8765))));
Check("rejects a wildcard listener", () =>
    Throws<InvalidDataException>(() => ConnectionProfileLoader.Parse("Wildcard", Config("0.0.0.0", 8765))));
Check("parses a tool result", () =>
{
    var result = NordControlClient.ParseToolResponse("""{"jsonrpc":"2.0","id":"1","result":{"structuredContent":{"dispatched":true},"isError":false}}""");
    Equal(false, result.IsError);
    Equal(true, result.StructuredContent.GetProperty("dispatched").GetBoolean());
});
Check("parses an RPC error", () =>
    Throws<InvalidOperationException>(() => NordControlClient.ParseToolResponse("""{"jsonrpc":"2.0","id":"1","error":{"code":-32600,"message":"bad"}}""")));

if (failures.Count > 0)
{
    Console.Error.WriteLine(string.Join(Environment.NewLine, failures));
    return 1;
}

    Console.WriteLine("NordControlUi tests passed (7/7).");
return 0;

void Check(string name, Action test)
{
    try
    {
        test();
    }
    catch (Exception exception)
    {
        failures.Add($"FAIL: {name}: {exception.Message}");
    }
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }
}

static void Throws<TException>(Action action) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}

static string Config(string bindAddress, int port) =>
    $$"""{"enabled":true,"bindAddress":"{{bindAddress}}","port":{{port}},"token":"12345678901234567890123456789012"}""";
