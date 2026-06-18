using System.Globalization;
using System.Text;
using Reporting.Layout;
using Reporting.Layout.Tabular;
using Reporting.Output.Pdf;

namespace Reporting.Output.Csv;

/// <summary>
/// Exports a <see cref="RenderedReport"/> to RFC 4180 CSV. Reuses the same
/// <see cref="LayoutPrimitiveGrid"/> that powers the XLSX exporter — every text primitive is
/// clustered into rows and columns, then emitted line-by-line.
/// </summary>
/// <remarks>
/// <para>Rows of kind <see cref="RowKind.GroupHeader"/> (single wide text) are still
/// emitted, but in a single cell — downstream tools can filter them out by checking for
/// blank trailing columns.</para>
///
/// <para>Numeric cells in detail rows are normalized to invariant culture
/// (<c>1234.56</c>) when <see cref="CsvExportOptions.NormalizeNumbers"/> is true, regardless
/// of how they were formatted in the source report (R$, pt-BR comma, etc.). Subtotal/Total
/// rows preserve the original formatted text since they often carry units like "R$" inline.</para>
/// </remarks>
public sealed class CsvExporter : IReportExporter
{
    private readonly CsvExportOptions _options;

    public CsvExporter(CsvExportOptions? options = null)
    {
        _options = options ?? CsvExportOptions.Default;
    }

    public string Format => "csv";
    public string FileExtension => ".csv";
    public string ContentType => "text/csv; charset=utf-8";

    public void Export(RenderedReport report, Stream output)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(output);

        var grid = LayoutPrimitiveGrid.Build(report);

        if (_options.IncludeBom)
        {
            var bom = Encoding.UTF8.GetPreamble();
            output.Write(bom, 0, bom.Length);
        }

        using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), bufferSize: 8192, leaveOpen: true)
        {
            NewLine = _options.LineEnding,
        };

        var columnCount = grid.ColumnCount;
        for (int r = 0; r < grid.Rows.Count; r++)
        {
            var row = grid.Rows[r];
            for (int c = 0; c < columnCount; c++)
            {
                if (c > 0)
                {
                    writer.Write(_options.Delimiter);
                }
                if (row.Cells.TryGetValue(c, out var raw))
                {
                    writer.Write(FormatCell(raw, row.Kind));
                }
                // else: empty cell → emit nothing between delimiters
            }
            writer.Write(_options.LineEnding);
        }
        writer.Flush();
    }

    private string FormatCell(string value, RowKind rowKind)
    {
        // Detail rows: prefer normalized numeric output so downstream parsers don't need
        // culture-specific decimal handling.
        string body;
        if (_options.NormalizeNumbers
            && rowKind == RowKind.Detail
            && LayoutPrimitiveGrid.TryParseDecimal(value) is decimal d)
        {
            body = d.ToString("0.##########", InvariantNumberFormat(_options.DecimalSeparator));
        }
        else
        {
            body = value;
        }

        return EscapeCsv(body);
    }

    private string EscapeCsv(string value)
    {
        // RFC 4180: a field must be quoted when it contains the delimiter, a double quote, CR,
        // LF, or leading/trailing whitespace. Quotes inside are doubled.
        if (string.IsNullOrEmpty(value))
        {
            return _options.QuoteAllFields ? "\"\"" : string.Empty;
        }

        bool needsQuotes = _options.QuoteAllFields
            || value.IndexOf(_options.Delimiter) >= 0
            || value.IndexOf('"') >= 0
            || value.IndexOf('\r') >= 0
            || value.IndexOf('\n') >= 0
            || char.IsWhiteSpace(value[0])
            || char.IsWhiteSpace(value[^1]);

        if (!needsQuotes)
        {
            return value;
        }

        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (var c in value)
        {
            if (c == '"')
            {
                sb.Append("\"\"");
            }
            else
            {
                sb.Append(c);
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    /// <summary>Creates a culture-invariant <see cref="NumberFormatInfo"/> with a custom decimal
    /// separator (so callers can pick <c>.</c> or <c>,</c> without touching thousand grouping).</summary>
    private static NumberFormatInfo InvariantNumberFormat(char decimalSeparator)
    {
        var fmt = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        fmt.NumberDecimalSeparator = decimalSeparator.ToString();
        fmt.NumberGroupSeparator = string.Empty;
        return fmt;
    }
}
