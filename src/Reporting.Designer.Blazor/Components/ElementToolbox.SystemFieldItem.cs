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

public partial class ElementToolbox
{

    private sealed partial record SystemFieldItem(
        string Label,
        string Icon,
        string Description,
        string Expression,
        double WidthMm)
    {
        // Tail shown to the right of the item — a hint of what gets inserted.
        public string Sample => Expression.Length > 14 ? Expression[..14] + "…" : Expression;
    }
}
