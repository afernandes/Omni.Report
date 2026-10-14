using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;

namespace Reporting.Designer.Blazor.ViewModels;

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
