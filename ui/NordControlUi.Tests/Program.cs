using NordControlUi;
using System.Reflection;
using System.Windows.Forms;

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
Check("ships without server-specific paths", () =>
{
    Equal(string.Empty, AppSettings.Defaults.FoliaConfigPath);
    Equal(string.Empty, AppSettings.Defaults.VelocityConfigPath);
    Equal(false, AppSettings.Defaults.IsComplete);
});
Check("keeps Execute visible at 150 percent scaling", VerifyExecuteButtonLayout);

if (failures.Count > 0)
{
    Console.Error.WriteLine(string.Join(Environment.NewLine, failures));
    return 1;
}

Console.WriteLine("NordControlUi tests passed (9/9).");
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

static void VerifyExecuteButtonLayout()
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            var formType = typeof(CommandValidator).Assembly.GetType("NordControlUi.MainForm", true)!;
            using var form = (Form)Activator.CreateInstance(
                formType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                null,
                null)!;
            form.CreateControl();
            form.Scale(new SizeF(1.5F, 1.5F));
            PerformLayout(form);
            var execute = FindButton(form, "Execute") ?? throw new InvalidOperationException("Execute button was not found.");
            if (execute.Width < 100 || execute.Height < 36)
            {
                throw new InvalidOperationException($"Execute button is clipped: {execute.Bounds}.");
            }

            if (execute.Parent is null || !execute.Parent.ClientRectangle.Contains(execute.Bounds))
            {
                throw new InvalidOperationException($"Execute button {execute.Bounds} extends outside its parent {execute.Parent?.ClientRectangle}.");
            }
        }
        catch (Exception exception)
        {
            failure = exception;
        }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();
    if (failure is not null)
    {
        throw failure;
    }
}

static void PerformLayout(Control control)
{
    control.PerformLayout();
    foreach (Control child in control.Controls)
    {
        PerformLayout(child);
    }
}

static Button? FindButton(Control control, string text)
{
    foreach (Control child in control.Controls)
    {
        if (child is Button button && button.Text == text)
        {
            return button;
        }

        var nested = FindButton(child, text);
        if (nested is not null)
        {
            return nested;
        }
    }

    return null;
}
