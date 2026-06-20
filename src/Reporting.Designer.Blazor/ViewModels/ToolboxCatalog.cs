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

/// <summary>
/// The element toolbox, discovered by reflection from the <see cref="ToolboxElementAttribute"/> annotations
/// on <see cref="DesignerElementKind"/>. Items keep their enum declaration order within a group; groups
/// follow <see cref="GroupOrder"/> (any unlisted group is appended). A new annotated element shows up in the
/// toolbox with no markup changes.
/// </summary>
public static class ToolboxCatalog
{
    /// <summary>Canonical left-to-right group order. A group not listed here appears after these.</summary>
    private static readonly string[] GroupOrder = ["Básicos", "Dados", "Gráficos", "Avançados"];

    public sealed record Item(
        DesignerElementKind Kind, string Label, string Icon, string Description, string? Hotkey, bool Draggable,
        double DefaultWidthMm, double DefaultHeightMm, string? DefaultText, string? DefaultExpression, string? PreviewLabel);

    public sealed record Group(string Name, IReadOnlyList<Item> Items);

    public static IReadOnlyList<Group> Groups { get; } = Build();

    private static readonly IReadOnlyDictionary<DesignerElementKind, Item> ByKind =
        Groups.SelectMany(g => g.Items).ToDictionary(i => i.Kind);

    /// <summary>The catalog entry for a kind (every annotated kind has one). Used by the designer for
    /// the element's icon, creation defaults and canvas preview label — a single source per element.</summary>
    public static Item For(DesignerElementKind kind) => ByKind[kind];

    private static IReadOnlyList<Group> Build()
    {
        var byGroup = new Dictionary<string, List<Item>>();
        var seen = new List<string>();
        // GetFields has no documented ordering — sort by the enum's underlying value (= declaration order
        // here) so item order is deterministic, since component tests address the basics by index.
        var fields = typeof(DesignerElementKind).GetFields(BindingFlags.Public | BindingFlags.Static)
            .OrderBy(f => (int)(DesignerElementKind)f.GetValue(null)!);
        foreach (var field in fields)
        {
            if (field.GetCustomAttribute<ToolboxElementAttribute>() is not { } a)
            {
                continue;
            }
            var kind = (DesignerElementKind)field.GetValue(null)!;
            if (!byGroup.TryGetValue(a.Group, out var list))
            {
                list = new List<Item>();
                byGroup[a.Group] = list;
                seen.Add(a.Group);
            }
            list.Add(new Item(kind, a.Label, a.Icon, a.Description, a.Hotkey, a.Draggable,
                a.DefaultWidthMm, a.DefaultHeightMm, a.DefaultText, a.DefaultExpression, a.PreviewLabel));
        }

        return seen
            .OrderBy(g => Array.IndexOf(GroupOrder, g) is var i && i >= 0 ? i : int.MaxValue)
            .Select(g => new Group(g, byGroup[g]))
            .ToList();
    }
}
