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
namespace Reporting.Designer.Blazor;

public partial class ReportDesigner
{

    public sealed partial class GesturePayload
    {
        public string Kind { get; set; } = "";
        public string ElementId { get; set; } = "";
        public string BandKind { get; set; } = "";
        public double XMm { get; set; }
        public double YMm { get; set; }
        public double WidthMm { get; set; }
        public double HeightMm { get; set; }
    }

}
