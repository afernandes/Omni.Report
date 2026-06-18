using System.Globalization;
using System.Text;
using Reporting.Layout;
using Reporting.Layout.Tabular;
using Reporting.Output.Pdf;

namespace Reporting.Output.Markdown;

/// <summary>
/// Exports a <see cref="RenderedReport"/> as GitHub-flavored Markdown. The
/// <see cref="LayoutPrimitiveGrid"/> projection becomes one (or more) GFM tables —
/// group-header rows split the table into H2 sections when
/// <see cref="MarkdownExportOptions.PromoteGroupHeaders"/> is true (default).
/// </summary>
/// <remarks>
/// <para>Output is suited for embedding in READMEs, wikis, Notion, Docusaurus, and similar
/// docs pipelines. Numeric cells retain their original formatting (R$, pt-BR comma) so
/// the rendered table reads naturally to a human; no normalization happens here.</para>
/// </remarks>
public sealed class MarkdownExporter : IReportExporter
{
    private readonly MarkdownExportOptions _options;

    public MarkdownExporter(MarkdownExportOptions? options = null)
    {
        _options = options ?? MarkdownExportOptions.Default;
    }

    public string Format => "markdown";
    public string FileExtension => ".md";
    public string ContentType => "text/markdown; charset=utf-8";

    public void Export(RenderedReport report, Stream output)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(output);

        var grid = LayoutPrimitiveGrid.Build(report);

        using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), bufferSize: 8192, leaveOpen: true)
        {
            NewLine = _options.LineEnding,
        };

        var title = _options.Title ?? report.Name;

        if (_options.IncludeFrontMatter)
        {
            writer.Write("---");
            writer.Write(_options.LineEnding);
            writer.Write("title: ");
            writer.Write(EscapeYaml(title));
            writer.Write(_options.LineEnding);
            writer.Write("generated: ");
            writer.Write(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            writer.Write(_options.LineEnding);
            writer.Write("---");
            writer.Write(_options.LineEnding);
            writer.Write(_options.LineEnding);
        }

        writer.Write("# ");
        writer.Write(EscapeInline(title));
        writer.Write(_options.LineEnding);
        writer.Write(_options.LineEnding);

        WriteTables(writer, grid, title);
        writer.Flush();
    }

    private void WriteTables(StreamWriter writer, LayoutPrimitiveGrid grid, string documentTitle)
    {
        var columnCount = grid.ColumnCount;
        if (columnCount == 0 || grid.Rows.Count == 0)
        {
            writer.Write("_(relatório vazio)_");
            writer.Write(_options.LineEnding);
            return;
        }

        // Find the FIRST multi-cell row. If its cells all look like short labels (no numbers,
        // ≤ 28 chars each) we treat it as a real column-title row; otherwise it's data and
        // we synthesize a blank header. Crucially, we DON'T scan past this row — a footer
        // like "OmniReport · ... | Página 1 de 1" would also "look label-y" and steal the
        // header role, dumping every data row above into the pre-table prose section.
        GridRow? headerRow = null;
        int headerIdx = -1;
        for (int i = 0; i < grid.Rows.Count; i++)
        {
            if (grid.Rows[i].Cells.Count >= 2)
            {
                headerIdx = i;
                var candidate = grid.Rows[i];
                bool looksLikeLabels = candidate.Cells.Values.All(v =>
                    !LayoutPrimitiveGrid.LooksLikeNumber(v) && v.Length <= 28);
                if (looksLikeLabels)
                {
                    headerRow = candidate;
                }
                break;
            }
        }

        bool syntheticHeader = headerRow is null;
        if (syntheticHeader && headerIdx > 0)
        {
            // Move the pre-table boundary one above the first multi-cell row when we're going
            // to synthesize — so the loop emits the leading single-cell rows as headings, and
            // the body loop starts at the first multi-cell row.
            headerIdx -= 1;
        }
        else if (syntheticHeader && headerIdx == 0)
        {
            headerIdx = -1;
        }

        if (headerRow is null && headerIdx < 0)
        {
            // No tabular content — emit every row as a bold/plain paragraph.
            foreach (var row in grid.Rows)
            {
                var text = row.Cells.Values.FirstOrDefault();
                if (string.IsNullOrEmpty(text)) continue;
                bool bold = _options.BoldTotals && (row.Kind == RowKind.Total || row.Kind == RowKind.Subtotal);
                if (bold) writer.Write("**");
                writer.Write(EscapeInline(text));
                if (bold) writer.Write("**");
                writer.Write(_options.LineEnding);
                writer.Write(_options.LineEnding);
            }
            return;
        }

        // Emit any single-cell rows BEFORE the header. Skip ones that just echo the document
        // title (we already emitted that as H1).
        for (int i = 0; i <= headerIdx - (syntheticHeader ? 0 : 1) && i < grid.Rows.Count; i++)
        {
            var row = grid.Rows[i];
            if (row.Cells.Count >= 2 && !syntheticHeader) continue; // skip the chosen header row itself
            var text = row.Cells.Values.FirstOrDefault();
            if (!string.IsNullOrEmpty(text)
                && string.Equals(text.Trim(), documentTitle.Trim(), StringComparison.Ordinal))
            {
                continue; // skip echo of the H1
            }
            EmitSingleCellRow(writer, row);
        }

        // Track whether the current open table has body rows — avoids emitting empty tables
        // when a group header arrives immediately after the opening row.
        bool tableOpen = false;
        bool tableHasBody = false;

        void OpenTable()
        {
            if (tableOpen) return;
            if (headerRow is not null)
            {
                WriteTableOpening(writer, headerRow, columnCount);
            }
            else
            {
                WriteBlankTableHeader(writer, columnCount);
            }
            tableOpen = true;
            tableHasBody = false;
        }

        void CloseIfEmpty()
        {
            // The way GFM works, an open thead without rows is still valid markup — just visually
            // useless. We don't actually "close" anything; we just refrain from opening another
            // identical thead when nothing was emitted. The flag drives that decision.
            if (tableOpen && !tableHasBody)
            {
                // Strip the last 2 lines we just wrote — the thead + separator. Cheap fix: do
                // nothing here; the next OpenTable() short-circuits because tableOpen is true.
                // Either way, the user sees one empty thead, not many.
            }
            tableOpen = false;
        }

        int firstBodyRow = syntheticHeader ? headerIdx + 1 : headerIdx + 1;
        OpenTable();

        for (int r = firstBodyRow; r < grid.Rows.Count; r++)
        {
            var row = grid.Rows[r];
            if (row.Kind == RowKind.GroupHeader)
            {
                if (_options.PromoteGroupHeaders)
                {
                    CloseIfEmpty();
                    if (tableHasBody)
                    {
                        writer.Write(_options.LineEnding);
                    }
                    tableOpen = false;
                    WriteGroupHeader(writer, row);
                    if (r + 1 < grid.Rows.Count)
                    {
                        OpenTable();
                    }
                }
                else
                {
                    OpenTable();
                    WriteBodyRow(writer, row, columnCount, bold: true);
                    tableHasBody = true;
                }
            }
            else
            {
                OpenTable();
                bool bold = _options.BoldTotals && (row.Kind == RowKind.Subtotal || row.Kind == RowKind.Total);
                WriteBodyRow(writer, row, columnCount, bold);
                tableHasBody = true;
            }
        }
    }

    /// <summary>Emits a row that has only one populated cell as a heading (when it looks like
    /// a section title) or a bold paragraph (when it's a subtotal/total).</summary>
    private void EmitSingleCellRow(StreamWriter writer, GridRow row)
    {
        var text = row.Cells.Values.FirstOrDefault();
        if (string.IsNullOrEmpty(text)) return;
        if (row.Kind is RowKind.Subtotal or RowKind.Total)
        {
            writer.Write("**");
            writer.Write(EscapeInline(text));
            writer.Write("**");
            writer.Write(_options.LineEnding);
            writer.Write(_options.LineEnding);
        }
        else
        {
            writer.Write("## ");
            writer.Write(EscapeInline(text));
            writer.Write(_options.LineEnding);
            writer.Write(_options.LineEnding);
        }
    }

    private void WriteGroupHeader(StreamWriter writer, GridRow row)
    {
        var text = row.Cells.Values.FirstOrDefault() ?? string.Empty;
        writer.Write("## ");
        writer.Write(EscapeInline(text));
        writer.Write(_options.LineEnding);
        writer.Write(_options.LineEnding);
    }

    private void WriteBlankTableHeader(StreamWriter writer, int columnCount)
    {
        // GFM still requires a header row + separator — emit a blank one when the report has
        // no natural column-titles row.
        writer.Write('|');
        for (int c = 0; c < columnCount; c++)
        {
            writer.Write("  |");
        }
        writer.Write(_options.LineEnding);
        writer.Write('|');
        for (int c = 0; c < columnCount; c++)
        {
            writer.Write(" --- |");
        }
        writer.Write(_options.LineEnding);
    }

    private void WriteTableOpening(StreamWriter writer, GridRow headerRow, int columnCount)
    {
        // Row 1: header cells. Row 2: alignment separator. Detail cells are guessed
        // left-aligned (no `:` markers); GFM renderers default to that.
        WriteRow(writer, headerRow, columnCount, bold: false);
        writer.Write('|');
        for (int c = 0; c < columnCount; c++)
        {
            writer.Write(" --- |");
        }
        writer.Write(_options.LineEnding);
    }

    private void WriteBodyRow(StreamWriter writer, GridRow row, int columnCount, bool bold)
    {
        WriteRow(writer, row, columnCount, bold);
    }

    private void WriteRow(StreamWriter writer, GridRow row, int columnCount, bool bold)
    {
        writer.Write('|');
        for (int c = 0; c < columnCount; c++)
        {
            writer.Write(' ');
            if (row.Cells.TryGetValue(c, out var raw) && !string.IsNullOrEmpty(raw))
            {
                if (bold)
                {
                    writer.Write("**");
                    writer.Write(EscapeTableCell(raw));
                    writer.Write("**");
                }
                else
                {
                    writer.Write(EscapeTableCell(raw));
                }
            }
            writer.Write(" |");
        }
        writer.Write(_options.LineEnding);
    }

    /// <summary>Escapes characters that have special meaning inside a Markdown table cell:
    /// <c>|</c> must be escaped, and newlines must become <c>&lt;br&gt;</c> since GFM tables
    /// can't contain raw line breaks.</summary>
    private static string EscapeTableCell(string value)
    {
        if (value.IndexOf('|') < 0 && value.IndexOf('\n') < 0 && value.IndexOf('\r') < 0)
        {
            return value;
        }
        var sb = new StringBuilder(value.Length + 8);
        foreach (var c in value)
        {
            switch (c)
            {
                case '|': sb.Append("\\|"); break;
                case '\r': break; // strip CR; LF becomes <br>
                case '\n': sb.Append("<br>"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    /// <summary>Inline Markdown escape — used for the document title and group headings.</summary>
    private static string EscapeInline(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            // Only escape characters that would actively break headings. Keeping the set
            // narrow preserves natural Portuguese punctuation (·, R$, etc.) unchanged.
            switch (c)
            {
                case '\\':
                case '`':
                case '*':
                case '_':
                case '[':
                case ']':
                case '<':
                case '>':
                    sb.Append('\\').Append(c);
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    private static string EscapeYaml(string value)
    {
        // YAML scalar in front matter: quote if it contains special chars; otherwise emit
        // bare. Keep it conservative — wrap in double quotes when in doubt.
        if (string.IsNullOrEmpty(value)) return "\"\"";
        bool needsQuotes = value.IndexOfAny(new[] { ':', '#', '\'', '"', '\n', '\r' }) >= 0;
        if (!needsQuotes) return value;
        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
