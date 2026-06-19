using Reporting.Geometry;
using Reporting.Metadata;

namespace Reporting.Styling;

/// <summary>Visual style applied to a <c>ReportElement</c>. All properties are optional —
/// unset (null) means "inherit from parent or use renderer default".</summary>
/// <remarks>The scalar/enum properties carry <see cref="PropertyGridAttribute"/> so they can be
/// FLATTENED into an element's metadata grid when the element exposes its <c>Style</c> via
/// <c>[PropertyGrid(Nested = true)]</c>. Font/Border/Padding are complex records edited by dedicated
/// editors, so they're not flattened here.</remarks>
public sealed record Style(
    Font? Font = null,
    [property: PropertyGrid(Category = "Aparência", Order = 1, Label = "Cor do texto", Bindable = true)]
    Color? ForeColor = null,
    [property: PropertyGrid(Category = "Aparência", Order = 2, Label = "Cor de fundo", Bindable = true)]
    Color? BackColor = null,
    Border? Border = null,
    Thickness? Padding = null,
    [property: PropertyGrid(Category = "Aparência", Order = 5, Label = "Alinhamento H")]
    HorizontalAlignment HorizontalAlignment = HorizontalAlignment.Left,
    [property: PropertyGrid(Category = "Aparência", Order = 6, Label = "Alinhamento V")]
    VerticalAlignment VerticalAlignment = VerticalAlignment.Top,
    [property: PropertyGrid(Category = "Aparência", Order = 7, Label = "Quebra de linha", Bindable = true)]
    bool WordWrap = true,
    [property: PropertyGrid(Category = "Aparência", Order = 8, Label = "Formato", Bindable = true)]
    string? Format = null)
{
    public static readonly Style Default = new();
}
