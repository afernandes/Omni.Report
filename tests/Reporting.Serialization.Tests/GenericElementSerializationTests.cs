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

/// <summary>A POSITIONAL record used as a nested value — exercises constructor-parameter matching.</summary>
public sealed record ProbeBand(string Label, int Order, Color Tint);

/// <summary>A test-only element with collections (of scalar and of a positional record) and a nested
/// nullable record — exercises the recursive value (de)serialization, also with zero switch edits.</summary>
public sealed record ProbeChartElement : ReportElement
{
    public required string Title { get; init; }
    public EquatableArray<ProbeBand> Bands { get; init; } = EquatableArray<ProbeBand>.Empty;
    public ProbeBand? Highlight { get; init; }
    public EquatableArray<string> Tags { get; init; } = EquatableArray<string>.Empty;
}

/// <summary>A new element with an UNSUPPORTED member shape (a dictionary). The generic path must reject it
/// loudly rather than silently drop the data — EquatableDictionary's members are all get-only, which would
/// otherwise pass the "record" branch vacuously.</summary>
public sealed record ProbeDictElement : ReportElement
{
    public EquatableDictionary<string, string> Meta { get; init; } = EquatableDictionary<string, string>.Empty;
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

    [Theory]
    [MemberData(nameof(Serializers))]
    public void A_new_element_with_collections_and_nested_records_round_trips(IReportSerializer serializer)
    {
        var probe = new ProbeChartElement
        {
            Id = "pc-1",
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(50), Unit.FromMm(30)),
            Title = "Vendas",
            Bands = new EquatableArray<ProbeBand>(new[]
            {
                new ProbeBand("baixo", 1, Color.FromHex("#FF0000")),
                new ProbeBand("alto", 2, Color.FromHex("#00FF00")),
            }),
            Highlight = new ProbeBand("destaque", 9, Color.FromHex("#0000FF")),
            Tags = new EquatableArray<string>(new[] { "a", "b", "c" }),
        };
        var def = new ReportDefinition("rt", PageSetup.A4Portrait,
            new DetailBand(Unit.FromMm(40), new EquatableArray<ReportElement>(new ReportElement[] { probe })));

        var back = serializer.LoadFromBytes(serializer.SaveToBytes(def)).Detail.Elements.Single();

        back.Should().BeOfType<ProbeChartElement>();
        back.Should().Be(probe,
            $"{serializer.GetType().Name} round-trips collections + nested positional records generically");
    }

    [Theory]
    [MemberData(nameof(Serializers))]
    public void A_new_element_with_an_unsupported_member_fails_loudly(IReportSerializer serializer)
    {
        var def = new ReportDefinition("rt", PageSetup.A4Portrait,
            new DetailBand(Unit.FromMm(40),
                new EquatableArray<ReportElement>(new ReportElement[] { new ProbeDictElement() })));

        var act = () => serializer.SaveToBytes(def);

        act.Should().Throw<InvalidOperationException>(
            "a dictionary member is not auto-serializable — the generic path must fail, not silently drop it");
    }
}
