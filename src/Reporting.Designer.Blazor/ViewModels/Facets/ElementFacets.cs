using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;

namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>Static caption.</summary>
internal sealed class LabelFacet : ElementFacet
{
    internal override IReadOnlyList<DesignerElementKind> Kinds => [DesignerElementKind.Label];
    internal override bool Owns(ReportElement element) => element is LabelElement;

    internal override ReportElement Build(ElementViewModel vm) =>
        new LabelElement { Text = vm.Text, Bounds = vm.Bounds };

    internal override void Read(ElementViewModel vm, ReportElement element) =>
        vm.Text = ((LabelElement)element).Text;
}

/// <summary>Expression-bound text, optionally auto-sizing.</summary>
internal sealed class TextBoxFacet : ElementFacet
{
    internal override IReadOnlyList<DesignerElementKind> Kinds => [DesignerElementKind.TextBox];
    internal override bool Owns(ReportElement element) => element is TextBoxElement;

    internal override ReportElement Build(ElementViewModel vm) => new TextBoxElement
    {
        Expression = vm.Expression,
        Bounds = vm.Bounds,
        CanGrow = vm.CanGrow,
        CanShrink = vm.CanShrink,
        TextRuns = vm.TextRuns,
    };

    internal override void Read(ElementViewModel vm, ReportElement element)
    {
        var tb = (TextBoxElement)element;
        vm.Expression = tb.Expression;
        vm.CanGrow = tb.CanGrow;
        vm.CanShrink = tb.CanShrink;
        vm.TextRuns = tb.TextRuns; // preserve mixed-style runs across edit→save (no editor yet)
    }
}

/// <summary>Straight rule.</summary>
internal sealed class LineFacet : ElementFacet
{
    internal override IReadOnlyList<DesignerElementKind> Kinds => [DesignerElementKind.Line];
    internal override bool Owns(ReportElement element) => element is LineElement;

    internal override ReportElement Build(ElementViewModel vm) =>
        new LineElement { Bounds = vm.Bounds, Direction = vm.LineDir };

    internal override void Read(ElementViewModel vm, ReportElement element) =>
        vm.LineDir = ((LineElement)element).Direction;
}

/// <summary>Container: the only facet with children, materialised recursively as editable child VMs.</summary>
internal sealed class RectangleFacet : ElementFacet
{
    internal override IReadOnlyList<DesignerElementKind> Kinds => [DesignerElementKind.Rectangle];
    internal override bool Owns(ReportElement element) => element is RectangleElement;

    internal override ReportElement Build(ElementViewModel vm) => new RectangleElement
    {
        Bounds = vm.Bounds,
        FillColor = vm.FillColor,
        CornerRadius = Unit.FromMm(vm.CornerRadiusMm),
        Children = vm.Children.Count == 0
            ? EquatableArray<ReportElement>.Empty
            : new EquatableArray<ReportElement>(vm.Children.Select(c => c.ToElement()).ToArray()),
    };

    internal override void Read(ElementViewModel vm, ReportElement element)
    {
        var rect = (RectangleElement)element;
        vm.FillColor = rect.FillColor;
        vm.CornerRadiusMm = rect.CornerRadius.ToMm();
        // Materialise children into editable child VMs (recursive — a child Rectangle materialises its own
        // children in turn). The same FromElement path preserves opaque kinds, so depth is unbounded.
        foreach (var child in rect.Children)
        {
            vm.AttachChild(ElementViewModel.FromElement(child));
        }
    }
}

/// <summary>Filled ellipse.</summary>
internal sealed class EllipseFacet : ElementFacet
{
    internal override IReadOnlyList<DesignerElementKind> Kinds => [DesignerElementKind.Ellipse];
    internal override bool Owns(ReportElement element) => element is EllipseElement;

    internal override ReportElement Build(ElementViewModel vm) =>
        new EllipseElement { Bounds = vm.Bounds, FillColor = vm.FillColor };

    internal override void Read(ElementViewModel vm, ReportElement element) =>
        vm.FillColor = ((EllipseElement)element).FillColor;
}

/// <summary>Raster image from embedded bytes, a per-row expression, or a static path/URL.</summary>
internal sealed class ImageFacet : ElementFacet
{
    internal override IReadOnlyList<DesignerElementKind> Kinds => [DesignerElementKind.Image];
    internal override bool Owns(ReportElement element) => element is ImageElement;

    internal override ReportElement Build(ElementViewModel vm) => new ImageElement
    {
        Bounds = vm.Bounds,
        // Source kind is inferred from which field the user filled: embedded bytes win, then a per-row
        // expression, otherwise a static path/URL.
        Source = vm.InlineImageData is { Length: > 0 } ? ImageSourceKind.Inline
            : !string.IsNullOrWhiteSpace(vm.ImageExpression) ? ImageSourceKind.Expression
            : ImageSourceKind.Path,
        InlineData = vm.InlineImageData is { Length: > 0 }
            ? new EquatableArray<byte>(vm.InlineImageData)
            : EquatableArray<byte>.Empty,
        Path = string.IsNullOrWhiteSpace(vm.ImagePath) ? null : vm.ImagePath,
        Expression = string.IsNullOrWhiteSpace(vm.ImageExpression) ? null : vm.ImageExpression,
        Sizing = vm.ImageSizing,
    };

    internal override void Read(ElementViewModel vm, ReportElement element)
    {
        var img = (ImageElement)element;
        vm.InlineImageData = img.InlineData.Count > 0 ? img.InlineData.ToArray() : null;
        vm.ImagePath = img.Path;
        vm.ImageExpression = img.Expression;
        vm.ImageSizing = img.Sizing;
    }
}

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

/// <summary>Chart with its series list.</summary>
internal sealed class ChartFacet : ElementFacet
{
    internal override IReadOnlyList<DesignerElementKind> Kinds => [DesignerElementKind.Chart];
    internal override bool Owns(ReportElement element) => element is ChartElement;

    internal override ReportElement Build(ElementViewModel vm) => new ChartElement
    {
        Bounds = vm.Bounds,
        Kind = vm.ChartKind,
        Title = string.IsNullOrWhiteSpace(vm.ChartTitle) ? null : vm.ChartTitle,
        ShowLegend = vm.ShowLegend,
        Series = vm.ChartSeries.Count == 0
            ? EquatableArray<ChartSeries>.Empty
            : new EquatableArray<ChartSeries>(vm.ChartSeries.Select(s => s.ToSeries())),
    };

    internal override void Read(ElementViewModel vm, ReportElement element)
    {
        var chart = (ChartElement)element;
        vm.ChartKind = chart.Kind;
        vm.ChartTitle = chart.Title ?? string.Empty;
        vm.ShowLegend = chart.ShowLegend;
        foreach (var series in chart.Series)
        {
            vm.ChartSeries.Add(ChartSeriesRule.From(series));
        }
    }
}
