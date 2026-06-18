using System.Collections.ObjectModel;
using Bunit;
using FluentAssertions;
using Reporting.Designer.Blazor.Components;
using Reporting.Designer.Blazor.ViewModels;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>Covers the data tree's master-detail nesting (Crystal Reports /
/// FastReport pattern): child sources are indented under their parent with a
/// "↳ via <fk>" badge, and the parent's fields stay visible above the badge.</summary>
public class DataSourcesTreeTests : BunitContext
{
    public DataSourcesTreeTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static DesignerDataSource MakeSource(string name, params (string FieldName, DesignerFieldType Type)[] fields)
    {
        return new DesignerDataSource(name, fields.Select(f => new DesignerField(f.FieldName, f.Type)));
    }

    [Fact]
    public void Renders_parent_and_child_sources_when_relation_links_them()
    {
        // Two sources + one relation: Clientes.id → Pedidos.cliente_id.
        // Expected: tree shows BOTH parent header (with its fields) AND the nested
        // child header (with its own fields), separated by a "↳ via" badge.
        var clientes = MakeSource("Clientes",
            ("id", DesignerFieldType.Number),
            ("nome", DesignerFieldType.Text));
        var pedidos = MakeSource("Pedidos",
            ("cliente_id", DesignerFieldType.Number),
            ("produto", DesignerFieldType.Text));

        var relations = new ObservableCollection<DesignerRelation>
        {
            new("PedidosDeCliente", "Clientes", "id", "Pedidos", "cliente_id"),
        };

        var cut = Render<DataSourcesTree>(p => p
            .Add(t => t.DataSources, new[] { clientes, pedidos })
            .Add(t => t.Relations, relations));

        // Parent (Clientes) is at depth-0 with its name visible.
        cut.Markup.Should().Contain("Clientes", "parent source must show in the tree");

        // Child (Pedidos) must appear nested — depth-1 source row + the "via" badge.
        cut.Markup.Should().Contain("Pedidos", "child source must still appear, nested under parent");
        cut.Markup.Should().Contain("ds-rel-badge", "the badge surfaces the join key in the tree");
        cut.Markup.Should().Contain("via id"); // "via id" or "via id → cliente_id"

        // Fields from BOTH sources must be visible as draggable rows.
        var fields = cut.FindAll("[data-field-name]");
        fields.Select(n => n.GetAttribute("data-field-name")).Should().BeEquivalentTo(
            new[] { "id", "nome", "cliente_id", "produto" },
            "every field from every source — root and nested — must be draggable");

        // The child must be marked as depth-1 (nested under parent), not depth-0.
        var pedidoSourceRow = cut.FindAll(".ds-source-node")
            .First(n => n.TextContent.Contains("Pedidos"));
        pedidoSourceRow.ClassList.Should().Contain("depth-1");
    }

    [Fact]
    public void Renders_sources_flat_when_no_relation_exists()
    {
        // Sanity check: with no relations, both sources stay at root (depth-0).
        var a = MakeSource("A", ("x", DesignerFieldType.Text));
        var b = MakeSource("B", ("y", DesignerFieldType.Text));
        var cut = Render<DataSourcesTree>(p => p
            .Add(t => t.DataSources, new[] { a, b })
            .Add(t => t.Relations, new ObservableCollection<DesignerRelation>()));

        var sources = cut.FindAll(".ds-source-node");
        sources.Should().HaveCount(2);
        sources.All(s => s.ClassList.Contains("depth-0")).Should().BeTrue();
        cut.FindAll(".ds-rel-badge").Should().BeEmpty();
    }

    [Fact]
    public void Orphan_child_with_missing_parent_falls_back_to_root()
    {
        // Relation references a parent that doesn't exist in the catalog — the would-be
        // child shouldn't vanish. It surfaces at root so the user can repair the relation.
        var pedidos = MakeSource("Pedidos", ("produto", DesignerFieldType.Text));
        var stale = new ObservableCollection<DesignerRelation>
        {
            new("Stale", "DeletedParent", "id", "Pedidos", "cliente_id"),
        };
        var cut = Render<DataSourcesTree>(p => p
            .Add(t => t.DataSources, new[] { pedidos })
            .Add(t => t.Relations, stale));

        cut.Markup.Should().Contain("Pedidos");
        var sources = cut.FindAll(".ds-source-node");
        sources.Should().HaveCount(1);
        sources[0].ClassList.Should().Contain("depth-0");
    }
}
