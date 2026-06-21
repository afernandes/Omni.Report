using FluentAssertions;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Data;
using Reporting.DataSources;
using Reporting.DataSources.Enumerable;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Reporting.Paper;
using Xunit;

namespace Reporting.Layout.Tests;

/// <summary>Covers <see cref="DetailBand.DataSetName"/>: the band can pin which dataset drives the detail
/// loop. The default (null) is purely additive — the engine keeps the historical
/// <c>PrimaryDataSource ?? first-declared</c> resolution.</summary>
public class DetailBandDataSetTests
{
    private sealed record Fruta(string nome);
    private sealed record Cor(string nome);

    private static async Task<List<string>> RenderNames(string? dataSetName, string? primaryDataSource)
    {
        var registry = new DataSourceRegistry();
        // "Frutas" is declared FIRST (the historical default driver), "Cores" second.
        registry.Register(new EnumerableDataSource<Fruta>("Frutas", [new Fruta("maçã"), new Fruta("uva")]));
        registry.Register(new EnumerableDataSource<Cor>("Cores", [new Cor("azul"), new Cor("verde"), new Cor("rosa")]));

        var detail = new DetailBand(Unit.FromMm(6),
            new EquatableArray<ReportElement>([
                new TextBoxElement { Id = "t", Expression = "Fields.nome", Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(50), Unit.FromMm(6)) },
            ]),
            DataSetName: dataSetName);
        var def = new ReportDefinition("DS", PageSetup.A4Portrait, detail)
        {
            DataSources = new EquatableArray<DataSourceDefinition>([
                new DataSourceDefinition("Frutas"),
                new DataSourceDefinition("Cores"),
            ]),
        };
        var report = await new ReportPaginator().PaginateAsync(
            new PaginationRequest { Definition = def, DataSources = registry, PrimaryDataSource = primaryDataSource });
        return report.Pages.SelectMany(p => p.Primitives).OfType<DrawTextPrimitive>().Select(t => t.Text).ToList();
    }

    [Fact]
    public async Task Null_DataSetName_keeps_the_first_declared_source_as_driver()
    {
        // Regression: the additive default must behave exactly as before — first declared source ("Frutas").
        var names = await RenderNames(dataSetName: null, primaryDataSource: null);
        names.Should().BeEquivalentTo("maçã", "uva");
    }

    [Fact]
    public async Task DataSetName_pins_a_non_first_source_as_the_detail_driver()
    {
        var names = await RenderNames(dataSetName: "Cores", primaryDataSource: null);
        names.Should().BeEquivalentTo("azul", "verde", "rosa");
    }

    [Fact]
    public async Task DataSetName_wins_over_the_request_PrimaryDataSource()
    {
        // Band is more specific than the host request: even with PrimaryDataSource=Frutas, the band drives Cores.
        var names = await RenderNames(dataSetName: "Cores", primaryDataSource: "Frutas");
        names.Should().BeEquivalentTo("azul", "verde", "rosa");
    }

    [Fact]
    public async Task Null_DataSetName_still_honours_the_request_PrimaryDataSource()
    {
        // Without a band dataset, the request's primary wins over first-declared — unchanged behaviour.
        var names = await RenderNames(dataSetName: null, primaryDataSource: "Cores");
        names.Should().BeEquivalentTo("azul", "verde", "rosa");
    }
}
