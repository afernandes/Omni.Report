namespace Reporting.Output.Csv;

/// <summary>Knobs for <see cref="CsvExporter"/>.</summary>
public sealed record CsvExportOptions
{
    /// <summary>Field separator. Default is <c>,</c> (RFC 4180). Use <c>;</c> for European
    /// Excel where comma is the decimal separator.</summary>
    public char Delimiter { get; init; } = ',';

    /// <summary>Line ending. Default <c>\r\n</c> (RFC 4180). Use <c>\n</c> for Unix-friendly
    /// pipelines.</summary>
    public string LineEnding { get; init; } = "\r\n";

    /// <summary>If true, prepends UTF-8 BOM (<c>EF BB BF</c>) — required by Excel on Windows
    /// for it to detect UTF-8 in CSV with non-ASCII characters (R$, acentos). Default true.</summary>
    public bool IncludeBom { get; init; } = true;

    /// <summary>If true, every field is quoted. Default false — only fields containing the
    /// delimiter, quote, newline, or leading/trailing whitespace are quoted.</summary>
    public bool QuoteAllFields { get; init; } = false;

    /// <summary>If true, numeric cells are output as <c>123.45</c> (invariant culture) so
    /// downstream tools can parse them directly. If false, the raw cell text from the report
    /// is preserved (which may use pt-BR comma decimal). Default true.</summary>
    public bool NormalizeNumbers { get; init; } = true;

    /// <summary>Decimal separator emitted when <see cref="NormalizeNumbers"/> is true.
    /// Default <c>.</c>.</summary>
    public char DecimalSeparator { get; init; } = '.';

    public static readonly CsvExportOptions Default = new();
}
