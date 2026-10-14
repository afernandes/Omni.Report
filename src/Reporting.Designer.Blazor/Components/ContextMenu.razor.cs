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
namespace Reporting.Designer.Blazor.Components;

public partial class ContextMenu
{
    private ElementReference _menuRoot;
    [Parameter] public bool Visible { get; set; }
    [Parameter] public double X { get; set; }
    [Parameter] public double Y { get; set; }
    [Parameter] public IReadOnlyList<ContextMenuItem> Items { get; set; } = Array.Empty<ContextMenuItem>();
    [Parameter] public EventCallback OnClose { get; set; }

    private DotNetObjectReference<ContextMenu>? _selfRef;
    private bool _wasVisible;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Visible)
        {
            try { await Js.InvokeVoidAsync("omniDesigner.positionContextMenu", _menuRoot, X, Y); }
            catch (JSException) { }
            catch (InvalidOperationException) { }
        }
        // Each open transition (false → true) re-registers the outside-click listener so
        // the menu is closed when the user pointer-downs anywhere except inside it.
        if (Visible && !_wasVisible)
        {
            _wasVisible = true;
            _selfRef ??= DotNetObjectReference.Create(this);
            try { await Js.InvokeVoidAsync("omniDesigner.registerOutsideClick", _selfRef, _menuRoot); }
            catch (JSException) { }
            catch (InvalidOperationException) { }
        }
        else if (!Visible && _wasVisible)
        {
            _wasVisible = false;
        }
    }

    [JSInvokable]
    public Task CloseFromOutside() => OnClose.InvokeAsync();

    private async Task Invoke(ContextMenuItem item)
    {
        if (item.OnClick.HasValue)
        {
            await item.OnClick.Value.InvokeAsync();
        }
        await OnClose.InvokeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _selfRef?.Dispose();
        await Task.CompletedTask;
    }
}
