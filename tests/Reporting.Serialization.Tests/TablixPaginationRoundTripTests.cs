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

/// <summary>Round-trip of the matrix pagination flags — <c>TablixElement.RepeatColumnHeaders</c> (default true)
/// and <c>KeepTogether</c> (default false) — across .repx and .repjson, including the non-default opt-outs.</summary>
public class TablixPaginationRoundTripTests
{
    private static ReportDefinition WithTablix(bool repeat, bool keep, Unit minCol = default)
    {
        var tablix = new TablixElement
        {
            Id = "tx",
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(100), Unit.FromMm(40)),
            DataSetName = "D",
            RowGroups = EquatableArray.Create(new TablixGroup("R", "Fields.A")),
            ColumnGroups = EquatableArray.Create(new TablixGroup("C", "Fields.B")),
            Cells = EquatableArray.Create(
                new TablixCell(0, 0, new LabelElement { Text = "x", Bounds = Rectangle.Empty }),
                new TablixCell(1, 1, new TextBoxElement { Expression = "Fields.V", Bounds = Rectangle.Empty })),
            RepeatColumnHeaders = repeat,
            KeepTogether = keep,
            MinColumnWidth = minCol,
        };
        var detail = new DetailBand(Unit.FromMm(6), EquatableArray.Create<ReportElement>(tablix));
        return new ReportDefinition("T", PageSetup.A4Portrait, detail);
    }

    [Fact]
    public void Repx_preserves_the_non_default_flags()
    {
        var def = WithTablix(repeat: false, keep: true);
        var s = new RepxSerializer();
        s.LoadFromBytes(s.SaveToBytes(def)).Should().Be(def);
    }

    [Fact]
    public void Repjson_preserves_the_non_default_flags()
    {
        var def = WithTablix(repeat: false, keep: true);
        var s = new RepJsonSerializer();
        s.LoadFromBytes(s.SaveToBytes(def)).Should().Be(def);
    }

    [Fact]
    public void Defaults_round_trip_unchanged()
    {
        var def = WithTablix(repeat: true, keep: false); // the defaults
        new RepxSerializer().LoadFromBytes(new RepxSerializer().SaveToBytes(def)).Should().Be(def);
        new RepJsonSerializer().LoadFromBytes(new RepJsonSerializer().SaveToBytes(def)).Should().Be(def);
    }

    [Fact]
    public void MinColumnWidth_round_trips_through_both_formats()
    {
        var def = WithTablix(repeat: true, keep: false, minCol: Unit.FromMm(25)); // opt-in to horizontal column tiling
        new RepxSerializer().LoadFromBytes(new RepxSerializer().SaveToBytes(def)).Should().Be(def);
        new RepJsonSerializer().LoadFromBytes(new RepJsonSerializer().SaveToBytes(def)).Should().Be(def);
    }
}
