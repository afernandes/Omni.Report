using System.Globalization;
using System.Text.RegularExpressions;
using Reporting.Geometry;
using Reporting.Layout.Primitives;

namespace Reporting.Layout.Tabular;

/// <summary>Semantic classification assigned to each row by the grid quantizer.</summary>
public enum RowKind
{
    /// <summary>The first row, typically column titles.</summary>
    Header,
    /// <summary>A regular data row.</summary>
    Detail,
    /// <summary>A single-column wide row that looks like a group title (no numbers, &gt; 3 chars).</summary>
    GroupHeader,
    /// <summary>A row containing "subtotal" / "sum" keywords, not the last row.</summary>
    Subtotal,
    /// <summary>A row containing "total" keywords or the last "subtotal" row.</summary>
    Total,
}

/// <summary>One row in a <see cref="LayoutPrimitiveGrid"/>, addressed by column index.</summary>
public sealed class GridRow
{
    /// <summary>Cell text keyed by column index. Sparse — missing keys = blank cells.</summary>
    public Dictionary<int, string> Cells { get; } = new();

    /// <summary>The absolute Y coordinate (across pages) of the source text cluster — used by
    /// the quantizer for clustering, exposed for ordering / debugging.</summary>
    public Unit Y { get; set; }

    /// <summary>Row classification — drives header bold, total formulas, group header color etc.</summary>
    public RowKind Kind { get; set; } = RowKind.Detail;
}

/// <summary>
/// A 2D grid reconstructed from a <see cref="RenderedReport"/>'s <see cref="DrawTextPrimitive"/>s
/// by clustering their X / Y coordinates into columns and rows. This is the cell-based
/// projection that powers tabular exporters (XLSX, CSV, Markdown).
/// </summary>
/// <remarks>
/// <para>Heuristic — not a perfect cell extraction. Works very well for reports that already
/// have a tabular structure (data rows with consistent X for each column). Less ideal for
/// free-form documents, where it still produces a usable grid but with sparse rows.</para>
///
/// <para>Y-clustering tolerance: 2.5mm (≈100 mils). Anything within that vertical band
/// collapses into the same row.</para>
///
/// <para>X-clustering tolerance: 3.8mm (≈150 mils). New columns are inserted preserving
/// X-sort; existing rows get their cells shifted right when a column is inserted before them.</para>
/// </remarks>
public sealed partial class LayoutPrimitiveGrid
{
    /// <summary>Absolute X coordinates (in mils) of each column anchor, in left-to-right order.</summary>
    public List<int> ColumnXs { get; } = [];

    /// <summary>Rows in top-to-bottom order across all pages.</summary>
    public List<GridRow> Rows { get; } = [];

    /// <summary>Total number of columns.</summary>
    public int ColumnCount => ColumnXs.Count;

    /// <summary>Builds a grid from every text primitive in <paramref name="report"/>, clustering
    /// across all pages with cumulative Y offsets so rows don't collide.</summary>
    public static LayoutPrimitiveGrid Build(RenderedReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var grid = new LayoutPrimitiveGrid();

        // Phase 1: collect all non-empty text primitives with absolute Y (across pages).
        var entries = new List<(Unit Y, Unit X, string Text)>();
        Unit pageOffset = Unit.Zero;
        foreach (var page in report.Pages)
        {
            foreach (var p in page.Primitives.OfType<DrawTextPrimitive>())
            {
                if (string.IsNullOrWhiteSpace(p.Text))
                {
                    continue;
                }
                entries.Add((p.Bounds.Y + pageOffset, p.Bounds.X, p.Text));
            }
            pageOffset += page.PageSetup.PageHeight;
        }

        // Phase 2: cluster into rows by Y (tolerance ≈ 2.5mm), and columns by X (≈ 3.8mm).
        const int yToleranceMils = 100;
        foreach (var entry in entries.OrderBy(e => e.Y.Mils).ThenBy(e => e.X.Mils))
        {
            var existing = grid.Rows.LastOrDefault(r => Math.Abs(r.Y.Mils - entry.Y.Mils) <= yToleranceMils);
            GridRow row;
            if (existing is null)
            {
                row = new GridRow { Y = entry.Y };
                grid.Rows.Add(row);
            }
            else
            {
                row = existing;
            }
            int colIndex = AssignColumn(grid, entry.X);
            row.Cells[colIndex] = entry.Text;
        }

        ClassifyRows(grid);
        return grid;
    }

    private static int AssignColumn(LayoutPrimitiveGrid grid, Unit x)
    {
        const int xToleranceMils = 150;
        for (int i = 0; i < grid.ColumnXs.Count; i++)
        {
            if (Math.Abs(grid.ColumnXs[i] - x.Mils) <= xToleranceMils)
            {
                return i;
            }
        }
        // Insert preserving X-sort.
        int insert = 0;
        while (insert < grid.ColumnXs.Count && grid.ColumnXs[insert] < x.Mils)
        {
            insert++;
        }
        grid.ColumnXs.Insert(insert, x.Mils);
        // Existing rows are keyed by column index — shift everything ≥ insert by one.
        if (insert < grid.ColumnXs.Count - 1)
        {
            foreach (var row in grid.Rows)
            {
                var shifted = new Dictionary<int, string>();
                foreach (var kv in row.Cells)
                {
                    shifted[kv.Key >= insert ? kv.Key + 1 : kv.Key] = kv.Value;
                }
                row.Cells.Clear();
                foreach (var kv in shifted)
                {
                    row.Cells[kv.Key] = kv.Value;
                }
            }
        }
        return insert;
    }

    private static void ClassifyRows(LayoutPrimitiveGrid grid)
    {
        if (grid.Rows.Count == 0)
        {
            return;
        }
        grid.Rows[0].Kind = RowKind.Header;
        for (int i = 1; i < grid.Rows.Count; i++)
        {
            var row = grid.Rows[i];
            var values = row.Cells.Values;
            if (values.Any(v => SubtotalPattern().IsMatch(v)))
            {
                row.Kind = i == grid.Rows.Count - 1 ? RowKind.Total : RowKind.Subtotal;
            }
            else if (values.Count == 1 && values.First().Length > 3 && !LooksLikeNumber(values.First()))
            {
                row.Kind = RowKind.GroupHeader;
            }
        }
    }

    /// <summary>True when the value parses as a decimal under pt-BR (comma decimal) or invariant
    /// (dot decimal) conventions, after stripping the <c>R$</c> currency prefix and whitespace.</summary>
    public static bool LooksLikeNumber(string text) => TryParseDecimal(text) is not null;

    /// <summary>Attempts to extract a numeric value from the cell. Returns <c>null</c> when the
    /// cell is text-only. Used by exporters to decide between text and numeric output.</summary>
    public static decimal? TryParseDecimal(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        var cleaned = text.Replace("R$", "", StringComparison.Ordinal)
                          .Replace(" ", "", StringComparison.Ordinal)
                          .Trim();
        if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.GetCultureInfo("pt-BR"), out var v1))
        {
            return v1;
        }
        if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var v2))
        {
            return v2;
        }
        return null;
    }

    /// <summary>Cheap heuristic: does the cell text carry a currency hint? Used by tabular
    /// exporters that distinguish between numeric and monetary columns.</summary>
    public static bool LooksLikeCurrency(string text)
        => text.Contains("R$", StringComparison.Ordinal)
        || text.Contains("$", StringComparison.Ordinal);

    [GeneratedRegex(@"\b(subtotal|total|sum)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex SubtotalPattern();
}
