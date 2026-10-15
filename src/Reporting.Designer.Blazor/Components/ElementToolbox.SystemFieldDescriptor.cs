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

public partial class ElementToolbox
{

    /// <summary>Payload passed to the host (ReportDesigner) when a system field is
    /// clicked. The host creates a TextBox with the expression preloaded.</summary>
    public sealed record SystemFieldDescriptor(string Label, string Expression, double WidthMm);
}
