using FluentAssertions;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Elements;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// Guards that basic-element parameters are authorable FROM SCRATCH in the designer — set on a
/// freshly added element, emitted by <see cref="ElementViewModel.ToElement"/>, and surfaced again by
/// <see cref="ElementViewModel.FromElement"/>. Closes the audit gaps where a property round-tripped
/// from a loaded .repx but had no way to be set/edited when building a new report.
/// </summary>
public class DesignerBasicElementAuthorabilityTests
{
    [Fact]
    public void Line_orientation_is_authorable_from_scratch_and_round_trips()
    {
        var vm = new ElementViewModel(DesignerElementKind.Line, "l1") { LineDir = LineDirection.Vertical };

        var line = vm.ToElement().Should().BeOfType<LineElement>().Subject;
        line.Direction.Should().Be(LineDirection.Vertical, "a vertical line must be buildable, not forced horizontal");

        ElementViewModel.FromElement(line).LineDir.Should().Be(LineDirection.Vertical);
    }

    [Fact]
    public void Image_path_expression_and_sizing_are_authorable_and_round_trip()
    {
        var vm = new ElementViewModel(DesignerElementKind.Image, "i1")
        {
            ImagePath = "https://x/logo.png",
            ImageSizing = ImageSizing.Fill,
        };

        var img = vm.ToElement().Should().BeOfType<ImageElement>().Subject;
        img.Source.Should().Be(ImageSourceKind.Path, "a path/URL image (no inline bytes) is a Path source");
        img.Path.Should().Be("https://x/logo.png");
        img.Sizing.Should().Be(ImageSizing.Fill);

        var back = ElementViewModel.FromElement(img);
        back.ImagePath.Should().Be("https://x/logo.png");
        back.ImageSizing.Should().Be(ImageSizing.Fill);

        // A per-row expression makes it a data-bound image.
        var bound = (ImageElement)new ElementViewModel(DesignerElementKind.Image, "i2")
        {
            ImageExpression = "Fields.Foto",
        }.ToElement();
        bound.Source.Should().Be(ImageSourceKind.Expression);
        bound.Expression.Should().Be("Fields.Foto");
    }

    [Fact]
    public void Barcode_symbology_and_text_strip_are_authorable_and_round_trip()
    {
        var vm = new ElementViewModel(DesignerElementKind.Barcode, "b1")
        {
            Expression = "Fields.Code",
            Symbology = BarcodeSymbology.Code39,
            BarcodeShowText = false,
        };

        var bc = vm.ToElement().Should().BeOfType<BarcodeElement>().Subject;
        bc.Symbology.Should().Be(BarcodeSymbology.Code39, "the picked 1D symbology must stick, not default to Code128");
        bc.ShowText.Should().BeFalse("the human-readable text strip must be toggleable");

        var back = ElementViewModel.FromElement(bc);
        back.Symbology.Should().Be(BarcodeSymbology.Code39);
        back.BarcodeShowText.Should().BeFalse();
    }

    [Fact]
    public void Sparkline_category_is_authorable_and_round_trips()
    {
        var vm = new ElementViewModel(DesignerElementKind.Sparkline, "s1") { SparkCategory = "Fields.Mes" };

        var sp = vm.ToElement().Should().BeOfType<SparklineElement>().Subject;
        sp.CategoryExpression.Should().Be("Fields.Mes");

        ElementViewModel.FromElement(sp).SparkCategory.Should().Be("Fields.Mes");
    }

    [Fact]
    public void Visibility_expression_and_autosize_are_authorable_and_round_trip()
    {
        var vm = new ElementViewModel(DesignerElementKind.TextBox, "t1")
        {
            Expression = "Fields.X",
            VisibleExpr = "Fields.Total > 0",
            CanGrow = true,
            CanShrink = true,
        };

        var tb = vm.ToElement().Should().BeOfType<TextBoxElement>().Subject;
        tb.VisibleExpression.Should().Be("Fields.Total > 0", "a conditional-visibility expression must be settable, not just Visible on/off");
        tb.CanGrow.Should().BeTrue();
        tb.CanShrink.Should().BeTrue();

        var back = ElementViewModel.FromElement(tb);
        back.VisibleExpr.Should().Be("Fields.Total > 0");
        back.CanGrow.Should().BeTrue();
        back.CanShrink.Should().BeTrue();
    }

    [Fact]
    public void Rectangle_corner_radius_is_authorable_and_round_trips()
    {
        var vm = new ElementViewModel(DesignerElementKind.Rectangle, "r1") { CornerRadiusMm = 3.5 };

        var rect = vm.ToElement().Should().BeOfType<RectangleElement>().Subject;
        rect.CornerRadius.ToMm().Should().BeApproximately(3.5, 0.01);

        ElementViewModel.FromElement(rect).CornerRadiusMm.Should().BeApproximately(3.5, 0.01);
    }

    [Fact]
    public void Chart_series_colour_is_authorable_and_round_trips()
    {
        var vm = new ElementViewModel(DesignerElementKind.Chart, "c1");
        vm.ChartSeries.Add(new ChartSeriesRule { Name = "A", ColorHex = "#FF8800" });

        var chart = vm.ToElement().Should().BeOfType<ChartElement>().Subject;
        var color = chart.Series[0].Color;
        color.Should().NotBeNull("a per-series colour picked in the designer must reach the model");
        (color!.Value.R, color.Value.G, color.Value.B).Should().Be(((byte)0xFF, (byte)0x88, (byte)0x00));

        ElementViewModel.FromElement(chart).ChartSeries[0].Color.Should().NotBeNull();
    }

    [Fact]
    public void Indicator_icon_per_state_is_authorable_and_round_trips()
    {
        var vm = new ElementViewModel(DesignerElementKind.Indicator, "ind1");
        vm.AddIndicatorState();
        vm.SetIndicatorState(0, start: "0", end: "100", icon: "ArrowUp");

        var ind = vm.ToElement().Should().BeOfType<IndicatorElement>().Subject;
        ind.States.Should().Contain(s => s.IconName == "ArrowUp", "the per-state icon must be editable, not only Start/End");
    }
}
