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
using Reporting.Expressions;
namespace Reporting.Designer.Blazor.Components;

public partial class ExpressionEditorDialog
{

    // ─── Function catalog by category ──────────────────────────────────────────
    private sealed partial record FunctionEntry(string Name, string Insert, string Description);
}
