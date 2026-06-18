using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Reporting.Layout;
using Reporting.Output.Excel;
using Reporting.Output.Pdf;
using Reporting.Printing;
using Reporting.Rendering.Skia;

namespace Reporting.Viewer.Blazor;

/// <summary>
/// Razor component that displays a paginated <see cref="RenderedReport"/>, exposes a
/// navigation/zoom toolbar, and lets the user export PDF/XLSX or trigger printing via
/// any <see cref="IReportPrinter"/> registered in DI.
/// </summary>
public partial class ReportViewer : ComponentBase, IDisposable
{
    [Inject] private ReportViewerOptions GlobalOptions { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private IServiceProvider Services { get; set; } = default!;

    /// <summary>The paginated report to display. When null the viewer shows an empty state.</summary>
    [Parameter] public RenderedReport? Report { get; set; }

    /// <summary>Optional per-instance options. Falls back to the DI-registered defaults.</summary>
    [Parameter] public ReportViewerOptions? Options { get; set; }

    /// <summary>Optional callback invoked when the user clicks Print and an
    /// <see cref="IReportPrinter"/> is not registered in DI.</summary>
    [Parameter] public EventCallback OnPrintRequestedWithoutDriver { get; set; }

    /// <summary>Optional explicit printer override (otherwise resolved from DI).</summary>
    [Parameter] public IReportPrinter? Printer { get; set; }

    /// <summary>Optional explicit PrintOptions used when the toolbar print button is clicked.
    /// If null, defaults to printing to the "Microsoft Print to PDF" virtual printer.</summary>
    [Parameter] public PrintOptions? PrintOptions { get; set; }

    /// <summary>Optional event callback raised after every successful export.</summary>
    [Parameter] public EventCallback<ReportExportEventArgs> OnExported { get; set; }

    // ── State ───────────────────────────────────────────────────────────────────

    private readonly List<string> _pageImages = [];
    private int _currentPageIndex;
    private int _zoomPercent = 100;
    private bool _busy;
    private string? _lastError;
    private RenderedReport? _lastReport;

    private ReportViewerOptions ResolvedOptions => Options ?? GlobalOptions;

    private int PageCount => _pageImages.Count;

    private int ZoomBinding
    {
        get => _zoomPercent;
        set => _zoomPercent = Clamp(value);
    }

    // ── Lifecycle ───────────────────────────────────────────────────────────────

    protected override void OnInitialized()
    {
        _zoomPercent = Clamp(ResolvedOptions.InitialZoomPercent);
    }

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_lastReport, Report))
        {
            _lastReport = Report;
            _currentPageIndex = 0;
            _lastError = null;
            RenderPages();
        }
    }

    // ── Navigation ──────────────────────────────────────────────────────────────

    private Task FirstPageAsync() => GoToPageAsync(0);
    private Task PreviousPageAsync() => GoToPageAsync(_currentPageIndex - 1);
    private Task NextPageAsync() => GoToPageAsync(_currentPageIndex + 1);
    private Task LastPageAsync() => GoToPageAsync(PageCount - 1);

    private Task GoToPageAsync(int index)
    {
        if (PageCount == 0)
        {
            return Task.CompletedTask;
        }
        var clamped = Math.Clamp(index, 0, PageCount - 1);
        if (clamped != _currentPageIndex)
        {
            _currentPageIndex = clamped;
            StateHasChanged();
        }
        return Task.CompletedTask;
    }

    private async Task OnPageNumberChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var oneBased))
        {
            await GoToPageAsync(oneBased - 1);
        }
    }

    // ── Zoom ────────────────────────────────────────────────────────────────────

    private void ZoomIn() => _zoomPercent = Clamp(_zoomPercent + 25);
    private void ZoomOut() => _zoomPercent = Clamp(_zoomPercent - 25);

    private static int Clamp(int z) => Math.Clamp(z, 25, 400);

    // ── Keyboard ────────────────────────────────────────────────────────────────

    private async Task OnKeyDown(KeyboardEventArgs e)
    {
        switch (e.Key)
        {
            case "ArrowLeft":
            case "PageUp":
                await PreviousPageAsync();
                break;
            case "ArrowRight":
            case "PageDown":
            case " ":
                await NextPageAsync();
                break;
            case "Home":
                await FirstPageAsync();
                break;
            case "End":
                await LastPageAsync();
                break;
            case "+":
            case "=":
                ZoomIn();
                StateHasChanged();
                break;
            case "-":
                ZoomOut();
                StateHasChanged();
                break;
        }
    }

    // ── Export / Print ──────────────────────────────────────────────────────────

    private async Task ExportPdfAsync()
    {
        if (Report is null || _busy)
        {
            return;
        }
        await RunExportAsync("pdf", "application/pdf", () =>
        {
            var bytes = new SkiaPdfExporter(new PdfExportOptions { Title = Report.Name })
                .ExportToBytes(Report);
            return Task.FromResult(bytes);
        });
    }

    private async Task ExportXlsxAsync()
    {
        if (Report is null || _busy)
        {
            return;
        }
        await RunExportAsync("xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            () =>
            {
                var bytes = new ExcelExporter(new ExcelExportOptions { Title = Report.Name })
                    .ExportToBytes(Report);
                return Task.FromResult(bytes);
            });
    }

    private async Task PrintAsync()
    {
        if (Report is null || _busy)
        {
            return;
        }
        var printer = Printer ?? (IReportPrinter?)Services.GetService(typeof(IReportPrinter));
        if (printer is null)
        {
            if (OnPrintRequestedWithoutDriver.HasDelegate)
            {
                await OnPrintRequestedWithoutDriver.InvokeAsync();
            }
            else
            {
                _lastError = "Nenhum IReportPrinter registrado em DI. Use OnPrintRequestedWithoutDriver para customizar.";
            }
            return;
        }
        try
        {
            _busy = true;
            var options = PrintOptions ?? new PrintOptions("Microsoft Print to PDF");
            var result = await printer.PrintAsync(Report, options);
            if (!result.Succeeded)
            {
                _lastError = "Falha na impressão: " + (result.ErrorMessage ?? "desconhecida");
            }
        }
        finally
        {
            _busy = false;
            StateHasChanged();
        }
    }

    private async Task RunExportAsync(string extension, string mimeType, Func<Task<byte[]>> producer)
    {
        try
        {
            _busy = true;
            _lastError = null;
            StateHasChanged();
            var bytes = await producer();
            var fileName = SafeFileName(Report?.Name ?? "report") + "." + extension;
            await TriggerBrowserDownloadAsync(fileName, mimeType, bytes);
            if (OnExported.HasDelegate)
            {
                await OnExported.InvokeAsync(new ReportExportEventArgs(extension, fileName, bytes));
            }
        }
        catch (Exception ex)
        {
            _lastError = "Erro ao exportar: " + ex.Message;
        }
        finally
        {
            _busy = false;
            StateHasChanged();
        }
    }

    private async Task TriggerBrowserDownloadAsync(string fileName, string mimeType, byte[] bytes)
    {
        try
        {
            await Js.InvokeVoidAsync("omniViewer.download", fileName, mimeType, bytes);
        }
        catch (JSException)
        {
            // Server-side prerender / test contexts without JS: silently swallow — the
            // OnExported callback still fires so the host can take over the download.
        }
    }

    private static string SafeFileName(string source)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(source.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "report" : sanitized;
    }

    // ── Rendering ───────────────────────────────────────────────────────────────

    private void RenderPages()
    {
        _pageImages.Clear();
        if (Report is null || Report.Pages.Count == 0)
        {
            return;
        }
        try
        {
            using var renderer = new SkiaRenderingContext(ResolvedOptions.RenderDpi);
            RenderedReportPlayer.Play(Report, renderer);
            for (int i = 0; i < renderer.Pages.Count; i++)
            {
                var png = renderer.GetPagePng(i);
                _pageImages.Add("data:image/png;base64," + Convert.ToBase64String(png));
            }
        }
        catch (Exception ex)
        {
            _lastError = "Erro renderizando páginas: " + ex.Message;
        }
    }

    public void Dispose()
    {
        _pageImages.Clear();
    }
}

/// <summary>Arguments raised by <see cref="ReportViewer.OnExported"/>.</summary>
public sealed record ReportExportEventArgs(string Extension, string FileName, byte[] Bytes);
