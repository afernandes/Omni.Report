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
using Reporting.Output.Excel;
using Reporting.Printing;
using System.Globalization;
namespace Reporting.Designer.Blazor.Components;

public partial class PreviewMode
{
    [Parameter] public IReadOnlyList<byte[]> Pages { get; set; } = [];
    [Parameter] public int CurrentPage { get; set; }
    [Parameter] public EventCallback OnExit { get; set; }
    [Parameter] public EventCallback<int> OnPageChanged { get; set; }
    /// <summary>Registered exporters — one toolbar button per descriptor. Hosts populate
    /// this from <see cref="Services.IExporterRegistry.Descriptors"/>; if the registry is
    /// empty the export group simply doesn't render and only the Imprimir button shows.</summary>
    [Parameter] public IReadOnlyList<Reporting.Designer.Blazor.Services.ExporterDescriptor>? Exporters { get; set; }
    /// <summary>Fires when the user clicks one of the dynamic export buttons. The owner
    /// resolves the exporter, paginates if needed, and triggers the download.</summary>
    [Parameter] public EventCallback<Reporting.Designer.Blazor.Services.ExporterDescriptor> OnExport { get; set; }
    [Parameter] public EventCallback OnPrint { get; set; }
    /// <summary>True while an export is running: export and print stay disabled so a second click is visibly refused
    /// instead of being silently dropped.</summary>
    [Parameter] public bool IsBusy { get; set; }
    /// <summary>Physical paper width in mm — drives the preview-page rectangle. Defaults to
    /// A4 (210mm) so existing call sites that don't pass a PageSetup still get a sane result.</summary>
    [Parameter] public double PaperWidthMm { get; set; } = 210.0;
    /// <summary>Zoom percentage (25–400). Optional — falls back to internal state when no
    /// parent provides one, so existing callers keep working without changes.</summary>
    [Parameter] public int Zoom { get; set; } = 100;
    [Parameter] public EventCallback<int> ZoomChanged { get; set; }

    // Local backing fields used only when the parent doesn't control Zoom.
    private int _zoom => Zoom > 0 ? Zoom : 100;

    private int PageCount => Pages?.Count ?? 0;

    /// <summary>Pixels per mm to keep the preview consistent with the design canvas
    /// (A4 = 210mm = 720px).</summary>
    private const double PxPerMm = 720.0 / 210.0;

    private string PreviewPageStyle
    {
        get
        {
            var widthPx = (PaperWidthMm * PxPerMm).ToString("F2", CultureInfo.InvariantCulture);
            var scale = (_zoom / 100.0).ToString("F2", CultureInfo.InvariantCulture);
            // padding:0 removes the design CSS's default 15mm internal padding — the rendered
            // PNG already includes the report's own margins, so any extra padding here would
            // double the visual margin and make Thermal previews look extra-padded.
            return $"width:{widthPx}px;padding:0;transform:scale({scale});transform-origin:top center;";
        }
    }

    private Task OnExitClicked() => OnExit.InvokeAsync();
    private Task OnPrintClicked() => OnPrint.InvokeAsync();

    private Task GoTo(int index)
    {
        if (PageCount == 0)
        {
            return Task.CompletedTask;
        }
        var clamped = Math.Clamp(index, 0, PageCount - 1);
        return OnPageChanged.InvokeAsync(clamped);
    }

    private Task SetZoom(int z) => ZoomChanged.InvokeAsync(Math.Clamp(z, 25, 400));
}
