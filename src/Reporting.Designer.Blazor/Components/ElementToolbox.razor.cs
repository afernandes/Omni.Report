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
    [Parameter] public EventCallback<DesignerElementKind> OnAdd { get; set; }
    [Parameter] public EventCallback<SystemFieldDescriptor> OnAddSystemField { get; set; }

    private string _filter = string.Empty;

    private Task OnAddClicked(DesignerElementKind kind) => OnAdd.InvokeAsync(kind);
    private Task OnSystemFieldClicked(SystemFieldItem item)
        => OnAddSystemField.InvokeAsync(new SystemFieldDescriptor(item.Label, item.Expression, item.WidthMm));

    private async Task OnDragStart(DragEventArgs e, DesignerElementKind kind)
    {
        // We rely on the JS module to read this via dataTransfer; nothing to do server-side.
        await Task.CompletedTask;
    }

    private IEnumerable<ToolboxCatalog.Item> Filter(IEnumerable<ToolboxCatalog.Item> source)
        => string.IsNullOrWhiteSpace(_filter)
           ? source
           : source.Where(i => i.Label.Contains(_filter, StringComparison.OrdinalIgnoreCase));

    private IEnumerable<SystemFieldItem> Filter(IEnumerable<SystemFieldItem> source)
        => string.IsNullOrWhiteSpace(_filter)
           ? source
           : source.Where(i => i.Label.Contains(_filter, StringComparison.OrdinalIgnoreCase));

    // System fields — click to insert a TextBox bound to the canonical expression.
    // Avoids typing things like "{Page.Number} de {Page.Total}" by hand each time.
    private readonly SystemFieldItem[] _system =
    [
        new("Nº Página",      "hash",          "Número da página atual",     "{Page.Number}",                            20),
        new("Total Páginas",  "files",         "Total de páginas do relatório", "{Page.Total}",                          20),
        new("Pág X de Y",     "book",          "Número e total combinados",  "Página {Page.Number} de {Page.Total}",     50),
        new("Hoje",           "calendar",      "Data atual no formato curto", "{Today:dd/MM/yyyy}",                       30),
        new("Agora",          "clock",         "Data e hora correntes",      "{Now:dd/MM/yyyy HH:mm}",                   40),
        new("Nome Relatório", "file-text",     "Nome do report",             "{Report.Name}",                            60),
        new("Usuário",        "user",          "Usuário corrente",           "{User.Name}",                              40),
    ];
}
