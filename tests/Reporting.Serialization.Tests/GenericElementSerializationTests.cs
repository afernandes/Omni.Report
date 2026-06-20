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
/// A test-only element that is NOT wired into any of the serializer switches. It exercises the
/// convention-based auto-wiring fallback: a new all-scalar <see cref="ReportElement"/> must round-trip
/// through both repx and repjson with zero switch edits — that's the whole point of the registry.
/// (See docs/serialization-auto-wiring-design.md.)
/// </summary>
public sealed record ProbeElement : ReportElement
{
    public required string Caption { get; init; }
    public int Count { get; init; }
    public bool Flag { get; init; }
    public double Ratio { get; init; }
    public Unit Inset { get; init; }
    public Color? Tint { get; init; }
    public HorizontalAlignment Align { get; init; }
}

public class GenericElementSerializationTests
{
    // Both serializers — constructed directly (RepxSerializer has no true parameterless ctor).
    public static TheoryData<IReportSerializer> Serializers() =>
        new() { new RepxSerializer(), new RepJsonSerializer() };

    [Theory]
    [MemberData(nameof(Serializers))]
    public void A_new_all_scalar_element_round_trips_with_zero_switch_edits(IReportSerializer serializer)
    {
        var probe = new ProbeElement
        {
            Id = "probe-1",
            Bounds = new Rectangle(Unit.FromMm(1), Unit.FromMm(2), Unit.FromMm(30), Unit.FromMm(8)),
            Caption = "olá mundo",
            Count = 7,
            Flag = true,
            Ratio = 1.5,
            Inset = Unit.FromMm(3),
            Tint = Color.FromHex("#112233"),
            Align = HorizontalAlignment.Center,
        };
        var def = new ReportDefinition("rt", PageSetup.A4Portrait,
            new DetailBand(Unit.FromMm(40), new EquatableArray<ReportElement>(new ReportElement[] { probe })));

        var loaded = serializer.LoadFromBytes(serializer.SaveToBytes(def));
        var back = loaded.Detail.Elements.Single();

        back.Should().BeOfType<ProbeElement>();
        back.Should().Be(probe,
            $"{serializer.GetType().Name} round-trips a new convention-based element without editing any switch");
    }

    [Theory]
    [MemberData(nameof(Serializers))]
    public void A_new_element_omits_default_scalars_but_keeps_required(IReportSerializer serializer)
    {
        // Everything except the required Caption left at its default → sparse: only Caption is emitted,
        // and the defaults round-trip back identically.
        var probe = new ProbeElement
        {
            Id = "probe-2",
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(10), Unit.FromMm(5)),
            Caption = "only required",
        };
        var def = new ReportDefinition("rt", PageSetup.A4Portrait,
            new DetailBand(Unit.FromMm(40), new EquatableArray<ReportElement>(new ReportElement[] { probe })));

        var back = serializer.LoadFromBytes(serializer.SaveToBytes(def)).Detail.Elements.Single();

        back.Should().Be(probe);
        ((ProbeElement)back).Tint.Should().BeNull("a null Color? default is omitted and read back as null");
    }
}
