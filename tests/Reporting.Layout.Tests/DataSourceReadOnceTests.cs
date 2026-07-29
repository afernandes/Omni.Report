using System.Runtime.CompilerServices;
using FluentAssertions;
using Reporting;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Data;
using Reporting.DataSources;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Paper;
using Xunit;

namespace Reporting.Layout.Tests;

/// <summary>
/// Each registered data source must be enumerated EXACTLY ONCE per pagination. The paginator used to read
/// the primary source twice — once to fill the all-sources snapshot (for sub-detail bands and qualified
/// <c>Fields.Source.X</c>) and again to build the Detail iteration — which meant a SQL-backed report issued
/// the same query twice, four times for master-detail, and held two copies of the same rows in memory.
/// </summary>
public class DataSourceReadOnceTests
{
    /// <summary>Data source that counts how many times it is enumerated and how many rows it hands out.</summary>
    private sealed class CountingSource(string name, IReadOnlyList<(int Id, string Text)> rows) : IReportDataSource
    {
        public int ReadCalls { get; private set; }
        public int RowsYielded { get; private set; }

        public string Name => name;

        public IReportRecordSchema Schema { get; } = new ReportRecordSchema(
            [new ReportField("Id", typeof(int)), new ReportField("Text", typeof(string))]);

        public async IAsyncEnumerable<IReportRecord> ReadAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ReadCalls++;
            foreach (var (id, text) in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RowsYielded++;
                yield return new DictionaryRecord(Schema,
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["Id"] = id, ["Text"] = text });
                await Task.Yield();
            }
        }
    }

    private static CountingSource Source(string name, int count) =>
        new(name, Enumerable.Range(1, count).Select(i => (i, $"linha {i}")).ToArray());

    private static TextBoxElement Field(string expr) => new()
    {
        Expression = expr,
        Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(60), Unit.FromMm(5)),
    };

    [Fact]
    public async Task A_single_source_report_reads_its_source_once()
    {
        var src = Source("D", 40);
        var registry = new DataSourceRegistry();
        registry.Register(src);

        var def = new ReportDefinition("um", PageSetup.A4Portrait,
            new DetailBand(Unit.FromMm(6), new EquatableArray<ReportElement>([Field("Fields.Text")]), DataSetName: "D"))
        {
            DataSources = EquatableArray.Create(new DataSourceDefinition("D")),
        };

        var rendered = await new ReportPaginator().PaginateAsync(
            new PaginationRequest { Definition = def, DataSources = registry });

        src.ReadCalls.Should().Be(1, "reading twice means two SQL queries / two HTTP calls for one report");
        src.RowsYielded.Should().Be(40, "and two copies of every row in memory");
        rendered.Pages.Should().NotBeEmpty();
    }

    [Fact]
    public async Task A_two_pass_report_still_reads_its_source_once()
    {
        // Page.Total forces a SECOND layout pass. That pass must replay the already-materialised rows, never
        // hit the data source again.
        var src = Source("D", 30);
        var registry = new DataSourceRegistry();
        registry.Register(src);

        var def = new ReportDefinition("dois-passes", PageSetup.A4Portrait,
            new DetailBand(Unit.FromMm(6), new EquatableArray<ReportElement>([Field("Fields.Text")]), DataSetName: "D"))
        {
            DataSources = EquatableArray.Create(new DataSourceDefinition("D")),
            PageFooter = new ReportBand(BandKind.PageFooter, Unit.FromMm(8),
                new EquatableArray<ReportElement>([Field("{Page.Number} de {Page.Total}")])),
        };

        var rendered = await new ReportPaginator().PaginateAsync(
            new PaginationRequest { Definition = def, DataSources = registry });

        src.ReadCalls.Should().Be(1, "the second pass replays the materialised rows, it does not re-query");
        rendered.Pages.Should().NotBeEmpty();
    }

    [Fact]
    public async Task A_master_detail_report_reads_each_source_once()
    {
        var parent = Source("Pais", 5);
        var child = Source("Filhos", 20);
        var registry = new DataSourceRegistry();
        registry.Register(parent);
        registry.Register(child);

        var def = new ReportDefinition("master-detail", PageSetup.A4Portrait,
            new DetailBand(Unit.FromMm(6), new EquatableArray<ReportElement>([Field("Fields.Text")]), DataSetName: "Pais"))
        {
            DataSources = EquatableArray.Create(
                new DataSourceDefinition("Pais",
                    Relations: EquatableArray.Create(
                        new DataRelation("rel", "Pais", "Id", "Filhos", "Id"))),
                new DataSourceDefinition("Filhos")),
        };

        await new ReportPaginator().PaginateAsync(
            new PaginationRequest { Definition = def, DataSources = registry });

        parent.ReadCalls.Should().Be(1, "the parent was read twice before: snapshot + iteration");
        child.ReadCalls.Should().Be(1, "and so was the child, for four reads across two sources");
    }
}
