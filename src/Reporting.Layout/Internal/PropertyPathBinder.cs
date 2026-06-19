using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Styling;

namespace Reporting.Layout.Internal;

/// <summary>
/// Applies a per-property expression result onto an immutable <see cref="ReportElement"/> by a dotted
/// property <b>path</b> (e.g. <c>"Style.Font.Size"</c>). Navigates the path by reflection, coerces the
/// raw value to the leaf property's type, and rebuilds the record chain bottom-up via each record's
/// synthesized clone (<c>with</c>) — so the original element is never mutated. Plans are cached per
/// <c>(type, path)</c>. On any failure (unknown path, uncoercible value, null record on the way) it
/// returns the input unchanged, so the property's static value remains the graceful fallback.
/// </summary>
internal static class PropertyPathBinder
{
    private sealed record Plan(PropertyInfo[] Chain, MethodInfo[] Clones, Type LeafType);

    private static readonly ConcurrentDictionary<(Type Type, string Path), Plan?> Cache = new();

    public static ReportElement Apply(ReportElement element, string path, object? raw, CultureInfo culture)
    {
        var plan = Cache.GetOrAdd((element.GetType(), path), k => BuildPlan(k.Type, k.Path));
        if (plan is null || !TryCoerce(raw, plan.LeafType, culture, out var value))
        {
            return element;
        }
        return (ReportElement)SetRecursive(element, plan, 0, value)!;
    }

    private static Plan? BuildPlan(Type rootType, string path)
    {
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            return null;
        }
        var chain = new PropertyInfo[segments.Length];
        var clones = new MethodInfo[segments.Length];
        var cur = rootType;
        for (int i = 0; i < segments.Length; i++)
        {
            var p = cur.GetProperty(segments[i], BindingFlags.Public | BindingFlags.Instance);
            var clone = cur.GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p is null || clone is null) // unknown segment, or a non-record on the path → no plan
            {
                return null;
            }
            chain[i] = p;
            clones[i] = clone;
            cur = p.PropertyType;
        }
        return new Plan(chain, clones, chain[^1].PropertyType);
    }

    private static object? SetRecursive(object obj, Plan plan, int i, object? leafValue)
    {
        var copy = plan.Clones[i].Invoke(obj, null)!; // record `with`-clone of this level
        if (i == plan.Chain.Length - 1)
        {
            plan.Chain[i].SetValue(copy, leafValue); // init-only set is allowed via reflection on the copy
            return copy;
        }
        var child = plan.Chain[i].GetValue(obj);
        if (child is null)
        {
            return obj; // can't navigate into a null nested record
        }
        plan.Chain[i].SetValue(copy, SetRecursive(child, plan, i + 1, leafValue));
        return copy;
    }

    /// <summary>Coerces an expression result to the target property type. Handles the domain types
    /// <see cref="Color"/> (from a <c>#hex</c> string) and <see cref="Unit"/> (a bare number = mm), any
    /// enum (by name or number, case-insensitive), and everything else via <see cref="Convert.ChangeType(object?, Type, IFormatProvider?)"/>.
    /// Returns false (caller skips, keeping the static value) on any failure.</summary>
    private static bool TryCoerce(object? raw, Type target, CultureInfo culture, out object? value)
    {
        value = null;
        var u = Nullable.GetUnderlyingType(target) ?? target;
        if (raw is null)
        {
            return Nullable.GetUnderlyingType(target) is not null || !target.IsValueType;
        }
        if (u.IsInstanceOfType(raw))
        {
            value = raw;
            return true;
        }
        try
        {
            if (u == typeof(Color))
            {
                value = Color.FromHex(Convert.ToString(raw, culture)!);
                return true;
            }
            if (u == typeof(Unit))
            {
                var s = Convert.ToString(raw, culture);
                if (double.TryParse(s, NumberStyles.Any, culture, out var mm)
                    || double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out mm))
                {
                    value = Unit.FromMm(mm);
                    return true;
                }
                return false;
            }
            if (u.IsEnum)
            {
                value = Enum.Parse(u, Convert.ToString(raw, culture)!, ignoreCase: true);
                return true;
            }
            value = Convert.ChangeType(raw, u, culture);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException or ArgumentException)
        {
            return false;
        }
    }
}
