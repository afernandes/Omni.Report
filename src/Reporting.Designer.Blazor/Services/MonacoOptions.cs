namespace Reporting.Designer.Blazor.Services;

/// <summary>Where the Designer loads the Monaco editor (expression and SQL editors) from. Register with
/// <see cref="ServiceCollectionExtensions.AddOmniReportMonaco"/>; without it the copy packaged with the Designer is
/// used, which works offline and under a Content-Security-Policy that allows only <c>'self'</c>.</summary>
public sealed class MonacoOptions
{
    /// <summary>URL of a Monaco <c>min/vs</c> folder — absolute, or relative to the page's base URL — to load
    /// instead of the packaged copy, for example a newer Monaco served by the host. <see langword="null"/> uses the
    /// packaged copy. A folder on another origin needs <c>worker-src data:</c> in the CSP (see docs/designer.md). If
    /// it cannot be loaded within 15 seconds, the editors fall back to a plain text box.</summary>
    public string? BaseUrl { get; set; }
}
