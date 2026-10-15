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
namespace Reporting.Designer.Blazor.Components;

public partial class CanvasRegion
{
    [Parameter, EditorRequired] public ReportDefinitionViewModel Report { get; set; } = default!;
    [Parameter] public ElementViewModel? Selected { get; set; }
    [Parameter] public EventCallback<ElementViewModel?> SelectionChanged { get; set; }
    [Parameter] public BandViewModel? ActiveBand { get; set; }
    [Parameter] public EventCallback<BandViewModel?> ActiveBandChanged { get; set; }
    [Parameter] public bool GridVisible { get; set; }
    [Parameter] public bool RulersVisible { get; set; } = true;
    [Parameter] public bool SnapToGrid { get; set; } = true;
    [Parameter] public double Zoom { get; set; } = 1.0;
    [Parameter] public IReadOnlyList<DesignerDataSource> DataSources { get; set; } = Array.Empty<DesignerDataSource>();
    [Parameter] public IReadOnlyList<DesignerParameter> Parameters { get; set; } = Array.Empty<DesignerParameter>();
    /// <summary>Master-detail relations, passed straight through to the BandCanvas's
    /// SubDetail DataMember picker. Nullable so blank-relation reports stay simple.</summary>
    [Parameter] public System.Collections.ObjectModel.ObservableCollection<DesignerRelation>? Relations { get; set; }
    [Parameter] public bool IsPreviewing { get; set; }
    [Parameter] public IReadOnlyList<byte[]> PreviewPages { get; set; } = [];
    [Parameter] public int PreviewCurrentPage { get; set; }
    [Parameter] public EventCallback OnExitPreview { get; set; }
    [Parameter] public EventCallback<int> OnPreviewPageChanged { get; set; }
    /// <summary>Dynamic export buttons rendered in the preview toolbar. One per
    /// registered <see cref="Reporting.Designer.Blazor.Services.ExporterDescriptor"/>.</summary>
    [Parameter] public IReadOnlyList<Reporting.Designer.Blazor.Services.ExporterDescriptor>? Exporters { get; set; }
    [Parameter] public EventCallback<Reporting.Designer.Blazor.Services.ExporterDescriptor> OnExport { get; set; }
    [Parameter] public EventCallback OnPrint { get; set; }
    [Parameter] public bool IsExporting { get; set; }
    [Parameter] public int PreviewZoom { get; set; } = 100;
    [Parameter] public EventCallback<int> PreviewZoomChanged { get; set; }

    private ElementReference _hRuler;
    private ElementReference _vRuler;
    private ElementReference _scrollRef;
    private bool _rulersAttached;

    /// <summary>Ruler display unit (cm / mm / in). Cycled via the corner button; pushed to the
    /// JS ruler engine which redraws labels and the guide readout in the chosen unit.</summary>
    private string _rulerUnit = "cm";
    private string UnitAbbr => _rulerUnit == "in" ? "pol" : _rulerUnit;

    private async Task CycleRulerUnit()
    {
        _rulerUnit = _rulerUnit switch { "cm" => "mm", "mm" => "in", _ => "cm" };
        try { await JS.InvokeVoidAsync("omniDesigner.setRulerUnit", _rulerUnit, _scrollRef); }
        catch (JSException) { }
        catch (InvalidOperationException) { }
    }

    /// <summary>Wires the JS ruler engine once (it then self-updates on scroll/zoom/resize/mouse),
    /// and on every later render nudges a redraw so zoom/selection/page changes are reflected.</summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        try
        {
            if (!_rulersAttached)
            {
                _rulersAttached = true;
                await JS.InvokeVoidAsync("omniDesigner.attachRulers", _hRuler, _vRuler, _scrollRef);
                await JS.InvokeVoidAsync("omniDesigner.setRulerUnit", _rulerUnit, _scrollRef);
            }
            else
            {
                await JS.InvokeVoidAsync("omniDesigner.refreshRulers", _scrollRef);
            }
            // Empurra o estado do snap a cada render para o botão Snap do toolbar valer.
            await JS.InvokeVoidAsync("omniDesigner.setSnap", SnapToGrid, _scrollRef);
        }
        catch (JSException) { /* test runtime / JS unavailable */ }
        catch (InvalidOperationException) { /* pre-render: no JS yet */ }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("omniDesigner.detachRulers", _scrollRef);
        }
        catch (JSException) { }
        catch (JSDisconnectedException) { }
        catch (InvalidOperationException) { }
    }
}
