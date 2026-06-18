using FluentAssertions;
using Reporting.Bands;
using Reporting.CodeFirst;
using Reporting.Data;
using Reporting.Elements;
using Reporting.Parameters;
using Xunit;

namespace Reporting.CodeFirst.Tests;

/// <summary>
/// Coverage for the CodeFirst fluent API surface added for RDL Phase 1 compatibility:
/// element-level <see cref="ReportElement.Bookmark"/>/<see cref="ReportElement.DocumentMapLabel"/>/
/// <see cref="ReportElement.Action"/>/<see cref="ReportElement.ToggleItemId"/>, band-level
/// PageBreak, Detail-level Filter/Sort/NoRowsMessage/PageBreak, DataSource-level
/// CalculatedField/Filter/Sort, Group-level Filter/Sort/Variables/PageBreak. Each test
/// proves that calling the fluent helper produces a <see cref="ReportDefinition"/> whose
/// model carries the expected RDL field — proving the code-first API can express every
/// runtime feature surfaced by the Designer.
/// </summary>
public sealed record Pedido(int Id, string Cliente, decimal Total);

public class RdlCodeFirstTests
{
    [Fact]
    public void Element_bookmark_and_document_map_label_are_set()
    {
        var def = ReportBuilder.Create("test")
            .Detail(d => d.Text("{Fields.Cliente}")
                .At(0, 0).Size(80, 6)
                .Bookmark("anchor")
                .DocumentMapLabel("Cliente"))
            .Build().Definition;

        var element = def.Detail.Elements[0];
        element.Bookmark.Should().Be("anchor");
        element.DocumentMapLabel.Should().Be("Cliente");
    }

    [Fact]
    public void Hyperlink_action_is_attached()
    {
        var def = ReportBuilder.Create("test")
            .Detail(d => d.Text("{Fields.Cliente}")
                .At(0, 0).Size(80, 6)
                .Hyperlink("https://example.com/{Fields.Id}"))
            .Build().Definition;

        var action = def.Detail.Elements[0].Action;
        action.Should().NotBeNull();
        action!.Kind.Should().Be(ActionKind.Hyperlink);
        action.Hyperlink.Should().Be("https://example.com/{Fields.Id}");
    }

    [Fact]
    public void Drillthrough_action_with_parameters_is_attached()
    {
        var def = ReportBuilder.Create("test")
            .Detail(d => d.Text("{Fields.Cliente}")
                .At(0, 0).Size(80, 6)
                .Drillthrough("Detalhes",
                    new DrillthroughParameter("Id", "{Fields.Id}"),
                    new DrillthroughParameter("Ano", "{Parameters.Ano}", Omit: true)))
            .Build().Definition;

        var action = def.Detail.Elements[0].Action;
        action.Should().NotBeNull();
        action!.Kind.Should().Be(ActionKind.DrillthroughReport);
        action.DrillthroughReportName.Should().Be("Detalhes");
        action.DrillthroughParameters.Should().HaveCount(2);
        action.DrillthroughParameters[1].Omit.Should().BeTrue();
    }

    [Fact]
    public void Toggle_and_start_hidden_set_drill_down_state()
    {
        var def = ReportBuilder.Create("test")
            .Detail(d => d.Text("{Fields.Detalhe}")
                .At(0, 0).Size(80, 6)
                .ToggleItem("master-cell")
                .StartHidden())
            .Build().Definition;

        var e = def.Detail.Elements[0];
        e.ToggleItemId.Should().Be("master-cell");
        e.InitiallyHidden.Should().BeTrue();
    }

    [Fact]
    public void Detail_no_rows_filter_sort_and_page_break_are_set()
    {
        var def = ReportBuilder.Create("test")
            .Detail(d => d.Text("{Fields.Total}").At(0, 0).Size(50, 6))
            .DetailNoRows("Sem dados.")
            .DetailFilter("Fields.Total > 0")
            .DetailSortBy("Fields.Cliente")
            .DetailSortBy("Fields.Total", SortDirection.Descending)
            .DetailPageBreak(PageBreak.End)
            .Build().Definition;

        def.Detail.NoRowsMessage.Should().Be("Sem dados.");
        def.Detail.FilterExpression.Should().Be("Fields.Total > 0");
        def.Detail.SortExpressions.Should().HaveCount(2);
        def.Detail.SortExpressions[1].Direction.Should().Be(SortDirection.Descending);
        def.Detail.PageBreak.Should().Be(PageBreak.End);
    }

    [Fact]
    public void Band_page_break_propagates_to_report_band()
    {
        var def = ReportBuilder.Create("test")
            .ReportFooter(f => f.PageBreak(PageBreak.Start).Label("Resumo Final"))
            .Detail(d => d.Text("{Fields.Total}").At(0, 0).Size(50, 6))
            .Build().Definition;

        def.ReportFooter.Should().NotBeNull();
        def.ReportFooter!.PageBreak.Should().Be(PageBreak.Start);
    }

    [Fact]
    public void Data_source_calculated_field_filter_and_sort_are_attached()
    {
        var def = ReportBuilder.Create("test")
            .DataSource("Pedidos", new[] { new Pedido(1, "Ana", 100m) })
            .CalculatedField("Imposto", "Fields.Total * 0.18")
            .DataSourceFilter("Fields.Total > 0")
            .DataSourceSortBy("Fields.Cliente")
            .Detail(d => d.Text("{Fields.Imposto}").At(0, 0).Size(50, 6))
            .Build().Definition;

        var ds = def.DataSources[0];
        ds.CalculatedFields.Should().HaveCount(1);
        ds.CalculatedFields[0].Name.Should().Be("Imposto");
        ds.FilterExpression.Should().Be("Fields.Total > 0");
        ds.SortExpressions.Should().ContainSingle();
    }

    [Fact]
    public void Group_page_break_filter_sort_and_variables_are_attached()
    {
        var def = ReportBuilder.Create("test")
            .DataSource("Pedidos", new[] { new Pedido(1, "Ana", 100m) })
            .Detail(d => d.Text("{Fields.Total}").At(0, 0).Size(50, 6))
            .Group("PorCliente", "Fields.Cliente", g => g
                .Header(h => h.Label("Cliente").At(0, 0).Size(50, 6))
                .PageBreak(PageBreak.Between)
                .Filter("Sum(Fields.Total) > 100")
                .SortBy("Fields.Cliente")
                .Variable("Subtotal", "Sum(Fields.Total)"))
            .Build().Definition;

        var group = def.Groups[0];
        group.PageBreak.Should().Be(PageBreak.Between);
        group.FilterExpression.Should().Be("Sum(Fields.Total) > 100");
        group.SortExpressions.Should().ContainSingle();
        group.Variables.Should().ContainSingle();
        group.Variables[0].Scope.Should().Be(VariableScope.Group);
    }
}
