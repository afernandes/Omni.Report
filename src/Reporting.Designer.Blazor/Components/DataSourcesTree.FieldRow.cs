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
using System.Collections.ObjectModel;
namespace Reporting.Designer.Blazor.Components;

public partial class DataSourcesTree
{
    private sealed record FieldRow(string DepthClass, DesignerDataSource Source, DesignerField Field, bool IsAmbiguous) : TreeRow(DepthClass);
}
