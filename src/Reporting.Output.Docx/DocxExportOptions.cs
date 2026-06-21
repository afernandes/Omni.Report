namespace Reporting.Output.Docx;

/// <summary>Options for the Word (.docx) exporter. Mirrors <c>ExcelExportOptions</c> where it makes sense.</summary>
public sealed record DocxExportOptions
{
    /// <summary>Document title embedded in the package properties; defaults to the report's name.</summary>
    public string? Title { get; init; }

    /// <summary>Document author embedded in the package properties.</summary>
    public string? Author { get; init; }

    /// <summary>If true (default), a heading paragraph with the report name precedes the table.</summary>
    public bool IncludeHeading { get; init; } = true;

    public static readonly DocxExportOptions Default = new();
}
