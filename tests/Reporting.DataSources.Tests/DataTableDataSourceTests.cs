using System.Data;
using FluentAssertions;
using Reporting.DataSources;
using Xunit;

namespace Reporting.DataSources.Tests;

public class DataTableDataSourceTests
{
    private static DataTable Sample()
    {
        var dt = new DataTable("Vendas");
        dt.Columns.Add("Cliente", typeof(string));
        dt.Columns.Add("Total", typeof(decimal));
        dt.Rows.Add("Ana", 100m);
        dt.Rows.Add("Beto", 250m);
        dt.Rows.Add("Carla", DBNull.Value);
        return dt;
    }

    [Fact]
    public async Task Reads_rows_and_translates_dbnull_to_null()
    {
        var ds = new DataTableDataSource("Vendas", Sample());
        var list = new List<IReportRecord>();
        await foreach (var r in ds.ReadAsync())
        {
            list.Add(r);
        }
        list.Should().HaveCount(3);
        list[0]["Cliente"].Should().Be("Ana");
        list[2]["Total"].Should().BeNull();
    }

    [Fact]
    public async Task Reads_by_ordinal()
    {
        var ds = new DataTableDataSource("Vendas", Sample());
        await using var en = ds.ReadAsync().GetAsyncEnumerator();
        (await en.MoveNextAsync()).Should().BeTrue();
        en.Current[0].Should().Be("Ana");
        en.Current[99].Should().BeNull();
    }

    [Fact]
    public void Schema_reflects_columns()
    {
        var ds = new DataTableDataSource("Vendas", Sample());
        ds.Schema.Fields.Select(f => f.Name).Should().BeEquivalentTo(["Cliente", "Total"]);
    }
}
