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

public partial class LeftPanel
{
    [Parameter, EditorRequired] public IReadOnlyList<DesignerDataSource> DataSources { get; set; } = default!;
    [Parameter, EditorRequired] public IReadOnlyList<DesignerParameter> Parameters { get; set; } = default!;
    [Parameter] public System.Collections.ObjectModel.ObservableCollection<DesignerRelation>? Relations { get; set; }
    [Parameter] public EventCallback<DesignerElementKind> OnAdd { get; set; }
    [Parameter] public EventCallback<ElementToolbox.SystemFieldDescriptor> OnAddSystemField { get; set; }
    [Parameter] public EventCallback OnAddParameter { get; set; }
    [Parameter] public EventCallback<DesignerParameter> OnRemoveParameter { get; set; }
    [Parameter] public IReadOnlyList<DesignerVariable>? Variables { get; set; }
    [Parameter] public EventCallback OnAddVariable { get; set; }
    [Parameter] public EventCallback<DesignerVariable> OnRemoveVariable { get; set; }
    [Parameter] public EventCallback OnAddDataSource { get; set; }
    [Parameter] public EventCallback<DesignerDataSource> OnEditDataSource { get; set; }

    private int _tab;
}
