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
}
