using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;

namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>1D barcodes and QR. Both are <see cref="BarcodeElement"/>, but QR is a separate designer kind
/// so the toolbox / outline / property grid can give it dedicated affordances.</summary>
internal sealed class BarcodeFacet : ElementFacet
{
    internal override IReadOnlyList<DesignerElementKind> Kinds =>
        [DesignerElementKind.Barcode, DesignerElementKind.QrCode];

    internal override bool Owns(ReportElement element) => element is BarcodeElement;

    internal override ReportElement Build(ElementViewModel vm) => vm.Kind == DesignerElementKind.QrCode
        ? new BarcodeElement
        {
            Bounds = vm.Bounds,
            Expression = vm.Expression,
            Symbology = BarcodeSymbology.QrCode,
            QrEcc = vm.QrEcc,
            ShowText = false, // QR has no human-readable text strip
        }
        : new BarcodeElement
        {
            Bounds = vm.Bounds,
            Expression = vm.Expression,
            // Honour the picked 1D symbology; if the user accidentally set QrCode on a Barcode-kind element
            // via direct property binding, force back to Code128 — the QrCode kind is the canonical place for QR.
            Symbology = vm.Symbology == BarcodeSymbology.QrCode ? BarcodeSymbology.Code128 : vm.Symbology,
            ShowText = vm.BarcodeShowText,
        };

    internal override void Read(ElementViewModel vm, ReportElement element)
    {
        var bc = (BarcodeElement)element;
        vm.Expression = bc.Expression;
        vm.Symbology = bc.Symbology;
        vm.QrEcc = bc.QrEcc;
        vm.BarcodeShowText = bc.ShowText;
    }
}
