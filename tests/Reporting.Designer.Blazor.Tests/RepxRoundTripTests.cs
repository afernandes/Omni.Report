using FluentAssertions;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Geometry;
using Reporting.Styling;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// Exhaustive round-trip coverage for the .repx pipeline. The contract: anything visible /
/// editable in the designer must survive a Save → Load cycle byte-for-byte (modulo Unit
/// rounding tolerance of ±0.1 mm).
/// </summary>
public class RepxRoundTripTests
{
    [Theory]
    [InlineData(DesignerElementKind.Label)]
    [InlineData(DesignerElementKind.TextBox)]
    [InlineData(DesignerElementKind.Line)]
    [InlineData(DesignerElementKind.Rectangle)]
    [InlineData(DesignerElementKind.Ellipse)]
    [InlineData(DesignerElementKind.Image)]
    [InlineData(DesignerElementKind.Barcode)]
    public void Each_element_kind_round_trips_position_and_size(DesignerElementKind kind)
    {
        var state = new DesignerState();
        var detail = state.Report.FindBand(DesignerBandKind.Detail)!;
        detail.AddElement(new ElementViewModel(kind, "el1")
        {
            Text = "Hello", Expression = "{Fields.X}",
            X = Unit.FromMm(13), Y = Unit.FromMm(4),
            Width = Unit.FromMm(55), Height = Unit.FromMm(7),
            FillColor = (kind is DesignerElementKind.Rectangle or DesignerElementKind.Ellipse) ? Color.FromHex("#C2410C") : null,
        });

        var bytes = state.Save();
        var reloaded = new DesignerState();
        reloaded.Load(bytes);

        var els = reloaded.Report.FindBand(DesignerBandKind.Detail)!.Elements;
        els.Should().ContainSingle();
        var back = els[0];
        back.Kind.Should().Be(kind);
        back.X.ToMm().Should().BeApproximately(13, 0.2);
        back.Y.ToMm().Should().BeApproximately(4, 0.2);
        back.Width.ToMm().Should().BeApproximately(55, 0.2);
        back.Height.ToMm().Should().BeApproximately(7, 0.2);
    }

    [Fact]
    public void Font_style_round_trips_all_flags()
    {
        var state = new DesignerState();
        var detail = state.Report.FindBand(DesignerBandKind.Detail)!;
        detail.AddElement(new ElementViewModel(DesignerElementKind.Label, "f")
        {
            Text = "x", X = Unit.FromMm(5), Y = Unit.FromMm(1),
            Width = Unit.FromMm(40), Height = Unit.FromMm(6),
            IsBold = true, IsItalic = true, IsUnderline = true, IsStrikethrough = true,
            FontFamily = "Cascadia Mono", FontSize = 14,
        });
        var bytes = state.Save();
        var reloaded = new DesignerState();
        reloaded.Load(bytes);
        var back = reloaded.Report.FindBand(DesignerBandKind.Detail)!.Elements[0];
        back.IsBold.Should().BeTrue();
        back.IsItalic.Should().BeTrue();
        back.IsUnderline.Should().BeTrue();
        back.IsStrikethrough.Should().BeTrue();
        back.FontFamily.Should().Be("Cascadia Mono");
        back.FontSize.Should().Be(14);
    }

    [Fact]
    public void Alignment_and_wordwrap_round_trip()
    {
        var state = new DesignerState();
        var detail = state.Report.FindBand(DesignerBandKind.Detail)!;
        detail.AddElement(new ElementViewModel(DesignerElementKind.Label, "a")
        {
            Text = "x", X = Unit.FromMm(5), Y = Unit.FromMm(1),
            Width = Unit.FromMm(40), Height = Unit.FromMm(6),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Middle,
            WordWrap = false,
        });
        var bytes = state.Save();
        var reloaded = new DesignerState();
        reloaded.Load(bytes);
        var back = reloaded.Report.FindBand(DesignerBandKind.Detail)!.Elements[0];
        back.HorizontalAlignment.Should().Be(HorizontalAlignment.Right);
        back.VerticalAlignment.Should().Be(VerticalAlignment.Middle);
        back.WordWrap.Should().BeFalse();
    }

    [Fact]
    public void Colors_round_trip()
    {
        var state = new DesignerState();
        var detail = state.Report.FindBand(DesignerBandKind.Detail)!;
        detail.AddElement(new ElementViewModel(DesignerElementKind.Label, "c")
        {
            Text = "x", X = Unit.FromMm(5), Y = Unit.FromMm(1),
            Width = Unit.FromMm(40), Height = Unit.FromMm(6),
            ForeColor = Color.FromHex("#9A3209"),
            BackColor = Color.FromHex("#FFF1E6"),
        });
        var bytes = state.Save();
        var reloaded = new DesignerState();
        reloaded.Load(bytes);
        var back = reloaded.Report.FindBand(DesignerBandKind.Detail)!.Elements[0];
        back.ForeColor.ToHex().Should().Contain("9A3209");
        back.BackColor.Should().NotBeNull();
        back.BackColor!.Value.ToHex().Should().Contain("FFF1E6");
    }

    [Fact]
    public void IsVisible_false_round_trips()
    {
        var state = new DesignerState();
        var detail = state.Report.FindBand(DesignerBandKind.Detail)!;
        detail.AddElement(new ElementViewModel(DesignerElementKind.TextBox, "v")
        {
            Expression = "{Fields.Total}",
            X = Unit.FromMm(5), Y = Unit.FromMm(1),
            Width = Unit.FromMm(40), Height = Unit.FromMm(6),
            IsVisible = false,
        });
        var bytes = state.Save();
        var reloaded = new DesignerState();
        reloaded.Load(bytes);
        var back = reloaded.Report.FindBand(DesignerBandKind.Detail)!.Elements[0];
        back.IsVisible.Should().BeFalse();
    }

    [Fact]
    public void Group_header_and_footer_round_trip_with_elements()
    {
        var state = new DesignerState();
        var gh = state.Report.AddBand(new BandViewModel(DesignerBandKind.GroupHeader, Unit.FromMm(8))
        {
            GroupName = "Cliente",
            GroupExpression = "Fields.Cliente.Nome",
        });
        gh.AddElement(new ElementViewModel(DesignerElementKind.Label, "gh-lbl")
        {
            Text = "Cliente:",
            X = Unit.FromMm(5), Y = Unit.FromMm(1),
            Width = Unit.FromMm(60), Height = Unit.FromMm(6),
        });
        var gf = state.Report.AddBand(new BandViewModel(DesignerBandKind.GroupFooter, Unit.FromMm(10))
        {
            GroupName = "Cliente",
            GroupExpression = "Fields.Cliente.Nome",
        });
        gf.AddElement(new ElementViewModel(DesignerElementKind.TextBox, "gf-tb")
        {
            Expression = "{Sum(Fields.Total)}",
            X = Unit.FromMm(120), Y = Unit.FromMm(2),
            Width = Unit.FromMm(50), Height = Unit.FromMm(6),
        });

        var bytes = state.Save();
        var reloaded = new DesignerState();
        reloaded.Load(bytes);

        var reloadedGh = reloaded.Report.FindBand(DesignerBandKind.GroupHeader);
        var reloadedGf = reloaded.Report.FindBand(DesignerBandKind.GroupFooter);
        reloadedGh.Should().NotBeNull("o GroupHeader deveria persistir no .repx");
        reloadedGf.Should().NotBeNull("o GroupFooter deveria persistir no .repx");

        reloadedGh!.GroupName.Should().Be("Cliente");
        reloadedGh.GroupExpression.Should().Be("Fields.Cliente.Nome");
        reloadedGh.Elements.Should().ContainSingle();
        reloadedGh.Elements[0].Text.Should().Be("Cliente:");

        reloadedGf!.Elements.Should().ContainSingle();
        reloadedGf.Elements[0].Expression.Should().Be("{Sum(Fields.Total)}");
    }

    [Fact]
    public void Full_report_with_seven_bands_round_trips()
    {
        var state = new DesignerState();
        var report = state.Report;
        // Replace seeded with a richer layout.
        foreach (var band in report.Bands.ToList()) report.RemoveBand(band);

        report.AddBand(new BandViewModel(DesignerBandKind.ReportHeader, Unit.FromMm(20)));
        report.AddBand(new BandViewModel(DesignerBandKind.PageHeader,   Unit.FromMm(10)));
        report.AddBand(new BandViewModel(DesignerBandKind.GroupHeader,  Unit.FromMm(8))
        {
            GroupName = "Cliente", GroupExpression = "Fields.Cliente",
        });
        var detail = report.AddBand(new BandViewModel(DesignerBandKind.Detail, Unit.FromMm(6)));
        detail.AddElement(new ElementViewModel(DesignerElementKind.TextBox, "det1")
        {
            Expression = "{Fields.Produto}",
            X = Unit.FromMm(5), Y = Unit.FromMm(1),
            Width = Unit.FromMm(60), Height = Unit.FromMm(4),
        });
        report.AddBand(new BandViewModel(DesignerBandKind.GroupFooter,  Unit.FromMm(10))
        {
            GroupName = "Cliente", GroupExpression = "Fields.Cliente",
        });
        report.AddBand(new BandViewModel(DesignerBandKind.PageFooter,   Unit.FromMm(10)));
        report.AddBand(new BandViewModel(DesignerBandKind.ReportFooter, Unit.FromMm(15)));

        var bytes = state.Save();
        var reloaded = new DesignerState();
        reloaded.Load(bytes);

        var kinds = reloaded.Report.Bands.Select(b => b.Kind).ToArray();
        kinds.Should().Equal(
            DesignerBandKind.ReportHeader,
            DesignerBandKind.PageHeader,
            DesignerBandKind.GroupHeader,
            DesignerBandKind.Detail,
            DesignerBandKind.GroupFooter,
            DesignerBandKind.PageFooter,
            DesignerBandKind.ReportFooter);

        var reloadedDetail = reloaded.Report.FindBand(DesignerBandKind.Detail)!;
        reloadedDetail.Elements.Should().ContainSingle();
        reloadedDetail.Elements[0].Expression.Should().Be("{Fields.Produto}");
    }
}
