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
using Reporting.Parameters;
namespace Reporting.Designer.Blazor.Components;

public partial class VariablesList
{
    [Parameter, EditorRequired] public IReadOnlyList<DesignerVariable> Variables { get; set; } = default!;
    [Parameter] public EventCallback OnAdd { get; set; }
    [Parameter] public EventCallback<DesignerVariable> OnRemove { get; set; }

    private static VariableScope ParseScope(string? v)
        => Enum.TryParse<VariableScope>(v, out var s) ? s : VariableScope.Report;
}
