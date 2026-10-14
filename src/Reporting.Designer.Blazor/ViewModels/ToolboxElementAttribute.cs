using System.Reflection;

namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>
/// Declares how a <see cref="DesignerElementKind"/> appears in the element toolbox. Put this on the enum
/// value and the toolbox picks it up automatically (see <see cref="ToolboxCatalog"/>) — adding a new
/// element no longer means editing the toolbox markup by hand.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class ToolboxElementAttribute : Attribute
{
    public ToolboxElementAttribute(string group, string label, string icon, string description)
    {
        Group = group;
        Label = label;
        Icon = icon;
        Description = description;
    }

    /// <summary>The toolbox group header this element appears under (e.g. "Básicos", "Gráficos").</summary>
    public string Group { get; }
    public string Label { get; }
    /// <summary>Icon name resolved by the IconCatalog.</summary>
    public string Icon { get; }
    public string Description { get; }
    /// <summary>Optional keyboard hint shown on the item (basic elements only).</summary>
    public string? Hotkey { get; init; }
    /// <summary>When true the item is draggable onto the canvas and carries the data-kind hook
    /// (the basic shapes); other groups are click-to-add.</summary>
    public bool Draggable { get; init; }

    // ── Creation defaults (used by the designer when a new element is dropped) ────
    /// <summary>Default width in mm for a freshly added element (40 mm placeholder by default).</summary>
    public double DefaultWidthMm { get; init; } = 40;
    /// <summary>Default height in mm (6 mm by default; e.g. a line uses 0).</summary>
    public double DefaultHeightMm { get; init; } = 6;
    /// <summary>Initial Text for the element (Label only).</summary>
    public string? DefaultText { get; init; }
    /// <summary>Initial Expression placeholder so the preview doesn't blow up on empty input.</summary>
    public string? DefaultExpression { get; init; }
    /// <summary>Placeholder caption shown in the canvas for elements that render as a dashed box
    /// (Tablix/Gauge/…) instead of native content.</summary>
    public string? PreviewLabel { get; init; }
}
