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

public partial class OutlineTree
{
    [Parameter, EditorRequired] public ReportDefinitionViewModel Report { get; set; } = default!;
    [Parameter] public ElementViewModel? SelectedElement { get; set; }
    [Parameter] public BandViewModel? ActiveBand { get; set; }
    [Parameter] public EventCallback<ElementViewModel?> OnSelect { get; set; }
    [Parameter] public EventCallback<BandViewModel> OnBandActivated { get; set; }
    [Parameter] public IReadOnlyList<DesignerDataSource> DataSources { get; set; } = Array.Empty<DesignerDataSource>();
    [Parameter] public IReadOnlyList<DesignerParameter> Parameters { get; set; } = Array.Empty<DesignerParameter>();
    /// <summary>Master-detail relations. Drives the DataMember picker shown beneath every
    /// SubDetail band row — same options as the band-strip select in the canvas, exposed
    /// here so users can wire bindings without scrolling.</summary>
    [Parameter] public System.Collections.ObjectModel.ObservableCollection<DesignerRelation>? Relations { get; set; }

    private string DataMemberTitle(BandViewModel band)
    {
        if (string.IsNullOrEmpty(band.DataMember))
        {
            return "Escolha uma relação (master-detail) ou uma fonte para esta sub-banda iterar.";
        }
        var rel = Relations?.FirstOrDefault(r => r.Name == band.DataMember);
        if (rel is not null)
        {
            return $"Relação '{rel.Name}': filtra {rel.ChildSource} pelo campo {rel.ChildField} = {rel.ParentSource}.{rel.ParentField}.";
        }
        if (DataSources.Any(d => d.Name == band.DataMember))
        {
            return $"Fonte '{band.DataMember}': itera todas as linhas (sem filtro).";
        }
        return $"⚠ '{band.DataMember}' não encontrado nas relações nem nas fontes.";
    }

    private ReportDefinitionViewModel? _subscribed;

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_subscribed, Report))
        {
            if (_subscribed is not null)
            {
                _subscribed.Changed -= StateHasChanged;
            }
            _subscribed = Report;
            if (_subscribed is not null)
            {
                _subscribed.Changed += StateHasChanged;
            }
        }
    }

    public void Dispose()
    {
        if (_subscribed is not null)
        {
            _subscribed.Changed -= StateHasChanged;
        }
    }

    private void RemoveElement(BandViewModel band, ElementViewModel el)
    {
        band.RemoveElement(el);
        if (ReferenceEquals(SelectedElement, el))
        {
            OnSelect.InvokeAsync(null);
        }
    }

    private void RemoveChild(ElementViewModel parent, ElementViewModel child)
    {
        parent.RemoveChild(child);
        if (ReferenceEquals(SelectedElement, child))
        {
            OnSelect.InvokeAsync(null);
        }
    }

    private readonly DesignerBandKind[] _optional =
    [
        DesignerBandKind.ReportHeader,
        DesignerBandKind.GroupHeader,
        DesignerBandKind.SubDetail,
        DesignerBandKind.GroupFooter,
        DesignerBandKind.ReportFooter,
    ];

    // Icon comes from the single per-element source (the [ToolboxElement] annotation via ToolboxCatalog),
    // so the outline shows each element's real icon (e.g. a chart's) — no separate switch to maintain.
    private static string KindIcon(ElementViewModel e) => ToolboxCatalog.For(e.Kind).Icon;

    private static string Short(DesignerBandKind k) => k switch
    {
        DesignerBandKind.ReportHeader => "RH",
        DesignerBandKind.GroupHeader => "GH",
        DesignerBandKind.SubDetail => "SD",
        DesignerBandKind.GroupFooter => "GF",
        DesignerBandKind.ReportFooter => "RF",
        _ => k.ToString(),
    };

    private static string Label(DesignerBandKind k) => k switch
    {
        DesignerBandKind.ReportHeader => "Report Header",
        DesignerBandKind.GroupHeader => "Group Header",
        DesignerBandKind.SubDetail => "Sub-Detail (master-detail)",
        DesignerBandKind.GroupFooter => "Group Footer",
        DesignerBandKind.ReportFooter => "Report Footer",
        _ => k.ToString(),
    };

    private void ToggleBand(DesignerBandKind kind)
    {
        // Sub-details aren't singletons — every toggle adds a new one (a report may
        // declare multiple nested loops). For the other singleton bands the existing
        // toggle-off / toggle-on semantics still apply.
        if (kind == DesignerBandKind.SubDetail)
        {
            Report.AddBand(new BandViewModel(kind, Unit.FromMm(8)) { DataMember = "" });
            return;
        }
        var existing = Report.FindBand(kind);
        if (existing is not null)
        {
            Report.RemoveBand(existing);
        }
        else
        {
            var newBand = new BandViewModel(kind, Unit.FromMm(8));
            Report.AddBand(newBand);
        }
    }
}
