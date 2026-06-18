namespace Reporting.Output.Html;

/// <summary>Optional metadata + styling knobs for <see cref="SvgHtmlExporter"/>.</summary>
public sealed record HtmlExportOptions
{
    /// <summary>Document <c>&lt;title&gt;</c>. Defaults to <see cref="Reporting.Layout.RenderedReport.Name"/>.</summary>
    public string? Title { get; init; }

    /// <summary>Language attribute on <c>&lt;html lang="…"&gt;</c>. Defaults to <c>"pt-BR"</c>.</summary>
    public string Language { get; init; } = "pt-BR";

    /// <summary>Background color of the body (around the pages). Default = paper-cream.</summary>
    public string BodyBackground { get; init; } = "#F4F2EC";

    /// <summary>Background color of each page card. Default = white.</summary>
    public string PageBackground { get; init; } = "#FFFFFF";

    /// <summary>If true, wraps each page in a <c>box-shadow</c> card. Disable for clean printing
    /// or when the consumer wants to apply their own page chrome.</summary>
    public bool DropShadow { get; init; } = true;

    /// <summary>If true, emits a CSS <c>@page</c> rule sized to the first page so the browser's
    /// "Save as PDF" / Ctrl+P prints page-for-page without scaling. Default true.</summary>
    public bool EmitPrintRules { get; init; } = true;

    /// <summary>If true, the resulting HTML is a single self-contained document (no external
    /// assets). The current implementation is always self-contained — flag reserved for
    /// future variants that split assets out.</summary>
    public bool SelfContained { get; init; } = true;

    public static readonly HtmlExportOptions Default = new();
}
