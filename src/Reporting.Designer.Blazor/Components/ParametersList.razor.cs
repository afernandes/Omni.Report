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

public partial class ParametersList
{
    [Parameter, EditorRequired] public IReadOnlyList<DesignerParameter> Parameters { get; set; } = default!;
    [Parameter] public EventCallback OnAdd { get; set; }
    [Parameter] public EventCallback<DesignerParameter> OnRemove { get; set; }

    private static DesignerFieldType ParseType(string? v)
        => Enum.TryParse<DesignerFieldType>(v, out var t) ? t : DesignerFieldType.Text;
}
