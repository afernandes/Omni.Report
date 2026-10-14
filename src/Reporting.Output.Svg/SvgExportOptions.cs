namespace Reporting.Output.Svg;

/// <summary>Knobs for <see cref="SvgExporter"/>.</summary>
public sealed record SvgExportOptions
{
    /// <summary>Document <c>&lt;title&gt;</c>. When <c>null</c>, falls back to
    /// <see cref="Reporting.Layout.RenderedReport.Name"/>.</summary>
    public string? Title { get; init; }

    /// <summary>If true, each page draws its own white (or <see cref="PageBackgroundColor"/>)
    /// fill before the primitives — handy for previews against a non-white body.
    /// Default <c>true</c>.</summary>
    public bool IncludeBackground { get; init; } = true;

    /// <summary>Page background fill color in <c>#RRGGBB</c> form. Default <c>#FFFFFF</c>.</summary>
    public string PageBackgroundColor { get; init; } = "#FFFFFF";

    /// <summary>Vertical gap (in points) between pages in the multi-page composite document.
    /// Has no effect when the report has a single page. Default 16pt (≈ 5.6mm).</summary>
    public float PageGap { get; init; } = 16f;

    public static readonly SvgExportOptions Default = new();
}
