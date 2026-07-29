using System.Reflection;
using FluentAssertions;
using Reporting;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Paper;
using Reporting.Serialization;
using Xunit;

namespace Reporting.Serialization.Tests;

/// <summary>
/// Reflection-driven SAFETY NET for serializer parity: for EVERY concrete <see cref="ReportElement"/>
/// subtype, populate every settable property with a non-default value, round-trip through both NATIVE
/// formats, and assert each property survives. This catches the whole class of "added a property / element
/// but forgot a serializer switch" bug (e.g. the QrEcc gap) — which manual fixtures miss by construction.
/// Adding a new component therefore can't silently lose a property: this test fails until it serializes.
/// <para>The native formats must be LOSSLESS. The RDL projection has its own net, with a documented set of
/// properties RDL cannot carry — see <see cref="RdlReflectionParityTests"/>.</para>
/// </summary>
public class ReflectionRoundTripTests
{
    public static TheoryData<Type> ElementTypes()
    {
        var data = new TheoryData<Type>();
        foreach (var t in ElementPopulator.ConcreteElementTypes())
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
        var element = (ReportElement)ElementPopulator.Populate(elementType, 0);
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
                if (ElementPopulator.Excluded.Contains(prop.Name) || prop.GetMethod is null)
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
}
