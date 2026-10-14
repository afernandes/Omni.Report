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
using Reporting.Output.Pdf;
using Reporting.Output.Excel;
using Reporting.Printing;
using System.Globalization;
namespace Reporting.Designer.Blazor.Components;

public partial class StatusBar
{
    [Parameter] public double SelectedX { get; set; }
    [Parameter] public double SelectedY { get; set; }
    [Parameter] public double SelectedW { get; set; }
    [Parameter] public double SelectedH { get; set; }
    [Parameter] public double Zoom { get; set; } = 1.0;
    /// <summary>Real paper + orientation, e.g. "A4 · Retrato". Replaces the old static page counter
    /// (the design canvas isn't paginated — page totals only exist in Preview, which has its own nav).</summary>
    [Parameter] public string PaperLabel { get; set; } = "—";
    [Parameter] public int BandCount { get; set; }
    [Parameter] public int ElementCount { get; set; }
    /// <summary>Number of currently-selected elements — reflects multi-selection, not a fixed "1".</summary>
    [Parameter] public int SelectionCount { get; set; }
    [Parameter] public bool IsDirty { get; set; }
    [Parameter] public int ErrorCount { get; set; }
    [Parameter] public string DataSourceName { get; set; } = "—";
    /// <summary>Real connection kind of the first source (in-memory / SQLite / …) — not a fixed
    /// "conectado" claim, since the designer doesn't verify live connections.</summary>
    [Parameter] public string DataSourceKind { get; set; } = string.Empty;
    [Parameter] public int DataSourceCount { get; set; }

    private string SelectionLabel
        => SelectionCount == 0 ? "—" : $"{SelectionCount} element{(SelectionCount > 1 ? "os" : "o")}";

    private string DataSourceTitle
        => DataSourceCount > 1
            ? $"{DataSourceCount} fontes de dados (exibindo a primeira)"
            : "Fonte de dados";

    private static string FormatMm(double mm) => mm.ToString("F0", CultureInfo.InvariantCulture);
}
