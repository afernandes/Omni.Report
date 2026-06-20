using System.Collections;
using System.Reflection;
using FluentAssertions;
using Reporting;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Paper;
using Reporting.Serialization;
using Reporting.Styling;
using Xunit;

namespace Reporting.Serialization.Tests;

/// <summary>
/// Reflection-driven SAFETY NET for serializer parity: for EVERY concrete <see cref="ReportElement"/>
/// subtype, populate every settable property with a non-default value, round-trip through both formats,
/// and assert each property survives. This catches the whole class of "added a property / element but
/// forgot a serializer switch" bug (e.g. the QrEcc gap) — which manual fixtures miss by construction.
/// Adding a new component therefore can't silently lose a property: this test fails until it serializes.
/// </summary>
public class ReflectionRoundTripTests
{
    // Properties intentionally not exercised here (covered elsewhere / not a flat value):
    //  - InlineDefinition: a whole nested ReportDefinition, alternative to ReportId (own round-trip tests).
    //  - Action: a discriminated record (only the fields matching its Kind round-trip) — own tests.
    private static readonly HashSet<string> Excluded = new() { "InlineDefinition", "Action" };

    public static TheoryData<Type> ElementTypes()
    {
        var data = new TheoryData<Type>();
        foreach (var t in typeof(ReportElement).Assembly.GetTypes()
                     .Where(t => t.IsSubclassOf(typeof(ReportElement)) && !t.IsAbstract)
                     .OrderBy(t => t.Name))
        {
            data.Add(t);
        }
        return data;
    }

    [Fact]
    public void Covers_every_concrete_element_type()
    {
        // Guards the guard: if reflection silently stops discovering types, the [Theory] would run zero
        // cases and "pass". There are 18 concrete ReportElement subtypes today; never fewer than 17.
        ((IEnumerable<object[]>)ElementTypes()).Count()
            .Should().BeGreaterThanOrEqualTo(17, "the safety net must cover every concrete ReportElement subtype");
    }

    [Theory]
    [MemberData(nameof(ElementTypes))]
    public void Every_property_of_every_element_round_trips(Type elementType)
    {
        var element = (ReportElement)Populate(elementType, 0);
        var def = new ReportDefinition("rt", PageSetup.A4Portrait,
            new DetailBand(Unit.FromMm(40), new EquatableArray<ReportElement>(new[] { element })));

        foreach (var serializer in new IReportSerializer[] { new RepxSerializer(), new RepJsonSerializer() })
        {
            var loaded = serializer.LoadFromBytes(serializer.SaveToBytes(def));
            var back = loaded.Detail.Elements.Single();
            back.GetType().Should().Be(elementType, $"{serializer.GetType().Name} must not degrade the element type");

            var mismatches = new List<string>();
            foreach (var prop in elementType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (Excluded.Contains(prop.Name) || prop.GetMethod is null)
                {
                    continue;
                }
                var expected = prop.GetValue(element);
                var actual = prop.GetValue(back);
                if (!Equals(expected, actual))
                {
                    mismatches.Add($"{prop.Name} ({prop.PropertyType.Name}): expected [{expected}] but got [{actual}]");
                }
            }

            mismatches.Should().BeEmpty(
                $"{elementType.Name} must round-trip every property through {serializer.GetType().Name} — a mismatch means a missing serializer switch");
        }
    }

    /// <summary>Builds an instance of <paramref name="t"/> with every settable property set to a value
    /// distinct from its default (so a dropped property surfaces as a round-trip mismatch).</summary>
    private static object Populate(Type t, int depth)
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
                catch { /* not settable in practice — leave the default */ }
            }
        }
        return obj;
    }

    private static object? Default(Type t) => t.IsValueType ? Activator.CreateInstance(t) : null;

    /// <summary>A value for <paramref name="type"/> distinct from <paramref name="current"/> default.</summary>
    private static object? GenValue(Type type, object? current, string name, int depth)
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
