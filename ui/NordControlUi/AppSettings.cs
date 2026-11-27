using System.Text.Json;

namespace NordControlUi;

public sealed record LogHighlightRule(string ContainsText, string ColorName);

public sealed record AppSettings(
    string FoliaConfigPath,
    string VelocityConfigPath,
    IReadOnlyList<LogHighlightRule>? HighlightRules = null,
    bool AutoRefreshLogs = false,
    int AutoRefreshMinutes = 5)
{
    public static AppSettings Defaults { get; } = new(string.Empty, string.Empty, [], false, 5);

    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(FoliaConfigPath) &&
        !string.IsNullOrWhiteSpace(VelocityConfigPath);

    public IReadOnlyList<LogHighlightRule> EffectiveHighlightRules => HighlightRules ?? [];

    public AppSettings Normalize() => this with
    {
        HighlightRules = EffectiveHighlightRules,
        AutoRefreshMinutes = AutoRefreshMinutes is >= 1 and <= 10_080 ? AutoRefreshMinutes : 5
    };
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
            return (JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), JsonOptions)
                    ?? AppSettings.Defaults).Normalize();
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
