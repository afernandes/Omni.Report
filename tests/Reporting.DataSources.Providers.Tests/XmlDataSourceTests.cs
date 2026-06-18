using FluentAssertions;
using Reporting.DataSources.Xml;
using Xunit;

namespace Reporting.DataSources.Providers.Tests;

/// <summary>
/// Behavioural tests for <see cref="XmlDataSource"/>. Covers (a) RowsXPath selection,
/// (b) automatic column discovery from attributes vs elements vs both, (c) explicit
/// per-column XPaths, (d) namespace handling for namespaced XML (atom, RSS-like), and
/// (e) type coercion via <see cref="TypeInference"/> applied to XML text values.
/// </summary>
public class XmlDataSourceTests
{
    private const string OrdersXml = """
        <?xml version="1.0"?>
        <orders>
          <order id="1" customer="Ana"><total>100.50</total><paid>true</paid></order>
          <order id="2" customer="Beto"><total>50.00</total><paid>false</paid></order>
        </orders>
        """;

    [Fact]
    public async Task Rows_xpath_selects_row_nodes()
    {
        var ds = new XmlDataSource("Test", new XmlDataSourceOptions
        {
            InlineXml = OrdersXml,
            RowsXPath = "/orders/order",
            Discovery = XmlColumnDiscovery.Both,
        });
        var rows = await ds.ReadAsync().ToListAsync();
        rows.Should().HaveCount(2);
    }

    [Fact]
    public async Task Discovery_attributes_picks_attribute_columns()
    {
        var ds = new XmlDataSource("Test", new XmlDataSourceOptions
        {
            InlineXml = OrdersXml,
            RowsXPath = "/orders/order",
            Discovery = XmlColumnDiscovery.Attributes,
        });
        var rows = await ds.ReadAsync().ToListAsync();
        rows[0]["id"].Should().Be(1);          // coerced to int
        rows[0]["customer"].Should().Be("Ana");
        // <total> and <paid> live as child elements — Attributes mode shouldn't see them.
        ds.Schema.Fields.Select(f => f.Name).Should().NotContain("total");
    }

    [Fact]
    public async Task Discovery_elements_picks_child_element_columns()
    {
        var ds = new XmlDataSource("Test", new XmlDataSourceOptions
        {
            InlineXml = OrdersXml,
            RowsXPath = "/orders/order",
            Discovery = XmlColumnDiscovery.Elements,
        });
        var rows = await ds.ReadAsync().ToListAsync();
        rows[0]["total"].Should().Be(100.5);
        rows[0]["paid"].Should().Be(true);
        // Attributes shouldn't appear in elements-only mode.
        ds.Schema.Fields.Select(f => f.Name).Should().NotContain("id");
    }

    [Fact]
    public async Task Discovery_both_picks_attributes_and_elements()
    {
        var ds = new XmlDataSource("Test", new XmlDataSourceOptions
        {
            InlineXml = OrdersXml,
            RowsXPath = "/orders/order",
            Discovery = XmlColumnDiscovery.Both,
        });
        var rows = await ds.ReadAsync().ToListAsync();
        rows[0]["id"].Should().Be(1);
        rows[0]["customer"].Should().Be("Ana");
        rows[0]["total"].Should().Be(100.5);
        rows[0]["paid"].Should().Be(true);
    }

    [Fact]
    public async Task Explicit_column_xpaths_extract_arbitrary_values()
    {
        // Explicit XPaths support computed columns — e.g. concat of multiple attributes.
        var ds = new XmlDataSource("Test", new XmlDataSourceOptions
        {
            InlineXml = OrdersXml,
            RowsXPath = "/orders/order",
            ColumnXPaths = new Dictionary<string, string>
            {
                ["display"] = "concat(@customer, ' - ', total)",
                ["doubled"] = "number(total) * 2",
            },
        });
        var rows = await ds.ReadAsync().ToListAsync();
        rows[0]["display"].Should().Be("Ana - 100.50");
        rows[0]["doubled"].Should().Be(201.0);
    }

    [Fact]
    public async Task Numeric_text_values_coerce_to_double()
    {
        var ds = new XmlDataSource("Test", new XmlDataSourceOptions
        {
            InlineXml = OrdersXml,
            RowsXPath = "/orders/order",
            Discovery = XmlColumnDiscovery.Elements,
        });
        _ = await ds.ReadAsync().ToListAsync();
        ds.Schema.Fields.First(f => f.Name == "total").Type.Should().Be(typeof(double));
    }

    [Fact]
    public async Task Atom_feed_with_namespace_resolves_via_registered_prefix()
    {
        var atom = """
            <?xml version="1.0" encoding="utf-8"?>
            <feed xmlns="http://www.w3.org/2005/Atom">
              <entry><title>Hello</title><id>1</id></entry>
              <entry><title>World</title><id>2</id></entry>
            </feed>
            """;
        var ds = new XmlDataSource("Atom", new XmlDataSourceOptions
        {
            InlineXml = atom,
            RowsXPath = "//a:entry",
            Discovery = XmlColumnDiscovery.Elements,
            Namespaces = new Dictionary<string, string> { ["a"] = "http://www.w3.org/2005/Atom" },
        });
        var rows = await ds.ReadAsync().ToListAsync();
        rows.Should().HaveCount(2);
        rows[1]["title"].Should().Be("World");
        rows[1]["id"].Should().Be(2);
    }

    [Fact]
    public async Task File_source_reads_disk_file()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, OrdersXml);
            var ds = new XmlDataSource("Test", new XmlDataSourceOptions
            {
                FilePath = path,
                RowsXPath = "/orders/order",
                Discovery = XmlColumnDiscovery.Attributes,
            });
            var rows = await ds.ReadAsync().ToListAsync();
            rows.Should().HaveCount(2);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
