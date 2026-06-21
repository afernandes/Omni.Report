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

    /// <summary>If true (default), chart/visual elements (vector primitives the Word table can't hold) are
    /// rasterised to inline PNG images. When false they're omitted (the legacy text-only behaviour).</summary>
    public bool RasterizeVisuals { get; init; } = true;

    /// <summary>Resolution for rasterised visuals. ~150 balances sharpness vs file size for print.</summary>
    public float RasterizeDpi { get; init; } = 150f;

    public static readonly DocxExportOptions Default = new();
}
