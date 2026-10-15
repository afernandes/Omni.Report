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

public partial class CommandPalette
{
    [Parameter] public bool Visible { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }
    [Parameter] public EventCallback<string> OnExecute { get; set; }

    private string _query = string.Empty;
    private CommandEntry? _highlight;

    private CommandEntry[] _all =
    [
        new("new",     "Sugestões", "Novo relatório",        "Arquivo",          "Ctrl+N",     "plus"),
        new("save",    "Sugestões", "Salvar",                "Arquivo",          "Ctrl+S",     "save"),
        new("open",    "Sugestões", "Abrir .repx…",          "Arquivo",          "Ctrl+O",     "folder-open"),
        new("preview", "Sugestões", "Preview",               "Relatório",        "F5",         "play"),
        new("undo",    "Editar",    "Desfazer",              "Editar",           "Ctrl+Z",     "undo-2"),
        new("redo",    "Editar",    "Refazer",               "Editar",           "Ctrl+Y",     "redo-2"),
        new("delete",  "Editar",    "Excluir seleção",       "Editar",           "Del",        "trash-2"),
        new("insert.text",  "Inserir", "Inserir TextBox",    "Inserir › Texto",  "Ctrl+Shift+T", "type"),
        new("insert.label", "Inserir", "Inserir Label",      "Inserir › Texto",  "Ctrl+Shift+L", "hash"),
        new("insert.rect",  "Inserir", "Inserir Retângulo",  "Inserir › Forma",  "Ctrl+Shift+R", "square"),
        new("insert.line",  "Inserir", "Inserir Linha",      "Inserir › Forma",  "",           "minus"),
        new("insert.img",   "Inserir", "Inserir Imagem",     "Inserir › Mídia",  "",           "image"),
        new("theme",   "Visualizar","Alternar tema",         "Tema",             "Ctrl+Shift+L", "moon"),
    ];

    private IEnumerable<CommandEntry> Filter()
    {
        if (string.IsNullOrWhiteSpace(_query))
        {
            return _all;
        }
        var q = _query.Trim();
        return _all.Where(c =>
            c.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            c.Crumb.Contains(q, StringComparison.OrdinalIgnoreCase));
    }

    private Task Execute(CommandEntry cmd)
    {
        _query = string.Empty;
        _highlight = null;
        return OnExecute.InvokeAsync(cmd.Id);
    }

    private Task OnCloseClicked() => OnClose.InvokeAsync();

    private async Task HandleKey(KeyboardEventArgs e)
    {
        if (e.Key == "Escape") { await OnClose.InvokeAsync(); return; }
        var items = Filter().ToList();
        if (items.Count == 0)
        {
            return;
        }
        var idx = _highlight is null ? 0 : items.IndexOf(_highlight);
        if (e.Key == "ArrowDown")
        {
            _highlight = items[Math.Min(items.Count - 1, idx + 1)];
        }
        else if (e.Key == "ArrowUp")
        {
            _highlight = items[Math.Max(0, idx - 1)];
        }
        else if (e.Key == "Enter")
        {
            await Execute(_highlight ?? items[0]);
        }
    }
}
