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
using Reporting.Printing;
namespace Reporting.Designer.Blazor.Components;

public partial class RightPanel
{
    [Parameter, EditorRequired] public ReportDefinitionViewModel Report { get; set; } = default!;
    [Parameter] public ElementViewModel? Selected { get; set; }
    [Parameter] public IReadOnlyList<ElementViewModel> Selection { get; set; } = Array.Empty<ElementViewModel>();
    [Parameter] public string BandLabel { get; set; } = "Detail";
    [Parameter] public EventCallback<ElementViewModel?> OnSelect { get; set; }
    [Parameter] public EventCallback<string?> OnOpenExpressionEditor { get; set; }
    [Parameter] public BandViewModel? ActiveBand { get; set; }
    [Parameter] public EventCallback<BandViewModel> OnBandActivated { get; set; }
    [Parameter] public IReadOnlyList<DesignerDataSource> DataSources { get; set; } = Array.Empty<DesignerDataSource>();
    [Parameter] public IReadOnlyList<DesignerParameter> Parameters { get; set; } = Array.Empty<DesignerParameter>();
    /// <summary>Master-detail relations, forwarded to the OutlineTree's per-SubDetail
    /// DataMember picker.</summary>
    [Parameter] public System.Collections.ObjectModel.ObservableCollection<DesignerRelation>? Relations { get; set; }

    private int _tab;

    // Captures the selected element's current style as a reusable report-level named style.
    private void HandleCreateNamedStyle(string name)
    {
        if (Selected is not null)
        {
            Report.SetNamedStyle(name, Selected.ToElement().Style);
        }
    }
}
