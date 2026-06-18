namespace Reporting.Viewer.Blazor;

/// <summary>Configuration for the <see cref="ReportViewer"/> component.</summary>
public sealed record ReportViewerOptions
{
    /// <summary>Pixel density used by the Skia renderer that produces the page bitmaps.
    /// 96 dpi matches the typical browser scaling and keeps page images reasonably-sized;
    /// 144 / 192 give crisper rendering at the cost of bandwidth.</summary>
    public float RenderDpi { get; init; } = 96f;

    /// <summary>Initial zoom level in percent (100 = 1:1 pixels). Min 25, max 400.</summary>
    public int InitialZoomPercent { get; init; } = 100;

    /// <summary>Allowed zoom presets shown on the toolbar dropdown.</summary>
    public int[] ZoomPresets { get; init; } = [25, 50, 75, 100, 125, 150, 200, 300, 400];

    /// <summary>Show the toolbar (navigation, zoom, export, print). Default true.</summary>
    public bool ShowToolbar { get; init; } = true;

    /// <summary>Show the page selector input. Default true.</summary>
    public bool ShowPageSelector { get; init; } = true;

    /// <summary>Show the export buttons (PDF / XLSX). Default true.</summary>
    public bool ShowExport { get; init; } = true;

    /// <summary>Show the print button. Default true; requires an <c>IReportPrinter</c>
    /// registered in DI when the user clicks it.</summary>
    public bool ShowPrint { get; init; } = true;

    public static readonly ReportViewerOptions Default = new();
}
