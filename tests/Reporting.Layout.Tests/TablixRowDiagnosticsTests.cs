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
/// The model persists <see cref="TablixCell.RowSpan"/>, cells beyond the two template rows of a flat table and any
/// number of cells in a matrix, but the renderer cannot draw them all. These tests pin that the loss is reported
/// through <see cref="RenderedReport.Diagnostics"/> instead of producing a grid that silently differs from the
/// definition.
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
    public async Task PaginateAsync_MatrizComSegundaMedida_EmiteDiagnosticoORL025()
    {
        var report = await PaginateMatrix(
            new TablixCell(0, 0, new LabelElement { Text = "Cor" }),
            new TablixCell(1, 1, new TextBoxElement { Id = "qtd", Expression = "Sum(Fields.Tamanho)" }),
            new TablixCell(1, 2, new TextBoxElement { Id = "total", Expression = "Count(Fields.Nome)" }));

        var diagnostic = report.Diagnostics.Should()
            .ContainSingle(d => d.Code == "ORL025" && d.Feature == "Tablix[Vendas].MatrixCells").Subject;
        diagnostic.Message.Should().Contain("1 célula", "the message says how much is missing");
    }

    [Fact]
    public async Task PaginateAsync_MatrizComCabecalhoDeColunaFixo_EmiteDiagnosticoORL025()
    {
        // Column headers come from the group values; a template label in row 0 is never drawn.
        var report = await PaginateMatrix(
            new TablixCell(0, 1, new LabelElement { Text = "Ano" }),
            new TablixCell(1, 1, new TextBoxElement { Id = "qtd", Expression = "Sum(Fields.Tamanho)" }));

        report.Diagnostics.Should().ContainSingle(d => d.Feature == "Tablix[Vendas].MatrixCells");
    }

    [Fact]
    public async Task PaginateAsync_MatrizComCantoEUmaMedida_NaoEmiteDiagnostico()
    {
        var report = await PaginateMatrix(
            new TablixCell(0, 0, new LabelElement { Text = "Cor" }),
            new TablixCell(1, 1, new TextBoxElement { Id = "qtd", Expression = "Sum(Fields.Tamanho)" }));

        report.Diagnostics.Should().BeEmpty("the corner and one body cell are exactly what the matrix draws");
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

    // Row groups by name, column groups by colour: one group on each axis is what makes a Tablix a matrix.
    private static Task<RenderedReport> PaginateMatrix(params TablixCell[] cells)
        => new ReportPaginator().PaginateAsync(Request(Definition(cells, matrix: true)));

    private static ReportDefinition Definition(params TablixCell[] cells) => Definition(cells, matrix: false);

    private static ReportDefinition Definition(TablixCell[] cells, bool matrix)
    {
        var tablix = new TablixElement
        {
            Id = "t",
            Name = "Vendas",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 80.Mm(), 16.Mm()),
            Cells = new EquatableArray<TablixCell>(cells),
            RowGroups = matrix ? new EquatableArray<TablixGroup>([new TablixGroup("Nome", "Fields.Nome")]) : EquatableArray<TablixGroup>.Empty,
            ColumnGroups = matrix ? new EquatableArray<TablixGroup>([new TablixGroup("Cor", "Fields.Cor")]) : EquatableArray<TablixGroup>.Empty,
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
