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

public partial class TabBar
{
    [Parameter, EditorRequired] public IReadOnlyList<DocumentTab> Tabs { get; set; } = default!;
    [Parameter] public DocumentTab? ActiveTab { get; set; }
    [Parameter] public EventCallback<DocumentTab> Activate { get; set; }
    [Parameter] public EventCallback<DocumentTab> Close { get; set; }
    [Parameter] public EventCallback OnNew { get; set; }

    private Task OnNewClicked() => OnNew.InvokeAsync();
}
