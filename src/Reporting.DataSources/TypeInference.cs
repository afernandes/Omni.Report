using System.Globalization;

namespace Reporting.DataSources;

/// <summary>
/// Heuristic-driven CLR type inference for raw string values produced by text-based
/// data providers (JSON, XML, REST, CSV). The four MyFyi-style providers (JSON, XML,
/// WebService, FileSystem) all face the same problem: JSON-string "12.5" should become
/// <see cref="double"/>, "2024-12-01" should become <see cref="DateTime"/>, "true"
/// should become <see cref="bool"/>, etc. Centralising the logic here keeps every
/// provider doing the same coercion — preventing the situation where the same .repx
/// renders differently depending on which provider supplied the data.
/// </summary>
/// <remarks>
/// <para><b>Inference order</b> (cheapest first): bool → integer → decimal → DateTime →
/// fallback string. The first parser that accepts the value wins. Empty strings stay
/// as null. The DateTime parser tries <see cref="DateTimeStyles.RoundtripKind"/> first
/// (ISO-8601), then falls back to <see cref="DateTimeStyles.AssumeLocal"/>.</para>
///
/// <para><b>Schema inference</b> aggregates per-column types across multiple rows:
/// a column whose first row is "1" but whose second row is "foo" must be typed as
/// string. <see cref="WidenType"/> merges two candidate types into the narrower common
/// type that holds both values — e.g. <c>(int, double)</c> → <c>double</c>,
/// <c>(int, string)</c> → <c>string</c>, <c>(int, null)</c> → <c>int?</c>. We always
/// fall through to <see cref="string"/> rather than throwing — a wrong-looking
/// expression should still render in production.</para>
/// </remarks>
public static class TypeInference
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Tries to infer the CLR value from a raw string. Returns the parsed
    /// value and the CLR type that "best fits" it. Empty / null input produces
    /// <c>(null, typeof(string))</c> — typed as string so widening doesn't lose
    /// information when the column has at least one real value.</summary>
    public static (object? Value, Type Type) Coerce(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return (null, typeof(string));
        // bool wins first — "true"/"false" would otherwise parse as nothing.
        if (bool.TryParse(raw, out var b)) return (b, typeof(bool));
        // Preserve strings that LOOK numeric but encode an opaque identifier — zero-prefixed
        // numbers (ZIP codes "01000", phone "012345"), strings starting with '+', etc. are
        // domain-specific identifiers where dropping the leading character changes the
        // meaning. A bare "0" is fine (canonical zero), but "00" or "01000" must stay
        // string. Real numbers never have a non-significant leading zero in JSON/XML output.
        var isOpaqueId = raw.Length > 1
            && (raw[0] == '0' || raw[0] == '+')
            && raw[1] >= '0' && raw[1] <= '9';
        if (isOpaqueId) return (raw, typeof(string));
        // Integer before decimal — "12" is int, "12.5" is double.
        if (long.TryParse(raw, NumberStyles.Integer, Inv, out var l))
        {
            // Keep small ints small so reports doing arithmetic don't get surprising overflow.
            if (l >= int.MinValue && l <= int.MaxValue) return ((int)l, typeof(int));
            return (l, typeof(long));
        }
        if (double.TryParse(raw, NumberStyles.Float, Inv, out var d)) return (d, typeof(double));
        // ISO-8601 first (round-trippable JSON/XML output); then locale-free general.
        if (DateTime.TryParse(raw, Inv, DateTimeStyles.RoundtripKind, out var dt)) return (dt, typeof(DateTime));
        if (DateTime.TryParse(raw, Inv, DateTimeStyles.AssumeLocal, out dt))     return (dt, typeof(DateTime));
        return (raw, typeof(string));
    }

    /// <summary>Merges two candidate column types into the narrowest type that can
    /// hold both. Null on either side just propagates the other side (with nullable
    /// flag implicit). Mismatched types always widen to <see cref="string"/>.</summary>
    /// <remarks>
    /// The lattice is intentionally small — bool / int / long / double / DateTime / string,
    /// in narrow-to-wide order. Anything else uses the existing type unchanged.
    /// </remarks>
    public static Type WidenType(Type a, Type b)
    {
        if (a == b) return a;
        if (a == typeof(string) || b == typeof(string)) return typeof(string);
        // Numeric promotion: int < long < double.
        if (a == typeof(int) && b == typeof(long))    return typeof(long);
        if (a == typeof(long) && b == typeof(int))    return typeof(long);
        if ((a == typeof(int) || a == typeof(long)) && b == typeof(double)) return typeof(double);
        if (a == typeof(double) && (b == typeof(int) || b == typeof(long))) return typeof(double);
        // Anything else: fall back to string. Crystal/SSRS do the same when type promotion
        // can't bridge the gap — the runtime then uses ToString() for display.
        return typeof(string);
    }

    /// <summary>Resolves the final declared type for a column whose per-row values
    /// produced the candidate set <paramref name="candidates"/>. Returns string when
    /// the set is empty (no widening happened).</summary>
    public static Type ConsolidateColumnType(IEnumerable<Type> candidates)
    {
        Type? running = null;
        foreach (var t in candidates)
        {
            running = running is null ? t : WidenType(running, t);
            // Short-circuit: once we widen to string nothing else changes it.
            if (running == typeof(string)) break;
        }
        return running ?? typeof(string);
    }
}
