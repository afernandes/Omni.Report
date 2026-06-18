using System.Collections.ObjectModel;
using Bunit;
using FluentAssertions;
using Reporting.Designer.Blazor.Components;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Geometry;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>Covers the SubDetail band's DataMember picker — both surfaces (canvas
/// caption strip and OutlineTree row) must list every registered relation, every
/// data source, and the band's current binding even when it went stale.
/// Mirrors FastReport / DevExpress UX where the master-detail link is editable
/// from a labeled dropdown, not a free-text field.</summary>
public class SubDetailCaptionTests : BunitContext
{
    public SubDetailCaptionTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static (ReportDefinitionViewModel vm, BandViewModel sub) BuildReportWithSubDetail(string? dataMember)
    {
        var vm = new ReportDefinitionViewModel("test");
        // Clear seeded bands; add a fresh Detail + SubDetail pair so the SD is the band
        // we assert on (no other bands compete for the caption selector).
        foreach (var b in vm.Bands.ToList()) vm.RemoveBand(b);
        vm.AddBand(new BandViewModel(DesignerBandKind.Detail, Unit.FromMm(10)));
        var sub = vm.AddBand(new BandViewModel(DesignerBandKind.SubDetail, Unit.FromMm(6))
        {
            DataMember = dataMember ?? string.Empty,
        });
        return (vm, sub);
    }

    [Fact]
    public void Subdetail_caption_lists_relations_and_sources_with_friendly_labels()
    {
        // Catalog: 2 sources (Clientes parent, Pedidos child) + 1 relation. The caption
        // must surface the relation under "Relações" (master-detail) and both sources
        // under "Fontes" — the user shouldn't need to remember the relation name.
        var clientes = new DesignerDataSource("Clientes", new[] { new DesignerField("id", DesignerFieldType.Number) });
        var pedidos  = new DesignerDataSource("Pedidos",  new[] { new DesignerField("cliente_id", DesignerFieldType.Number) });
        var relations = new ObservableCollection<DesignerRelation>
        {
            new("PedidosDeCliente", "Clientes", "id", "Pedidos", "cliente_id"),
        };
        var (vm, sub) = BuildReportWithSubDetail(dataMember: null);

        // Caption only renders BELOW the active band (DevExpress-style band-properties
        // surface) — so we have to activate the sub-band before asserting.
        var cut = Render<BandCanvas>(p => p
            .Add(c => c.Report, vm)
            .Add(c => c.ActiveBand, sub)
            .Add(c => c.DataSources, new[] { clientes, pedidos })
            .Add(c => c.Relations, relations));

        // The caption select must include the relation option with its friendly compound label.
        var select = cut.Find(".band-caption-row .band-caption-select");
        var options = select.QuerySelectorAll("option").Select(o => o.TextContent).ToList();
        options.Should().ContainMatch("*PedidosDeCliente*Clientes*Pedidos*",
            "relations must appear with parent → child context so the user picks the right one");
        // And both data sources show under the "iteração solta" group.
        options.Should().ContainMatch("*Clientes*todas as linhas*");
        options.Should().ContainMatch("*Pedidos*todas as linhas*");
    }

    [Fact]
    public void Subdetail_caption_warns_when_no_data_member_is_set()
    {
        // Empty DataMember + active band ⇒ "⚠ não configurado" inline next to the select.
        var (vm, sub) = BuildReportWithSubDetail(dataMember: null);
        var cut = Render<BandCanvas>(p => p
            .Add(c => c.Report, vm)
            .Add(c => c.ActiveBand, sub)
            .Add(c => c.DataSources, Array.Empty<DesignerDataSource>())
            .Add(c => c.Relations, new ObservableCollection<DesignerRelation>()));

        cut.Find(".band-caption-warn").TextContent.Should().Contain("não configurado");
    }

    [Fact]
    public void Inactive_subdetail_without_data_member_shows_discovery_hint()
    {
        // When the SD isn't focused but also has no DataMember, the user gets a small
        // warning pill underneath telling them the band is incomplete. Clicking it
        // activates the band, which then swaps the pill for the full caption.
        var (vm, _) = BuildReportWithSubDetail(dataMember: null);
        var cut = Render<BandCanvas>(p => p
            .Add(c => c.Report, vm)
            .Add(c => c.ActiveBand, (BandViewModel?)null) // intentionally NOT focused
            .Add(c => c.DataSources, Array.Empty<DesignerDataSource>())
            .Add(c => c.Relations, new ObservableCollection<DesignerRelation>()));

        cut.Find(".band-caption-hint").TextContent.Should().Contain("sem DataMember");
        // And the full caption-row must NOT be rendered while inactive.
        cut.FindAll(".band-caption-row").Should().BeEmpty();
    }

    [Fact]
    public void Subdetail_caption_surfaces_stale_binding_so_user_can_repair_it()
    {
        // Imagine: the user renamed/deleted "PedidosDeCliente" → the SubDetail still
        // points at the old name. The select must keep the stale value visible with
        // a ⚠ marker rather than silently switching to "— selecione —".
        var (vm, sub) = BuildReportWithSubDetail(dataMember: "RelacaoFantasma");
        var cut = Render<BandCanvas>(p => p
            .Add(c => c.Report, vm)
            .Add(c => c.ActiveBand, sub)
            .Add(c => c.DataSources, Array.Empty<DesignerDataSource>())
            .Add(c => c.Relations, new ObservableCollection<DesignerRelation>()));

        var options = cut.FindAll(".band-caption-row .band-caption-select option").Select(o => o.TextContent).ToList();
        options.Should().ContainMatch("*RelacaoFantasma*não encontrado*",
            "stale bindings must stay visible so the user can repair them");
    }

    [Fact]
    public void Outline_tree_shows_datamember_row_under_each_subdetail_band()
    {
        // The outline mirrors the canvas: every SubDetail band gets a child row with
        // its own DataMember picker. Lets users wire bindings from the right panel
        // without scrolling the canvas.
        var clientes = new DesignerDataSource("Clientes", new[] { new DesignerField("id", DesignerFieldType.Number) });
        var pedidos  = new DesignerDataSource("Pedidos",  new[] { new DesignerField("cliente_id", DesignerFieldType.Number) });
        var relations = new ObservableCollection<DesignerRelation>
        {
            new("PedidosDeCliente", "Clientes", "id", "Pedidos", "cliente_id"),
        };
        var (vm, _) = BuildReportWithSubDetail(dataMember: "PedidosDeCliente");

        var cut = Render<OutlineTree>(p => p
            .Add(o => o.Report, vm)
            .Add(o => o.DataSources, new[] { clientes, pedidos })
            .Add(o => o.Relations, relations));

        // The is-datamember row must exist with a select whose current value is the
        // relation we wired up.
        var dmRow = cut.Find(".outline-row.is-datamember");
        var select = dmRow.QuerySelector(".outline-datamember")!;
        select.GetAttribute("value").Should().Be("PedidosDeCliente");
    }
}
