using System.Reflection;

namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>
/// The element toolbox, discovered by reflection from the <see cref="ToolboxElementAttribute"/> annotations
/// on <see cref="DesignerElementKind"/>. Items keep their enum declaration order within a group; groups
/// follow <see cref="GroupOrder"/> (any unlisted group is appended). A new annotated element shows up in the
/// toolbox with no markup changes.
/// </summary>
public static partial class ToolboxCatalog
{
    /// <summary>Canonical left-to-right group order. A group not listed here appears after these.</summary>
    private static readonly string[] GroupOrder = ["Básicos", "Dados", "Gráficos", "Avançados"];

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
