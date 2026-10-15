using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Reporting.Designer.Blazor.Services;

namespace Reporting.Designer.Blazor.Components;

public partial class CommandPalette
{
    [Parameter] public bool Visible { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }
    [Parameter] public EventCallback<string> OnExecute { get; set; }

    /// <summary>The Designer's command list — the same one the menus render, so the palette can reach every action.</summary>
    [Parameter] public IReadOnlyList<DesignerCommand> Commands { get; set; } = [];

    private string _query = string.Empty;
    private DesignerCommand? _highlight;
    private ElementReference _input;
    private bool _focusPending;

    protected override void OnParametersSet()
    {
        // `autofocus` is ignored for an element Blazor inserts after load, so the search box is focused explicitly
        // each time the palette opens; otherwise the first keystrokes go to the canvas instead of the query.
        if (!Visible)
        {
            _focusPending = true;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Visible && _focusPending)
        {
            _focusPending = false;
            await _input.FocusAsync();
        }
    }

    private IEnumerable<DesignerCommand> Filter()
    {
        if (string.IsNullOrWhiteSpace(_query))
        {
            return Commands;
        }
        var q = _query.Trim();
        return Commands.Where(c =>
            c.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            c.Menu.Contains(q, StringComparison.OrdinalIgnoreCase));
    }

    private Task Execute(DesignerCommand cmd)
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
