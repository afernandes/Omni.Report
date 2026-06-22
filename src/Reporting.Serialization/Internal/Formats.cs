using System.Globalization;
using Reporting.Geometry;
using Reporting.Styling;

namespace Reporting.Serialization.Internal;

/// <summary>Parsers and writers for the small "leaf" types used by both XML and JSON
/// serializers. Always uses <see cref="CultureInfo.InvariantCulture"/> — format files
/// must be culture-independent.</summary>
internal static class Formats
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    // ── Unit (stored as integer mils for lossless round-trip) ──────────────────

    public static string FormatUnit(Unit unit) => unit.Mils.ToString(Culture);

    public static Unit ParseUnit(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Unit.Zero;
        }
        // Accept both bare mils ("394") and "Nmm" / "Nin" / "Npt" suffixes.
        var trimmed = text.Trim();
        if (trimmed.EndsWith("mm", StringComparison.OrdinalIgnoreCase))
        {
            return Unit.FromMm(double.Parse(trimmed[..^2], Culture));
        }
        if (trimmed.EndsWith("in", StringComparison.OrdinalIgnoreCase))
        {
            return Unit.FromInch(double.Parse(trimmed[..^2], Culture));
        }
        if (trimmed.EndsWith("pt", StringComparison.OrdinalIgnoreCase))
        {
            return Unit.FromPoint(double.Parse(trimmed[..^2], Culture));
        }
        return new Unit(int.Parse(trimmed, Culture));
    }

    public static string FormatRectangle(Rectangle r) =>
        string.Create(Culture, $"{r.X.Mils},{r.Y.Mils},{r.Width.Mils},{r.Height.Mils}");

    public static Rectangle ParseRectangle(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Rectangle.Empty;
        }
        var parts = text.Split(',');
        if (parts.Length != 4)
        {
            throw new FormatException($"Rectangle must be 'X,Y,W,H' in mils; got '{text}'.");
        }
        return new Rectangle(
            new Unit(int.Parse(parts[0], Culture)),
            new Unit(int.Parse(parts[1], Culture)),
            new Unit(int.Parse(parts[2], Culture)),
            new Unit(int.Parse(parts[3], Culture)));
    }

    public static string FormatThickness(Thickness t) =>
        string.Create(Culture, $"{t.Left.Mils},{t.Top.Mils},{t.Right.Mils},{t.Bottom.Mils}");

    public static Thickness ParseThickness(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Thickness.Zero;
        }
        var parts = text.Split(',');
        if (parts.Length != 4)
        {
            throw new FormatException($"Thickness must be 'L,T,R,B' in mils; got '{text}'.");
        }
        return new Thickness(
            new Unit(int.Parse(parts[0], Culture)),
            new Unit(int.Parse(parts[1], Culture)),
            new Unit(int.Parse(parts[2], Culture)),
            new Unit(int.Parse(parts[3], Culture)));
    }

    public static string FormatColor(Color c) => c.ToHex();
    public static Color ParseColor(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Color.Transparent;
        }
        text = text.Trim();
        // Writer always emits #hex, but a hand-authored repx/repjson may carry a CSS/RDL colour name — accept
        // both so the readers agree with the RDL importer and expression-binding coercion (Color.FromName).
        return text.StartsWith('#') ? Color.FromHex(text) : Color.FromName(text) ?? Color.FromHex(text);
    }

    public static string FormatType(Type type) => type.AssemblyQualifiedName ?? type.FullName ?? type.Name;
    public static Type ParseType(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return typeof(object);
        }
        // Prefer the simple FullName lookup before falling back to assembly-qualified.
        var t = Type.GetType(text, throwOnError: false);
        if (t is not null)
        {
            return t;
        }
        // Try common BCL types by simple name (DateTime, Decimal, String, Int32, …).
        return text switch
        {
            "System.DateTime" or "DateTime" => typeof(DateTime),
            "System.String" or "String" => typeof(string),
            "System.Int32" or "Int32" => typeof(int),
            "System.Int64" or "Int64" => typeof(long),
            "System.Decimal" or "Decimal" => typeof(decimal),
            "System.Double" or "Double" => typeof(double),
            "System.Boolean" or "Boolean" => typeof(bool),
            "System.Guid" or "Guid" => typeof(Guid),
            _ => throw new InvalidOperationException($"Unknown type '{text}'."),
        };
    }
}
