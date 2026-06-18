namespace Reporting.Output.Markdown;

/// <summary>Knobs for <see cref="MarkdownExporter"/>.</summary>
public sealed record MarkdownExportOptions
{
    /// <summary>If set, emitted as <c># Title</c> at the top of the document. Falls back to
    /// <see cref="Reporting.Layout.RenderedReport.Name"/> when <c>null</c>.</summary>
    public string? Title { get; init; }

    /// <summary>If true, prepends a YAML front-matter block with <c>title</c> and
    /// <c>generated</c> fields — handy for Jekyll/Hugo/Docusaurus pipelines. Default false.</summary>
    public bool IncludeFrontMatter { get; init; } = false;

    /// <summary>If true, group-header rows are promoted to <c>## H2</c> sections breaking the
    /// table into multiple tables. If false, group headers become single-cell rows with bold
    /// text. Default true.</summary>
    public bool PromoteGroupHeaders { get; init; } = true;

    /// <summary>If true, subtotal/total rows are formatted bold via <c>**…**</c>. Default true.</summary>
    public bool BoldTotals { get; init; } = true;

    /// <summary>Line ending. Default <c>\n</c> (Unix/GitHub).</summary>
    public string LineEnding { get; init; } = "\n";

    public static readonly MarkdownExportOptions Default = new();
}
