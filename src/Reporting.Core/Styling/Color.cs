using System.Globalization;

namespace Reporting.Styling;

/// <summary>An sRGB color with alpha. Channel values are 0–255.</summary>
public readonly record struct Color(byte R, byte G, byte B, byte A)
{
    public static readonly Color Transparent = new(0, 0, 0, 0);
    public static readonly Color Black = new(0, 0, 0, 255);
    public static readonly Color White = new(255, 255, 255, 255);
    public static readonly Color Red = new(255, 0, 0, 255);
    public static readonly Color Green = new(0, 128, 0, 255);
    public static readonly Color Blue = new(0, 0, 255, 255);
    public static readonly Color Gray = new(128, 128, 128, 255);
    public static readonly Color LightGray = new(211, 211, 211, 255);

    public static Color FromRgb(byte r, byte g, byte b) => new(r, g, b, 255);

    public static Color FromArgb(byte a, byte r, byte g, byte b) => new(r, g, b, a);

    /// <summary>Parses <c>#RRGGBB</c> or <c>#AARRGGBB</c>.</summary>
    public static Color FromHex(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);
        var span = hex.AsSpan().TrimStart('#');
        return span.Length switch
        {
            6 => new(
                byte.Parse(span[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(span[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(span[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                255),
            8 => new(
                byte.Parse(span[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(span[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(span[6..8], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(span[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture)),
            _ => throw new FormatException($"Invalid color hex literal '{hex}'. Expected #RRGGBB or #AARRGGBB."),
        };
    }

    public string ToHex()
        => A == 255
            ? string.Create(CultureInfo.InvariantCulture, $"#{R:X2}{G:X2}{B:X2}")
            : string.Create(CultureInfo.InvariantCulture, $"#{A:X2}{R:X2}{G:X2}{B:X2}");

    public override string ToString() => ToHex();
}
