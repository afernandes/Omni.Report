using FluentAssertions;
using Reporting.DataSources;
using Reporting.DataSources.Json;
using Xunit;

namespace Reporting.DataSources.Providers.Tests;

/// <summary>
/// End-to-end behavioural tests for <see cref="JsonDataSource"/>. The contract: the
/// source must (a) accept JSON in three shapes (inline string, file, URL — we cover
/// inline + file here; URL is exercised via <see cref="WebServiceDataSourceTests"/>),
/// (b) navigate to a nested array via <see cref="JsonDataSourceOptions.RootPath"/>,
/// (c) infer column types correctly (int / double / bool / string / DateTime), and
/// (d) yield records that resolve fields case-insensitively. Failures here indicate a
/// regression visible to any report bound to a JSON file.
/// </summary>
public class JsonDataSourceTests
{
    [Fact]
    public async Task Root_array_yields_one_record_per_element()
    {
        var json = """[{"id":1,"name":"Ana"},{"id":2,"name":"Beto"}]""";
        var ds = new JsonDataSource("Test", new JsonDataSourceOptions { InlineJson = json });
        var rows = await ds.ReadAsync().ToListAsync();
        rows.Should().HaveCount(2);
        rows[0]["name"].Should().Be("Ana");
        rows[1]["id"].Should().Be(2);
    }

    [Fact]
    public async Task Nested_root_path_locates_the_array()
    {
        var json = """{"data":{"results":[{"a":1},{"a":2}]}}""";
        var ds = new JsonDataSource("Test", new JsonDataSourceOptions
        {
            InlineJson = json,
            RootPath = "data.results",
        });
        var rows = await ds.ReadAsync().ToListAsync();
        rows.Should().HaveCount(2);
        rows[1]["a"].Should().Be(2);
    }

    [Fact]
    public async Task Bracket_index_navigates_into_array()
    {
        // data.groups[0].rows — a property, then an array index, then a property.
        var json = """
            {"data":{"groups":[
                {"name":"A","rows":[{"v":1},{"v":2}]},
                {"name":"B","rows":[{"v":3}]}
            ]}}
            """;
        var ds = new JsonDataSource("Test", new JsonDataSourceOptions
        {
            InlineJson = json,
            RootPath = "data.groups[0].rows",
        });
        var rows = await ds.ReadAsync().ToListAsync();
        rows.Should().HaveCount(2);
        rows[0]["v"].Should().Be(1);
        rows[1]["v"].Should().Be(2);
    }

    [Fact]
    public async Task Index_only_segment_selects_array_element()
    {
        // Root is an array of arrays; "[1]" picks the second inner array.
        var json = """[[{"a":1}],[{"a":2},{"a":3}]]""";
        var ds = new JsonDataSource("Test", new JsonDataSourceOptions
        {
            InlineJson = json,
            RootPath = "[1]",
        });
        var rows = await ds.ReadAsync().ToListAsync();
        rows.Should().HaveCount(2);
        rows[1]["a"].Should().Be(3);
    }

    [Fact]
    public async Task Out_of_range_index_throws()
    {
        var json = """{"items":[{"a":1}]}""";
        var ds = new JsonDataSource("Test", new JsonDataSourceOptions
        {
            InlineJson = json,
            RootPath = "items[5]",
        });
        var act = async () => await ds.ReadAsync().ToListAsync();
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*out of range*");
    }

    [Fact]
    public async Task Index_on_non_array_throws()
    {
        var json = """{"obj":{"a":1}}""";
        var ds = new JsonDataSource("Test", new JsonDataSourceOptions
        {
            InlineJson = json,
            RootPath = "obj[0]",
        });
        var act = async () => await ds.ReadAsync().ToListAsync();
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not an Array*");
    }

    [Fact]
    public async Task Field_lookup_is_case_insensitive()
    {
        var json = """[{"Total":100}]""";
        var ds = new JsonDataSource("Test", new JsonDataSourceOptions { InlineJson = json });
        var row = (await ds.ReadAsync().ToListAsync())[0];
        row["total"].Should().Be(100);
        row["TOTAL"].Should().Be(100);
    }

    [Fact]
    public async Task Schema_infers_int_double_bool_string_types()
    {
        var json = """[{"n":1,"d":2.5,"b":true,"s":"hi"}]""";
        var ds = new JsonDataSource("Test", new JsonDataSourceOptions { InlineJson = json });
        _ = await ds.ReadAsync().ToListAsync(); // populate schema
        var schema = ds.Schema;
        schema.Fields.First(f => f.Name == "n").Type.Should().Be(typeof(int));
        schema.Fields.First(f => f.Name == "d").Type.Should().Be(typeof(double));
        schema.Fields.First(f => f.Name == "b").Type.Should().Be(typeof(bool));
        schema.Fields.First(f => f.Name == "s").Type.Should().Be(typeof(string));
    }

    [Fact]
    public async Task Mixed_int_and_double_widens_to_double()
    {
        // RDL-style: a column with mixed numeric kinds widens to the largest one so
        // expressions like Sum(Fields.price) don't lose fractional cents.
        var json = """[{"price":10},{"price":12.5}]""";
        var ds = new JsonDataSource("Test", new JsonDataSourceOptions { InlineJson = json });
        _ = await ds.ReadAsync().ToListAsync();
        ds.Schema.Fields.First(f => f.Name == "price").Type.Should().Be(typeof(double));
    }

    [Fact]
    public async Task Nested_object_flattens_with_dot_notation()
    {
        var json = """[{"name":"Ana","address":{"city":"SP","zip":"01000"}}]""";
        var ds = new JsonDataSource("Test", new JsonDataSourceOptions { InlineJson = json });
        var row = (await ds.ReadAsync().ToListAsync())[0];
        row["address.city"].Should().Be("SP");
        row["address.zip"].Should().Be("01000");
    }

    [Fact]
    public async Task Single_object_root_yields_one_record()
    {
        var json = """{"id":42,"name":"Solo"}""";
        var ds = new JsonDataSource("Test", new JsonDataSourceOptions { InlineJson = json });
        var rows = await ds.ReadAsync().ToListAsync();
        rows.Should().ContainSingle();
        rows[0]["name"].Should().Be("Solo");
    }

    [Fact]
    public async Task File_source_reads_disk_file()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, """[{"x":1}]""");
            var ds = new JsonDataSource("Test", new JsonDataSourceOptions { FilePath = path });
            var rows = await ds.ReadAsync().ToListAsync();
            rows.Should().ContainSingle();
            rows[0]["x"].Should().Be(1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Constructor_rejects_zero_or_multiple_sources()
    {
        var empty = () => new JsonDataSource("X", new JsonDataSourceOptions());
        empty.Should().Throw<ArgumentException>();
        var both = () => new JsonDataSource("X", new JsonDataSourceOptions { InlineJson = "[]", FilePath = "x" });
        both.Should().Throw<ArgumentException>();
    }
}

/// <summary>Convenience extension — turns an async sequence into a list (test code only).</summary>
internal static class AsyncEnumerableExtensions
{
    public static async Task<List<IReportRecord>> ToListAsync(
        this IAsyncEnumerable<IReportRecord> source, CancellationToken ct = default)
    {
        var list = new List<IReportRecord>();
        await foreach (var item in source.WithCancellation(ct)) list.Add(item);
        return list;
    }
}
