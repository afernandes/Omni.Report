using FluentAssertions;
using Reporting;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Data;
using Reporting.DataSources;
using Reporting.DataSources.Enumerable;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Xunit;

namespace Reporting.Layout.Tests;

/// <summary>
/// Behavioral tests for the RDL Phase 1 features in the paginator: NoRowsMessage,
/// FilterExpression, SortExpressions (at data-source and Detail levels), CalculatedField,
/// and PageBreak. These tests render a report with realistic data and assert the rendered
/// output respects the configured RDL semantics — proving end-to-end that the round-trip
/// fields actually drive paginator behaviour, not just serialize.
/// </summary>
public class RdlBehaviorTests
{
    [Fact]
    public async Task NoRowsMessage_is_emitted_when_detail_has_zero_rows()
    {
        // Empty data source → NoRowsMessage must appear in place of the Detail band.
        // We verify by counting the text primitives that contain the configured message.
        var def = ReportDefinition.Empty("EmptyTest") with
        {
            DataSources = EquatableArray.Create(new DataSourceDefinition("Vendas")),
            Detail = new DetailBand(
                Unit.FromMm(6), EquatableArray<ReportElement>.Empty,
                NoRowsMessage: "Sem registros para mostrar."),
        };
        var req = TestData.BuildRequest(def, Array.Empty<Venda>());
        var report = await new ReportPaginator().PaginateAsync(req);
        var allText = string.Join("\n", report.Pages
            .SelectMany(p => p.Primitives)
            .OfType<DrawTextPrimitive>()
            .Select(t => t.Text));
        allText.Should().Contain("Sem registros para mostrar.");
    }

    [Fact]
    public async Task FilterExpression_skips_rows_that_dont_match()
    {
        // Filter: only rows with Total > 10 should render.
        // 3 rows total, 2 pass the filter (Caderno 25, Caneta 10 fails because >, not >=).
        var def = ReportDefinition.Empty("FilterTest") with
        {
            DataSources = EquatableArray.Create(new DataSourceDefinition("Vendas")),
            Detail = new DetailBand(
                Unit.FromMm(6),
                EquatableArray.Create<ReportElement>(new TextBoxElement
                {
                    Id = "row",
                    Bounds = new Rectangle(0.Mm(), 0.Mm(), 100.Mm(), Unit.FromMm(6)),
                    Expression = "{Fields.Produto}",
                }),
                FilterExpression: "Fields.Total > 10"),
        };
        var req = TestData.BuildRequest(def, TestData.ThreeRows());
        var report = await new ReportPaginator().PaginateAsync(req);
        var emitted = report.Pages.SelectMany(p => p.Primitives).OfType<DrawTextPrimitive>().Count();
        emitted.Should().Be(1); // only the Caderno row (Total=25) passes Total>10
    }

    [Fact]
    public async Task SortExpression_orders_rows_descending_by_total()
    {
        // Sort: descending by Total. The first emitted row should be the largest (25),
        // the second the medium (10), the third the smallest (5).
        var def = ReportDefinition.Empty("SortTest") with
        {
            DataSources = EquatableArray.Create(new DataSourceDefinition("Vendas")),
            Detail = new DetailBand(
                Unit.FromMm(6),
                EquatableArray.Create<ReportElement>(new TextBoxElement
                {
                    Id = "row",
                    Bounds = new Rectangle(0.Mm(), 0.Mm(), 100.Mm(), Unit.FromMm(6)),
                    Expression = "{Fields.Produto}",
                }),
                SortExpressions: EquatableArray.Create(
                    new SortDescriptor("Fields.Total", SortDirection.Descending))),
        };
        var req = TestData.BuildRequest(def, TestData.ThreeRows());
        var report = await new ReportPaginator().PaginateAsync(req);
        var texts = report.Pages.SelectMany(p => p.Primitives)
            .OfType<DrawTextPrimitive>().Select(t => t.Text).ToList();
        // Caderno (25) → Caneta-Ana (10) → Caneta-Beto (5)
        texts.Should().Equal("Caderno", "Caneta", "Caneta");
    }

    [Fact]
    public async Task CalculatedField_evaluates_per_row_and_is_visible_to_expressions()
    {
        // Calculated field "Dobro" = Fields.Total * 2. A TextBox referencing
        // {Fields.Dobro} should emit the doubled value for each row.
        //
        // NOTE: CalculatedField.Expression follows the RDL convention — it is a raw
        // expression (no template braces). Template syntax with {expr:fmt} is used at
        // the *consuming* level (TextBoxElement.Expression), where the template engine
        // expands {Fields.Dobro} by looking up the calculated field's value.
        var ds = new DataSourceDefinition("Vendas",
            CalculatedFields: EquatableArray.Create(
                new CalculatedField("Dobro", "Fields.Total * 2")));
        var def = ReportDefinition.Empty("CalcTest") with
        {
            DataSources = EquatableArray.Create(ds),
            Detail = new DetailBand(
                Unit.FromMm(6),
                EquatableArray.Create<ReportElement>(new TextBoxElement
                {
                    Id = "row",
                    Bounds = new Rectangle(0.Mm(), 0.Mm(), 100.Mm(), Unit.FromMm(6)),
                    Expression = "{Fields.Dobro}",
                })),
        };
        var req = TestData.BuildRequest(def, TestData.ThreeRows());
        var report = await new ReportPaginator().PaginateAsync(req);
        var texts = report.Pages.SelectMany(p => p.Primitives)
            .OfType<DrawTextPrimitive>().Select(t => t.Text).ToList();
        // 10*2=20, 25*2=50, 5*2=10
        texts.Should().Equal("20", "50", "10");
    }

    [Fact]
    public async Task Group_PageBreak_Between_inserts_break_between_group_instances()
    {
        // Group by Cliente with PageBreak.Between → each cliente gets its own page.
        // Three rows: Ana, Ana, Beto. Two cliente instances → at least 2 pages.
        var def = TestData.GroupedReport();
        var grouped = def.Groups[0] with { PageBreak = PageBreak.Between };
        def = def with { Groups = EquatableArray.Create(grouped) };
        var req = TestData.BuildRequest(def, TestData.ThreeRows());
        var report = await new ReportPaginator().PaginateAsync(req);
        report.Pages.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Detail_PageBreak_End_inserts_break_after_last_row()
    {
        // Detail.PageBreak.End → after rendering all rows, the next band (ReportFooter
        // or end-of-report) lives on a new page. With 3 rows and no report footer we
        // expect at least 2 pages (the second one might be empty save for the closing
        // page footer).
        var def = TestData.GroupedReport() with { Groups = EquatableArray<GroupBand>.Empty };
        def = def with { Detail = def.Detail with { PageBreak = PageBreak.End } };
        var req = TestData.BuildRequest(def, TestData.ThreeRows());
        var report = await new ReportPaginator().PaginateAsync(req);
        report.Pages.Count.Should().BeGreaterThanOrEqualTo(2);
    }
}
