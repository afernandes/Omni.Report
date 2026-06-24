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
    [property: PropertyGrid(Category = "Aparência", Order = 0, Label = "Fonte", Editor = "font")]
    Font? Font = null,
    [property: PropertyGrid(Category = "Aparência", Order = 1, Label = "Cor do texto", Bindable = true)]
    Color? ForeColor = null,
    [property: PropertyGrid(Category = "Aparência", Order = 2, Label = "Cor de fundo", Bindable = true)]
    Color? BackColor = null,
    [property: PropertyGrid(Category = "Borda", Order = 10, Label = "Borda", Editor = "border")]
    Border? Border = null,
    [property: PropertyGrid(Category = "Borda", Order = 11, Label = "Padding", Editor = "padding")]
    Thickness? Padding = null,
    [property: PropertyGrid(Category = "Aparência", Order = 5, Label = "Alinhamento H", Editor = "h-align")]
    HorizontalAlignment HorizontalAlignment = HorizontalAlignment.Left,
    [property: PropertyGrid(Category = "Aparência", Order = 6, Label = "Alinhamento V", Editor = "v-align")]
    VerticalAlignment VerticalAlignment = VerticalAlignment.Top,
    [property: PropertyGrid(Category = "Aparência", Order = 7, Label = "Quebra de linha", Bindable = true)]
    bool WordWrap = true,
    [property: PropertyGrid(Category = "Aparência", Order = 9, Label = "Formato", Editor = "format-preset", Bindable = true)]
    string? Format = null,
    // Complex record (like Font/Border) — edited by a dedicated editor, NOT flattened into the metadata grid.
    BackgroundImage? BackgroundImage = null,
    // Gradient fill (RDL-aligned): when BackgroundGradient != None, the fill blends BackColor (start) → BackColorEnd
    // (end) along the direction. These are flattened scalar/enum props so the metadata grid edits them with the
    // existing color-picker + enum editors (no dedicated editor needed). Kept at the END to preserve positional ctor compat.
    [property: PropertyGrid(Category = "Aparência", Order = 3, Label = "Cor de fundo (fim)", Bindable = true)]
    Color? BackColorEnd = null,
    [property: PropertyGrid(Category = "Aparência", Order = 4, Label = "Gradiente")]
    BackgroundGradientType BackgroundGradient = BackgroundGradientType.None)
{
    public static readonly Style Default = new();
}
