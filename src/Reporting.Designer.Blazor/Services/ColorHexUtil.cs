using System.Globalization;
using Reporting.Styling;

namespace Reporting.Designer.Blazor.Services;

/// <summary>
/// Helpers for editing colours through an <c>&lt;input type=color&gt;</c>, which only round-trips
/// <c>#RRGGBB</c>. They strip the 8-digit <c>#AARRGGBB</c> form for display and re-attach the original
/// alpha on write, so a translucent colour is never silently forced opaque. Shared by EVERY colour editor
/// to keep them in parity — a past divergence (the metadata section got alpha-safe handling but the list
/// editor kept the raw form) dropped alpha only in the list editor.
/// </summary>
public static class ColorHexUtil
{
    /// <summary>A <see cref="Color"/> as <c>#RRGGBB</c> (drops alpha) — a valid colour-input value.</summary>
    public static string ToRgbHex(Color c) => string.Create(CultureInfo.InvariantCulture, $"#{c.R:X2}{c.G:X2}{c.B:X2}");

    /// <summary>A hex STRING reduced to <c>#RRGGBB</c> for the colour input (<c>#AARRGGBB → #RRGGBB</c>).</summary>
    public static string ToRgbHexString(string hex) => hex.Length == 9 ? "#" + hex[3..] : hex;

    /// <summary>The picked <c>#RRGGBB</c> re-merged with the original string's 8-digit alpha prefix, if any.</summary>
    public static string MergeHexAlpha(string oldHex, string? newRgb)
    {
        var rgb = string.IsNullOrEmpty(newRgb) ? "#000000" : newRgb;
        return oldHex.Length == 9 ? "#" + oldHex.Substring(1, 2) + rgb.TrimStart('#') : rgb;
    }

    /// <summary>The picked <c>#RRGGBB</c> as a <see cref="Color"/>, keeping the old colour's alpha channel.</summary>
    public static Color WithRgb(Color old, string? hex) => Color.FromHex(hex ?? "#000000") with { A = old.A };
}
