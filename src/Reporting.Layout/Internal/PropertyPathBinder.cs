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
    // One level of the path. The owner at this level is rebuilt immutably with <see cref="Prop"/> changed:
    // a record CLASS via its synthesized <c>&lt;Clone&gt;$</c> + init-only set; a record STRUCT (Rectangle,
    // Thickness, …) — which has no <c>&lt;Clone&gt;$</c> — by re-invoking its positional constructor with
    // every component copied except the one being navigated. That makes struct-segment paths such as
    // <c>"Bounds.Width"</c> and <c>"Style.Padding.Left"</c> actually bind instead of silently no-op'ing.
    private sealed record Level(PropertyInfo Prop, MethodInfo? CloneFn, ConstructorInfo? Ctor, PropertyInfo[]? CtorProps);

    private sealed record Plan(Level[] Levels, Type LeafType);

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
        var levels = new Level[segments.Length];
        var cur = rootType;
        for (int i = 0; i < segments.Length; i++)
        {
            // Navigate through a Nullable<T> intermediate by its underlying type (e.g. Style.Padding is Thickness?).
            var owner = Nullable.GetUnderlyingType(cur) ?? cur;
            var p = owner.GetProperty(segments[i], BindingFlags.Public | BindingFlags.Instance);
            if (p is null)
            {
                return null; // unknown segment
            }
            var clone = owner.GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (clone is not null)
            {
                levels[i] = new Level(p, clone, null, null); // record class
            }
            else if (owner.IsValueType)
            {
                // record struct → rebuild via its positional constructor (ctor param name = property name).
                var ctor = owner.GetConstructors()
                    .OrderByDescending(c => c.GetParameters().Length)
                    .FirstOrDefault(c => c.GetParameters().Length > 0);
                var props = ctor?.GetParameters()
                    .Select(par => owner.GetProperty(par.Name!, BindingFlags.Public | BindingFlags.Instance))
                    .ToArray();
                if (ctor is null || props is null || Array.IndexOf(props, null) >= 0)
                {
                    return null; // not a positional record struct we can reconstruct
                }
                levels[i] = new Level(p, null, ctor, props!);
            }
            else
            {
                return null; // neither a record class nor a reconstructible value type
            }
            cur = p.PropertyType;
        }
        return new Plan(levels, levels[^1].Prop.PropertyType);
    }

    private static object? SetRecursive(object obj, Plan plan, int i, object? leafValue)
    {
        var level = plan.Levels[i];
        object? newComponent;
        if (i == plan.Levels.Length - 1)
        {
            newComponent = leafValue;
        }
        else
        {
            var child = level.Prop.GetValue(obj);
            if (child is null)
            {
                return obj; // can't navigate into an absent nested record/struct
            }
            newComponent = SetRecursive(child, plan, i + 1, leafValue);
        }
        if (level.CloneFn is not null)
        {
            var copy = level.CloneFn.Invoke(obj, null)!; // record `with`-clone of this level
            level.Prop.SetValue(copy, newComponent); // init-only set is allowed via reflection on the copy
            return copy;
        }
        // record struct: rebuild via its positional ctor, swapping only the navigated component.
        var args = new object?[level.CtorProps!.Length];
        for (int j = 0; j < args.Length; j++)
        {
            args[j] = level.CtorProps[j].Name == level.Prop.Name ? newComponent : level.CtorProps[j].GetValue(obj);
        }
        return level.Ctor!.Invoke(args);
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
                // A colour result is a STRING: a #hex literal, or a known CSS/RDL name (e.g. the ubiquitous
                // negative-in-red expression =IIf(x<0,"Red","Black")). A numeric result (e.g. 160000) must
                // NOT be mis-read as "#160000" — reject it so the static colour stays the fallback.
                if (raw is not string colorText)
                {
                    return false;
                }
                if (colorText.TrimStart().StartsWith('#'))
                {
                    value = Color.FromHex(colorText.Trim());
                    return true;
                }
                if (Color.FromName(colorText) is { } named)
                {
                    value = named;
                    return true;
                }
                return false;
            }
            if (u == typeof(Unit))
            {
                // A bare number = millimetres. Try invariant FIRST (an expression result typically uses a
                // '.' decimal) and WITHOUT AllowThousands — NumberStyles.Any would read "2.5" under pt-BR
                // as the grouping separator (2.5 → 25), a silent 10× corruption.
                var s = Convert.ToString(raw, culture);
                const NumberStyles styles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint
                    | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite;
                if (double.TryParse(s, styles, CultureInfo.InvariantCulture, out var mm)
                    || double.TryParse(s, styles, culture, out mm))
                {
                    value = Unit.FromMm(mm);
                    return true;
                }
                return false;
            }
            if (u.IsEnum)
            {
                var text = Convert.ToString(raw, culture)!;
                var isFlags = u.IsDefined(typeof(FlagsAttribute), inherit: false);
                if (!isFlags && text.Contains(','))
                {
                    return false; // a comma-list is only valid for a [Flags] enum, not a single-value one
                }
                var parsed = Enum.Parse(u, text, ignoreCase: true);
                if (isFlags)
                {
                    // [Flags] (e.g. FontStyle): accept any combination of defined bits, reject the rest.
                    long mask = 0;
                    foreach (var v in Enum.GetValues(u))
                    {
                        mask |= Convert.ToInt64(v, culture);
                    }
                    if ((Convert.ToInt64(parsed, culture) & ~mask) != 0)
                    {
                        return false;
                    }
                }
                else if (!Enum.IsDefined(u, parsed))
                {
                    return false; // an out-of-range number (e.g. "99") is a coercion failure → keep the static value
                }
                value = parsed;
                return true;
            }
            if (u == typeof(double) || u == typeof(float) || u == typeof(decimal)
                || u == typeof(int) || u == typeof(long) || u == typeof(short) || u == typeof(byte)
                || u == typeof(sbyte) || u == typeof(uint) || u == typeof(ulong) || u == typeof(ushort))
            {
                // A STRING numeric result must parse invariant-first, no thousands — the same pt-BR
                // "2.5"→25 (10×) trap that Unit had. A boxed numeric value is converted invariantly too.
                if (raw is string num)
                {
                    const NumberStyles ns = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint
                        | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite | NumberStyles.AllowExponent;
                    if (!double.TryParse(num, ns, CultureInfo.InvariantCulture, out var d)
                        && !double.TryParse(num, ns, culture, out d))
                    {
                        return false;
                    }
                    value = Convert.ChangeType(d, u, CultureInfo.InvariantCulture);
                    return true;
                }
                value = Convert.ChangeType(raw, u, CultureInfo.InvariantCulture);
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
