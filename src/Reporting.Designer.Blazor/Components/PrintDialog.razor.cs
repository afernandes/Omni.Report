using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Reporting;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Paper;
using Reporting.Styling;
using Reporting.Designer.Blazor;
using Reporting.Designer.Blazor.Components;
using Reporting.Designer.Blazor.Icons;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Output.Pdf;
using Reporting.Output.Excel;
using Reporting.Printing;
using System.Globalization;
using Reporting.Designer.Blazor.Services;
namespace Reporting.Designer.Blazor.Components;

public partial class PrintDialog
{
    [Parameter] public bool Visible { get; set; }
    [Parameter] public EventCallback OnClosed { get; set; }
    /// <summary>The actually-pre-rendered preview pages — same byte[] PNGs the PreviewMode
    /// uses. Drives the thumbnail panel; "Imprimir" disabled when this is empty because
    /// we have nothing to print.</summary>
    [Parameter] public IReadOnlyList<byte[]>? PreviewPages { get; set; }
    /// <summary>0-based index of the page the preview is currently showing — used as the
    /// "current page" target when the user picks <see cref="PrintPageRangeMode.Current"/>.</summary>
    [Parameter] public int CurrentPage { get; set; }
    /// <summary>Total page count, used in the "Todas (N)" label.</summary>
    [Parameter] public int TotalPages { get; set; }
    /// <summary>The report's own paper name — shown in the "Manter do relatório" option
    /// so the user knows what they'd be using by default.</summary>
    [Parameter] public string DocumentPaper { get; set; } = "A4";
    /// <summary>Default orientation of the report. Used when the user hasn't overridden
    /// the orientation in this dialog (we don't auto-flip on paper change).</summary>
    [Parameter] public Orientation DocumentOrientation { get; set; } = Orientation.Portrait;

    /// <summary>Invoked when the user clicks Imprimir — receives the final
    /// <see cref="PrintRequest"/>. The owner is responsible for calling
    /// <see cref="IDesignerPrintService.PrintAsync"/> and closing the dialog.</summary>
    [Parameter] public EventCallback<PrintRequest> OnPrint { get; set; }

    private PrintRequest _request = PrintRequest.Default;
    private List<DesignerPrinter> _printers = new();
    private bool _hasNativeService;
    private int _previewPageIndex;
    private bool _busy;
    private string? _errorMessage;
    private bool _initialized;

    protected override async Task OnParametersSetAsync()
    {
        // Reset state every time the dialog opens — we want fresh defaults, no leakage
        // from a previous print job's stale errors or page index.
        if (Visible && !_initialized)
        {
            _initialized = true;
            _request = PrintRequest.Default;
            _previewPageIndex = CurrentPage;
            _errorMessage = null;
            _busy = false;
            await LoadPrintersAsync();
        }
        else if (!Visible)
        {
            _initialized = false;
        }
    }

    /// <summary>Resolves the print service from DI and asks for the printer list. The
    /// service is OPTIONAL — when nobody registered one, we keep defaults: browser mode,
    /// no printer list (the dialog hides the dropdown via _hasNativeService=false).</summary>
    private async Task LoadPrintersAsync()
    {
        if (Services.GetService(typeof(IDesignerPrintService)) is not IDesignerPrintService service)
        {
            _hasNativeService = false;
            return;
        }
        _hasNativeService = service.SupportsDirectPrint;
        if (!_hasNativeService)
        {
            return;
        }
        try
        {
            var list = await service.ListPrintersAsync();
            _printers = list.ToList();
            // Pre-select the OS default printer if present.
            var def = _printers.FirstOrDefault(p => p.IsDefault) ?? _printers.FirstOrDefault();
            if (def is not null)
            {
                _request = _request with { PrinterName = def.Name, OutputMode = PrintOutputMode.SystemSpooler };
            }
        }
        catch
        {
            // Printer enumeration failure is non-fatal — the dialog still works for
            // PDF/browser modes. The user just won't see a list.
            _printers.Clear();
        }
    }

    private void Mutate(Func<PrintRequest, PrintRequest> change)
    {
        _request = change(_request);
        _errorMessage = null;
    }

    private void OnPrinterChanged(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return;
        }
        _request = _request with { PrinterName = name };
    }

    private void OnPaperChanged(string? key)
    {
        _request = key switch
        {
            null or "" => _request with { PaperSize = null },
            "A4" => _request with { PaperSize = PaperSize.A4 },
            "A5" => _request with { PaperSize = PaperSize.A5 },
            "Letter" => _request with { PaperSize = PaperSize.Letter },
            "Legal" => _request with { PaperSize = PaperSize.Legal },
            "Thermal80" => _request with { PaperSize = PaperSize.Thermal80, Orientation = Orientation.Portrait },
            "Thermal58" => _request with { PaperSize = PaperSize.Thermal58, Orientation = Orientation.Portrait },
            _ => _request,
        };
    }

    /// <summary>Picks the orientation actively in use for highlight state in the
    /// retrato/paisagem button group: explicit override wins, else the document default.</summary>
    private Orientation EffectiveOrientation =>
        _request.Orientation ?? DocumentOrientation;

    private static int ParseCopies(string? raw)
        => int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            ? Math.Clamp(n, 1, 999)
            : 1;

    private async Task HandlePrint()
    {
        if (_busy)
        {
            return;
        }        // Encode the "current page" choice as a range — keeps the request DTO clean.
        var finalRequest = _request;
        if (finalRequest.RangeMode == PrintPageRangeMode.Current)
        {
            finalRequest = finalRequest with
            {
                RangeMode = PrintPageRangeMode.Range,
                RangeText = (CurrentPage + 1).ToString(CultureInfo.InvariantCulture),
            };
        }
        _busy = true;
        _errorMessage = null;
        try
        {
            await OnPrint.InvokeAsync(finalRequest);
            await OnClosed.InvokeAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
            _busy = false;
        }
    }

    private async Task HandleCancel() => await OnClosed.InvokeAsync();
}
