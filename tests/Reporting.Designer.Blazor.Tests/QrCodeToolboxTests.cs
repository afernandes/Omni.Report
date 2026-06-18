using Bunit;
using FluentAssertions;
using Reporting.Designer.Blazor.Components;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Elements;
using Reporting.Geometry;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>Covers the QR-Code toolbox affordance + round-trip: the toolbox exposes a
/// dedicated button, and inserting/saving/loading preserves the QR-specific properties
/// (Symbology=QrCode, ECC level).</summary>
public class QrCodeToolboxTests : BunitContext
{
    public QrCodeToolboxTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Toolbox_exposes_a_dedicated_qr_code_button()
    {
        // The button should sit next to "Barcode" so they're discoverable together.
        // We check the rendered markup carries the qr-code icon class — that's the
        // user-visible affordance (a click on the button inserts the element).
        var cut = Render<ElementToolbox>(p => p
            .Add(t => t.OnAdd, _ => Task.CompletedTask)
            .Add(t => t.OnAddSystemField, _ => Task.CompletedTask));

        cut.Markup.Should().Contain("QR Code", "the toolbox label must read 'QR Code'");
        // Each toolbox button carries a data-kind attribute matching the DesignerElementKind
        // enum name — that's how the JS drop handler routes the insertion.
        cut.Find("[data-kind='QrCode']").Should().NotBeNull(
            "the QR Code button must be marked with data-kind='QrCode'");
    }

    [Fact]
    public void ToElement_for_QrCode_kind_emits_BarcodeElement_with_QrCode_symbology()
    {
        var vm = new ElementViewModel(DesignerElementKind.QrCode, "qr-1")
        {
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(30), Unit.FromMm(30)),
            Expression = "https://example.com",
            QrEcc = QrEccLevel.High,
        };

        var element = vm.ToElement();
        var barcode = element.Should().BeOfType<BarcodeElement>().Subject;
        barcode.Symbology.Should().Be(BarcodeSymbology.QrCode);
        barcode.QrEcc.Should().Be(QrEccLevel.High);
        barcode.ShowText.Should().BeFalse("QR has no human-readable text strip");
    }

    [Fact]
    public void FromElement_routes_QrCode_symbology_to_QrCode_kind()
    {
        // Round-trip from the immutable model back into the designer VM. A BarcodeElement
        // with Symbology=QrCode must land in DesignerElementKind.QrCode (not Barcode), so
        // the toolbox / outline / canvas use the QR-specific icon and defaults.
        var element = new BarcodeElement
        {
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(25), Unit.FromMm(25)),
            Expression = "00020126...PIX...",
            Symbology = BarcodeSymbology.QrCode,
            QrEcc = QrEccLevel.Quartile,
            ShowText = false,
        };

        var vm = ElementViewModel.FromElement(element);
        vm.Kind.Should().Be(DesignerElementKind.QrCode);
        vm.Symbology.Should().Be(BarcodeSymbology.QrCode);
        vm.QrEcc.Should().Be(QrEccLevel.Quartile);
    }

    [Fact]
    public void FromElement_routes_other_symbologies_to_Barcode_kind()
    {
        // Sanity check: only QrCode goes to the QrCode kind; every other symbology stays
        // on the 1D Barcode kind.
        var element = new BarcodeElement
        {
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(50), Unit.FromMm(15)),
            Expression = "5901234123457",
            Symbology = BarcodeSymbology.Ean13,
        };

        var vm = ElementViewModel.FromElement(element);
        vm.Kind.Should().Be(DesignerElementKind.Barcode);
        vm.Symbology.Should().Be(BarcodeSymbology.Ean13);
    }
}
