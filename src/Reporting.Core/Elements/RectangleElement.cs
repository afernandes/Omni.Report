using Reporting.Common;
using Reporting.Geometry;
using Reporting.Metadata;
using Reporting.Styling;

namespace Reporting.Elements;

/// <summary>A filled rectangle that doubles as a container — draws its optional fill (with corner radius) and then
/// lays out nested <see cref="Children"/> positioned relative to its top-left.</summary>
public sealed record RectangleElement : ReportElement
{
    /// <summary>Interior fill. Null leaves the rectangle unfilled, showing whatever is beneath it.</summary>
    [PropertyGrid(Category = "Forma", Order = 1, Label = "Preenchimento", Bindable = true)]
    public Color? FillColor { get; init; }

    /// <summary>Corner rounding. Applies to the clip region imposed on <see cref="Children"/>.</summary>
    [PropertyGrid(Category = "Forma", Order = 2, Label = "Raio do canto", Bindable = true)]
    public Unit CornerRadius { get; init; } = Unit.Zero;

    /// <summary>Nested report items, positioned by <see cref="ReportElement.Bounds"/> RELATIVE to this
    /// rectangle's top-left. The rectangle acts as a container (grouping/background): it draws its fill first,
    /// then its children on top. Children that overflow the rectangle are NOT clipped (visual parity with the
    /// flattened legacy behaviour); real clipping is a follow-up.</summary>
    public EquatableArray<ReportElement> Children { get; init; } = EquatableArray<ReportElement>.Empty;
}
