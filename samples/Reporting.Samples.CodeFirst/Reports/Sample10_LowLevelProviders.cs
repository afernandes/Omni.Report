using Reporting.DataSources;
using Reporting.DataSources.FileSystem;
using Reporting.DataSources.Json;
using Reporting.DataSources.Xml;

namespace Reporting.Samples.CodeFirst.Reports;

/// <summary>
/// Low-level examples that DON'T use the <see cref="Reporting.CodeFirst.ReportBuilder"/>
/// fluent API. They show the bare <see cref="IReportDataSource"/> contract — useful
/// when you need to drive your own dashboard, dump rows to a console, or feed an
/// arbitrary consumer that isn't a band-based report. Every provider implements the
/// same async-streaming interface, so the consumer code is identical across them.
/// </summary>
public static class Sample10_LowLevelProviders
{
    /// <summary>Runs every provider against its sample data and prints a header + the
    /// first three rows. The output is printed to the console — handy for verifying
    /// the providers are wired correctly without launching the Designer.</summary>
    public static async Task RunAsync(TextWriter writer, CancellationToken ct = default)
    {
        // 1) JSON — file source, nested root path. The schema is populated on first
        //    iteration; we materialise the rows lazily and snapshot just the first three
        //    so a huge JSON file doesn't blow up the demo.
        var json = new JsonDataSource("Pedidos", new JsonDataSourceOptions
        {
            FilePath = Sample06_JsonPedidos.ResolveSamplePath("pedidos.json"),
            RootPath = "data.results",
        });
        await DumpAsync(writer, json, "JSON · pedidos.json (data.results)", ct);

        // 2) XML — RSS feed; element-discovery mode picks child tags as columns.
        var rss = new XmlDataSource("Posts", new XmlDataSourceOptions
        {
            FilePath = Sample06_JsonPedidos.ResolveSamplePath("rss-feed.xml"),
            RowsXPath = "/rss/channel/item",
            Discovery = XmlColumnDiscovery.Elements,
        });
        await DumpAsync(writer, rss, "XML · rss-feed.xml (RSS items)", ct);

        // 3) XML — attribute discovery on the produtos catalog. Demonstrates that the
        //    same parser handles both attribute-heavy AND element-heavy documents
        //    just by changing the Discovery mode.
        var prods = new XmlDataSource("Produtos", new XmlDataSourceOptions
        {
            FilePath = Sample06_JsonPedidos.ResolveSamplePath("produtos.xml"),
            RowsXPath = "/catalogo/produto",
            Discovery = XmlColumnDiscovery.Both,
        });
        await DumpAsync(writer, prods, "XML · produtos.xml (id+categoria attrs + child elements)", ct);

        // 4) FileSystem — listing of the binary output directory. Shows the fixed
        //    10-column schema and the parent/child id linking that lets reports
        //    build tree views.
        var fs = new FileSystemDataSource("Files", new FileSystemDataSourceOptions
        {
            RootDirectory = AppContext.BaseDirectory,
            FilePattern = "*.dll",
            Recursive = false,
            IncludeDirectories = false,
        });
        await DumpAsync(writer, fs, "FileSystem · " + AppContext.BaseDirectory + " (*.dll)", ct);
    }

    /// <summary>Dumps a provider's first three rows to <paramref name="writer"/>.
    /// Generic over <see cref="IReportDataSource"/> — works for every provider since
    /// the contract is the same.</summary>
    private static async Task DumpAsync(TextWriter writer, IReportDataSource source, string title, CancellationToken ct)
    {
        await writer.WriteLineAsync("─── " + title + " ─────────────────────────");
        // Trigger schema population by calling ReadAsync once. We bound the dump to
        // three rows to keep the console output legible; iterate without bound for a
        // full dump.
        int count = 0;
        await foreach (var record in source.ReadAsync(ct))
        {
            if (count == 0)
            {
                // Header line — schema is now populated by the first ReadAsync iteration.
                var header = string.Join("  ", source.Schema.Fields.Select(f => $"{f.Name}:{f.Type.Name}"));
                await writer.WriteLineAsync("  schema: " + header);
            }
            var values = string.Join(" | ", source.Schema.Fields.Select(f => Format(record[f.Name])));
            await writer.WriteLineAsync($"  row {count + 1}: " + values);
            count++;
            if (count >= 3) break;
        }
        if (count == 0) await writer.WriteLineAsync("  (no rows)");
        await writer.WriteLineAsync(string.Empty);
    }

    private static string Format(object? v) => v switch
    {
        null => "—",
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm"),
        string s when s.Length > 30 => s[..27] + "…",
        _ => v.ToString() ?? string.Empty,
    };
}
