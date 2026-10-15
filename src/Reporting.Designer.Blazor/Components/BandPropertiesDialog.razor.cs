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

public partial class BandPropertiesDialog
{
    [Parameter] public bool Visible { get; set; }
    [Parameter] public BandViewModel? Band { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    /// <summary>True when the band is a region that consumes rows — Detail and SubDetail.
    /// Drives visibility of NoRowsMessage / Filter / Sort. Group bands also see Filter / Sort
    /// but NOT NoRowsMessage (RDL ties the "no rows" message to a data region, not a group).</summary>
    private bool IsDataRegion => Band?.Kind is DesignerBandKind.Detail or DesignerBandKind.SubDetail;

    private bool IsGroup => Band?.Kind is DesignerBandKind.GroupHeader;

    private string DialogTitle => Band?.Kind switch
    {
        DesignerBandKind.Detail => "Propriedades · Detail",
        DesignerBandKind.SubDetail => $"Propriedades · Sub-Detail · {Band.DataMember ?? "?"}",
        DesignerBandKind.GroupHeader => $"Propriedades · Grupo · {Band.GroupName ?? "?"}",
        DesignerBandKind.GroupFooter => "Propriedades · Group Footer",
        DesignerBandKind.ReportHeader => "Propriedades · Report Header",
        DesignerBandKind.PageHeader => "Propriedades · Page Header",
        DesignerBandKind.PageFooter => "Propriedades · Page Footer",
        DesignerBandKind.ReportFooter => "Propriedades · Report Footer",
        _ => "Propriedades da banda",
    };

    private string PageBreakHint => Band?.PageBreak switch
    {
        PageBreak.Start => "Força quebra de página ANTES desta banda emitir.",
        PageBreak.End => "Força quebra de página DEPOIS desta banda emitir.",
        PageBreak.StartAndEnd => "Banda sempre renderiza em página própria.",
        PageBreak.Between => "Quebra entre cada instância do grupo (group only).",
        _ => "Banda flui normalmente com o restante do relatório.",
    };

    private static PageBreak ParsePageBreak(string? raw)
        => string.IsNullOrEmpty(raw) ? PageBreak.None : Enum.Parse<PageBreak>(raw);

    private static string? AsNullable(string? v) => string.IsNullOrWhiteSpace(v) ? null : v;

    private void AddSort()
    {
        Band?.SortExpressions.Add(new SortDescriptorRule { Expression = "Fields." });
    }

    private void AddVariable()
    {
        Band?.Variables.Add(new GroupVariableRule { Name = "Var", Expression = "" });
    }

    private Task OnClosed() => OnClose.InvokeAsync();
}
