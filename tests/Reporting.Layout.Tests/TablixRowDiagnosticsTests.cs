using FluentAssertions;
using Reporting.Bands;
using Reporting.Common;
using Reporting.DataSources;
using Reporting.DataSources.Enumerable;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Paper;
using Xunit;

namespace Reporting.Layout.Tests;

/// <summary>
/// The model persists <see cref="TablixCell.RowSpan"/> and cells beyond the two template rows, but the renderer
/// cannot draw them. These tests pin that the loss is reported through <see cref="RenderedReport.Diagnostics"/>
/// instead of producing a grid that silently differs from the definition.
/// </summary>
public sealed class TablixRowDiagnosticsTests
{
    [Fact]
    public async Task PaginateAsync_CelulaComRowSpan_EmiteDiagnosticoORL025()
    {
        var report = await Paginate(
            new TablixCell(0, 0, new LabelElement { Text = "Nome" }, RowSpan: 2),
            new TablixCell(1, 0, new TextBoxElement { Id = "a", Expression = "Fields.Nome" }));

        report.Diagnostics.Should().ContainSingle(d => d.Code == "ORL025" && d.Feature == "Tablix[Vendas].RowSpan");
    }

    [Fact]
    public async Task PaginateAsync_TabelaComCelulaAlemDaLinhaDeDetalhe_EmiteDiagnosticoORL025()
    {
        var report = await Paginate(
            new TablixCell(0, 0, new LabelElement { Text = "Nome" }),
            new TablixCell(1, 0, new TextBoxElement { Id = "a", Expression = "Fields.Nome" }),
            new TablixCell(2, 0, new LabelElement { Text = "Rodapé" }));

        report.Diagnostics.Should().ContainSingle(d => d.Code == "ORL025" && d.Feature == "Tablix[Vendas].TemplateRows");
    }

    [Fact]
    public async Task PaginateAsync_TabelaSoComCabecalhoEDetalhe_NaoEmiteDiagnostico()
    {
        var report = await Paginate(
            new TablixCell(0, 0, new LabelElement { Text = "Resumo" }, ColumnSpan: 2),
            new TablixCell(1, 0, new TextBoxElement { Id = "a", Expression = "Fields.Nome" }),
            new TablixCell(1, 1, new TextBoxElement { Id = "b", Expression = "Fields.Cor" }));

        report.Diagnostics.Should().BeEmpty("ColumnSpan and the two template rows are fully rendered");
    }

    [Fact]
    public async Task PaginateAsync_RowSpanDentroDeSubrelatorioInline_EmiteDiagnostico()
    {
        var inner = Definition(
            new TablixCell(0, 0, new LabelElement { Text = "Nome" }, RowSpan: 3),
            new TablixCell(1, 0, new TextBoxElement { Id = "a", Expression = "Fields.Nome" }));
        var subreport = new SubreportElement
        {
            Id = "sub",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 80.Mm(), 16.Mm()),
            InlineDefinition = inner,
        };
        var outer = new ReportDefinition("externo", PageSetup.A4Portrait,
            new DetailBand(20.Mm(), new EquatableArray<ReportElement>(new ReportElement[] { subreport })));

        var report = await new ReportPaginator().PaginateAsync(Request(outer));

        report.Diagnostics.Should().Contain(d => d.Feature == "Tablix[Vendas].RowSpan");
    }

    private static Task<RenderedReport> Paginate(params TablixCell[] cells)
        => new ReportPaginator().PaginateAsync(Request(Definition(cells)));

    private static ReportDefinition Definition(params TablixCell[] cells)
    {
        var tablix = new TablixElement
        {
            Id = "t",
            Name = "Vendas",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 80.Mm(), 16.Mm()),
            Cells = new EquatableArray<TablixCell>(cells),
        };
        return new ReportDefinition("e", PageSetup.A4Portrait,
            new DetailBand(20.Mm(), new EquatableArray<ReportElement>(new ReportElement[] { tablix })));
    }

    private static PaginationRequest Request(ReportDefinition definition)
    {
        var registry = new DataSourceRegistry();
        registry.Register(new EnumerableDataSource<StyledRow>("Dados", [new StyledRow("Ana", "azul", 1)]));
        return new PaginationRequest { Definition = definition, DataSources = registry };
    }
}
