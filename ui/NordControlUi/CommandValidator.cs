namespace NordControlUi;

public static class CommandValidator
{
    public static string Normalize(string? value)
    {
        var command = (value ?? string.Empty).Trim();
        if (command.StartsWith('/'))
        {
            command = command[1..].TrimStart();
        }

        if (command.Length == 0)
        {
            throw new ArgumentException("Enter a console command.");
        }

        if (command.Length > 2048)
        {
            throw new ArgumentException("The command cannot exceed 2,048 characters.");
        }

        if (command.IndexOfAny(['\r', '\n', '\0']) >= 0)
        {
            throw new ArgumentException("The command must contain a single line.");
        }

        return command;
    }
}
