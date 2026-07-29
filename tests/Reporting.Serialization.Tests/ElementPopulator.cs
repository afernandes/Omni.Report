using System.Collections;
using System.Reflection;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Styling;

namespace Reporting.Serialization.Tests;

/// <summary>
/// Builds a <see cref="ReportElement"/> with EVERY settable property set to a value distinct from its
/// default, so that a property dropped by a serializer surfaces as a round-trip mismatch. Shared by the
/// reflection-driven parity nets: <see cref="ReflectionRoundTripTests"/> (native .repx/.repjson, which must
/// be lossless) and <see cref="RdlReflectionParityTests"/> (RDL, a projection with a documented lossy set).
/// </summary>
internal static class ElementPopulator
{
    /// <summary>Properties no reflection net exercises (covered elsewhere / not a flat value):
    /// <c>InlineDefinition</c> is a whole nested ReportDefinition (own round-trip tests) and
    /// <c>Action</c> is a discriminated record where only the fields matching its Kind round-trip.</summary>
    internal static readonly HashSet<string> Excluded = new() { "InlineDefinition", "Action" };

    /// <summary>Every concrete <see cref="ReportElement"/> subtype in the model assembly, name-ordered.</summary>
    internal static IEnumerable<Type> ConcreteElementTypes()
        => typeof(ReportElement).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(ReportElement)) && !t.IsAbstract)
            .OrderBy(t => t.Name);

    /// <summary>Builds an instance of <paramref name="t"/> with every settable property set to a value
    /// distinct from its default.</summary>
    internal static object Populate(Type t, int depth)
    {
        object obj;
        var paramless = t.GetConstructor(Type.EmptyTypes);
        if (paramless is not null)
        {
            obj = paramless.Invoke(null);
        }
        else
        {
            var ctor = t.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
            var args = ctor.GetParameters()
                .Select(p => GenValue(p.ParameterType, Default(p.ParameterType), p.Name!, depth + 1))
                .ToArray();
            obj = ctor.Invoke(args);
        }

        foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.SetMethod is null || Excluded.Contains(prop.Name))
            {
                continue;
            }
            var value = GenValue(prop.PropertyType, prop.GetValue(obj), prop.Name, depth + 1);
            if (value is not null)
            {
                try { prop.SetValue(obj, value); }
                catch (Exception ex) when (ex is ArgumentException or TargetInvocationException or MethodAccessException)
                {
                    // not settable in practice (init-only via reflection quirk, validation in the setter) —
                    // leave the default; the property simply isn't exercised for this type.
                }
            }
        }
        return obj;
    }

    private static object? Default(Type t) => t.IsValueType ? Activator.CreateInstance(t) : null;

    /// <summary>A value for <paramref name="type"/> distinct from <paramref name="current"/> default.</summary>
    internal static object? GenValue(Type type, object? current, string name, int depth)
    {
        if (depth > 5)
        {
            return null; // bound recursion (e.g. nested element → cells → element …)
        }
        var u = Nullable.GetUnderlyingType(type) ?? type;

        if (u == typeof(string)) return "rt-" + name;
        if (u == typeof(bool)) return !(current as bool? ?? false);
        if (u == typeof(Color)) return Color.FromArgb(200, 10, 90, 170); // distinct + non-opaque (also tests alpha)
        if (u == typeof(Unit)) return Unit.FromMm(7);
        if (u.IsEnum)
        {
            foreach (var v in Enum.GetValues(u))
            {
                if (!Equals(v, current)) return v;
            }
            return current;
        }
        if (u == typeof(int) || u == typeof(long) || u == typeof(short) || u == typeof(byte)
            || u == typeof(sbyte) || u == typeof(uint) || u == typeof(ulong) || u == typeof(ushort))
        {
            return Convert.ChangeType(7, u);
        }
        if (u == typeof(double) || u == typeof(float) || u == typeof(decimal))
        {
            return Convert.ChangeType(7.5, u);
        }

        if (u.IsGenericType && u.GetGenericTypeDefinition() == typeof(EquatableArray<>))
        {
            var itemType = u.GetGenericArguments()[0];
            var arr = Array.CreateInstance(itemType, 1);
            arr.SetValue(itemType == typeof(byte) ? (byte)42 : GenValue(itemType, null, name + "Item", depth + 1), 0);
            return Activator.CreateInstance(u, arr);
        }
        if (u.IsGenericType && u.GetGenericTypeDefinition() == typeof(EquatableDictionary<,>))
        {
            var args = u.GetGenericArguments();
            var dict = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(args))!;
            dict[GenValue(args[0], null, "k", depth + 1)!] = GenValue(args[1], null, "v", depth + 1);
            return Activator.CreateInstance(u, dict);
        }

        if (u == typeof(ReportDefinition))
        {
            return null; // InlineDefinition handled by exclusion; don't synthesise a whole report
        }
        if (u == typeof(ReportElement) || u.IsSubclassOf(typeof(ReportElement)))
        {
            // A nested element slot (e.g. a Tablix cell's content) — a simple leaf to bound recursion.
            return new TextBoxElement { Id = "rt-cell", Expression = "x", Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(10), Unit.FromMm(5)) };
        }
        // Any other record (Style, Font, Border, ChartSeries, GaugeRange, TablixGroup, TablixCell, …).
        if (!u.IsPrimitive && (u.IsClass || u.IsValueType) && u != typeof(object))
        {
            return Populate(u, depth + 1);
        }
        return current;
    }
}
