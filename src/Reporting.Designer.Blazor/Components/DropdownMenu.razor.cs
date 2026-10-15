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

public partial class DropdownMenu
{
    private ElementReference _menuRoot;
    [Parameter, EditorRequired] public string Label { get; set; } = default!;
    [Parameter] public RenderFragment? ChildContent { get; set; }

    private bool _open;
    private DotNetObjectReference<DropdownMenu>? _selfRef;

    [CascadingParameter] public Reporting.Designer.Blazor.Services.DesignerMenuCoordinator? MenuCoordinator { get; set; }
    private readonly Reporting.Designer.Blazor.Services.DesignerMenuCoordinator _localMenus = new();
    private Reporting.Designer.Blazor.Services.DesignerMenuCoordinator Menus => MenuCoordinator ?? _localMenus;

    protected override void OnInitialized()
    {
        Menus.CloseRequested += OnCloseRequested;
    }

    private void OnCloseRequested()
    {
        if (_open)
        {
            ClosePrivate();
            _ = InvokeAsync(StateHasChanged);
        }
    }

    private void Toggle()
    {
        if (_open)
        {
            ClosePrivate();
            return;
        }
        Open();
    }

    private void Open()
    {
        // Close every other menu first.
        Menus.Open();
        _open = true;
        _ = RegisterOutsideClickAsync();
    }

    private void ClosePrivate()
    {
        if (!_open)
        {
            return;
        }
        _open = false;
        Menus.Close();
    }

    private void OnHostMouseEnter()
    {
        // If a sibling is already open, switch to this one on hover (no click needed).
        if (!_open && Menus.AnyOpen)
        {
            Open();
        }
    }

    private async Task RegisterOutsideClickAsync()
    {
        try
        {
            _selfRef ??= DotNetObjectReference.Create(this);
            await Js.InvokeVoidAsync("omniDesigner.registerOutsideClick", _selfRef, _menuRoot);
        }
        catch (JSException) { }
        catch (InvalidOperationException) { }
        catch (Exception) { /* defensive */ }
    }

    private void OnPanelClicked()
    {
        // Clicking an item bubbles up; close after the item handler ran.
        ClosePrivate();
    }

    [JSInvokable]
    public Task CloseFromOutside()
    {
        if (_open)
        {
            ClosePrivate();
            return InvokeAsync(StateHasChanged);
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        Menus.CloseRequested -= OnCloseRequested;
        if (_open)
        {
            Menus.Close();
        }
        _selfRef?.Dispose();
    }
}
