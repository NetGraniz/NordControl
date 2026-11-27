namespace NordControlUi;

internal static class LogHighlightPalette
{
    public static IReadOnlyList<string> ColorNames { get; } =
    [
        "White",
        "Gray",
        "Red",
        "Orange",
        "Amber",
        "Yellow",
        "Lime",
        "Green",
        "Mint",
        "Cyan",
        "Sky",
        "Blue",
        "Indigo",
        "Violet",
        "Magenta",
        "Rose"
    ];

    public static Color Resolve(string? colorName) => colorName switch
    {
        "Gray" => Color.FromArgb(174, 184, 178),
        "Red" => Color.FromArgb(255, 107, 107),
        "Orange" => Color.FromArgb(255, 154, 92),
        "Amber" => Color.FromArgb(255, 190, 92),
        "Yellow" => Color.FromArgb(248, 221, 101),
        "Lime" => Color.FromArgb(190, 230, 95),
        "Green" => Color.FromArgb(105, 211, 142),
        "Mint" => Color.FromArgb(102, 221, 188),
        "Cyan" => Color.FromArgb(95, 211, 224),
        "Sky" => Color.FromArgb(101, 184, 246),
        "Blue" => Color.FromArgb(111, 148, 255),
        "Indigo" => Color.FromArgb(145, 128, 255),
        "Violet" => Color.FromArgb(190, 125, 245),
        "Magenta" => Color.FromArgb(232, 112, 219),
        "Rose" => Color.FromArgb(245, 120, 158),
        _ => Color.FromArgb(226, 235, 228)
    };
}
