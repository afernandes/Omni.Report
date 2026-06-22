using System.Collections.Generic;
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

    /// <summary>Resolves a CSS/RDL named colour (case-insensitive) to a <see cref="Color"/>, or null when the
    /// name is unknown. Covers the full CSS3 / .NET <c>KnownColor</c> palette that RDL uses; <c>#hex</c>
    /// literals go through <see cref="FromHex"/>. Shared so the RDL importer, the repx/repjson readers and the
    /// expression-binding coercion all agree on the same names. The <c>gray/grey</c> spellings are both
    /// accepted.</summary>
    public static Color? FromName(string? name)
        => name is not null && NamedColors.TryGetValue(name.Trim(), out var c) ? c : null;

    // Full CSS3 / X11 named-colour table (the set RDL and System.Drawing.KnownColor share). Case-insensitive;
    // both gray and grey spellings are present. Built once.
    private static readonly Dictionary<string, Color> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["transparent"] = Transparent,
        ["aliceblue"] = FromHex("#F0F8FF"), ["antiquewhite"] = FromHex("#FAEBD7"), ["aqua"] = FromHex("#00FFFF"),
        ["aquamarine"] = FromHex("#7FFFD4"), ["azure"] = FromHex("#F0FFFF"), ["beige"] = FromHex("#F5F5DC"),
        ["bisque"] = FromHex("#FFE4C4"), ["black"] = FromHex("#000000"), ["blanchedalmond"] = FromHex("#FFEBCD"),
        ["blue"] = FromHex("#0000FF"), ["blueviolet"] = FromHex("#8A2BE2"), ["brown"] = FromHex("#A52A2A"),
        ["burlywood"] = FromHex("#DEB887"), ["cadetblue"] = FromHex("#5F9EA0"), ["chartreuse"] = FromHex("#7FFF00"),
        ["chocolate"] = FromHex("#D2691E"), ["coral"] = FromHex("#FF7F50"), ["cornflowerblue"] = FromHex("#6495ED"),
        ["cornsilk"] = FromHex("#FFF8DC"), ["crimson"] = FromHex("#DC143C"), ["cyan"] = FromHex("#00FFFF"),
        ["darkblue"] = FromHex("#00008B"), ["darkcyan"] = FromHex("#008B8B"), ["darkgoldenrod"] = FromHex("#B8860B"),
        ["darkgray"] = FromHex("#A9A9A9"), ["darkgrey"] = FromHex("#A9A9A9"), ["darkgreen"] = FromHex("#006400"),
        ["darkkhaki"] = FromHex("#BDB76B"), ["darkmagenta"] = FromHex("#8B008B"), ["darkolivegreen"] = FromHex("#556B2F"),
        ["darkorange"] = FromHex("#FF8C00"), ["darkorchid"] = FromHex("#9932CC"), ["darkred"] = FromHex("#8B0000"),
        ["darksalmon"] = FromHex("#E9967A"), ["darkseagreen"] = FromHex("#8FBC8F"), ["darkslateblue"] = FromHex("#483D8B"),
        ["darkslategray"] = FromHex("#2F4F4F"), ["darkslategrey"] = FromHex("#2F4F4F"), ["darkturquoise"] = FromHex("#00CED1"),
        ["darkviolet"] = FromHex("#9400D3"), ["deeppink"] = FromHex("#FF1493"), ["deepskyblue"] = FromHex("#00BFFF"),
        ["dimgray"] = FromHex("#696969"), ["dimgrey"] = FromHex("#696969"), ["dodgerblue"] = FromHex("#1E90FF"),
        ["firebrick"] = FromHex("#B22222"), ["floralwhite"] = FromHex("#FFFAF0"), ["forestgreen"] = FromHex("#228B22"),
        ["fuchsia"] = FromHex("#FF00FF"), ["gainsboro"] = FromHex("#DCDCDC"), ["ghostwhite"] = FromHex("#F8F8FF"),
        ["gold"] = FromHex("#FFD700"), ["goldenrod"] = FromHex("#DAA520"), ["gray"] = FromHex("#808080"),
        ["grey"] = FromHex("#808080"), ["green"] = FromHex("#008000"), ["greenyellow"] = FromHex("#ADFF2F"),
        ["honeydew"] = FromHex("#F0FFF0"), ["hotpink"] = FromHex("#FF69B4"), ["indianred"] = FromHex("#CD5C5C"),
        ["indigo"] = FromHex("#4B0082"), ["ivory"] = FromHex("#FFFFF0"), ["khaki"] = FromHex("#F0E68C"),
        ["lavender"] = FromHex("#E6E6FA"), ["lavenderblush"] = FromHex("#FFF0F5"), ["lawngreen"] = FromHex("#7CFC00"),
        ["lemonchiffon"] = FromHex("#FFFACD"), ["lightblue"] = FromHex("#ADD8E6"), ["lightcoral"] = FromHex("#F08080"),
        ["lightcyan"] = FromHex("#E0FFFF"), ["lightgoldenrodyellow"] = FromHex("#FAFAD2"), ["lightgray"] = FromHex("#D3D3D3"),
        ["lightgrey"] = FromHex("#D3D3D3"), ["lightgreen"] = FromHex("#90EE90"), ["lightpink"] = FromHex("#FFB6C1"),
        ["lightsalmon"] = FromHex("#FFA07A"), ["lightseagreen"] = FromHex("#20B2AA"), ["lightskyblue"] = FromHex("#87CEFA"),
        ["lightslategray"] = FromHex("#778899"), ["lightslategrey"] = FromHex("#778899"), ["lightsteelblue"] = FromHex("#B0C4DE"),
        ["lightyellow"] = FromHex("#FFFFE0"), ["lime"] = FromHex("#00FF00"), ["limegreen"] = FromHex("#32CD32"),
        ["linen"] = FromHex("#FAF0E6"), ["magenta"] = FromHex("#FF00FF"), ["maroon"] = FromHex("#800000"),
        ["mediumaquamarine"] = FromHex("#66CDAA"), ["mediumblue"] = FromHex("#0000CD"), ["mediumorchid"] = FromHex("#BA55D3"),
        ["mediumpurple"] = FromHex("#9370DB"), ["mediumseagreen"] = FromHex("#3CB371"), ["mediumslateblue"] = FromHex("#7B68EE"),
        ["mediumspringgreen"] = FromHex("#00FA9A"), ["mediumturquoise"] = FromHex("#48D1CC"), ["mediumvioletred"] = FromHex("#C71585"),
        ["midnightblue"] = FromHex("#191970"), ["mintcream"] = FromHex("#F5FFFA"), ["mistyrose"] = FromHex("#FFE4E1"),
        ["moccasin"] = FromHex("#FFE4B5"), ["navajowhite"] = FromHex("#FFDEAD"), ["navy"] = FromHex("#000080"),
        ["oldlace"] = FromHex("#FDF5E6"), ["olive"] = FromHex("#808000"), ["olivedrab"] = FromHex("#6B8E23"),
        ["orange"] = FromHex("#FFA500"), ["orangered"] = FromHex("#FF4500"), ["orchid"] = FromHex("#DA70D6"),
        ["palegoldenrod"] = FromHex("#EEE8AA"), ["palegreen"] = FromHex("#98FB98"), ["paleturquoise"] = FromHex("#AFEEEE"),
        ["palevioletred"] = FromHex("#DB7093"), ["papayawhip"] = FromHex("#FFEFD5"), ["peachpuff"] = FromHex("#FFDAB9"),
        ["peru"] = FromHex("#CD853F"), ["pink"] = FromHex("#FFC0CB"), ["plum"] = FromHex("#DDA0DD"),
        ["powderblue"] = FromHex("#B0E0E6"), ["purple"] = FromHex("#800080"), ["rebeccapurple"] = FromHex("#663399"),
        ["red"] = FromHex("#FF0000"), ["rosybrown"] = FromHex("#BC8F8F"), ["royalblue"] = FromHex("#4169E1"),
        ["saddlebrown"] = FromHex("#8B4513"), ["salmon"] = FromHex("#FA8072"), ["sandybrown"] = FromHex("#F4A460"),
        ["seagreen"] = FromHex("#2E8B57"), ["seashell"] = FromHex("#FFF5EE"), ["sienna"] = FromHex("#A0522D"),
        ["silver"] = FromHex("#C0C0C0"), ["skyblue"] = FromHex("#87CEEB"), ["slateblue"] = FromHex("#6A5ACD"),
        ["slategray"] = FromHex("#708090"), ["slategrey"] = FromHex("#708090"), ["snow"] = FromHex("#FFFAFA"),
        ["springgreen"] = FromHex("#00FF7F"), ["steelblue"] = FromHex("#4682B4"), ["tan"] = FromHex("#D2B48C"),
        ["teal"] = FromHex("#008080"), ["thistle"] = FromHex("#D8BFD8"), ["tomato"] = FromHex("#FF6347"),
        ["turquoise"] = FromHex("#40E0D0"), ["violet"] = FromHex("#EE82EE"), ["wheat"] = FromHex("#F5DEB3"),
        ["white"] = FromHex("#FFFFFF"), ["whitesmoke"] = FromHex("#F5F5F5"), ["yellow"] = FromHex("#FFFF00"),
        ["yellowgreen"] = FromHex("#9ACD32"),
    };
}
