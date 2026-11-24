using System.Text.Json;

namespace NordControlUi;

public sealed record AppSettings(string FoliaConfigPath, string VelocityConfigPath)
{
    public static AppSettings Defaults { get; } = new(string.Empty, string.Empty);

    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(FoliaConfigPath) &&
        !string.IsNullOrWhiteSpace(VelocityConfigPath);
}

public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NordControlUi",
        "settings.json");

    public static AppSettings Load()
    {
        if (!File.Exists(SettingsPath))
        {
            return AppSettings.Defaults;
        }

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), JsonOptions)
                   ?? AppSettings.Defaults;
        }
        catch (Exception)
        {
            return AppSettings.Defaults;
        }
    }

    public static void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = SettingsPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temporaryPath, SettingsPath, true);
    }
}
