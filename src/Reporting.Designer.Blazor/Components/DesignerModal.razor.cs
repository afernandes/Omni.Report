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
using Reporting.Designer.Blazor.Services;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Printing;
namespace Reporting.Designer.Blazor.Components;

public partial class DesignerModal
{
    [Parameter] public bool Visible { get; set; }
    [Parameter] public string Title { get; set; } = "Diálogo";
    [Parameter] public string HeadIcon { get; set; } = "settings";
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public RenderFragment? Footer { get; set; }

    /// <summary>When true, clicking the backdrop closes the dialog. Industry default for
    /// editing dialogs (Radzen <c>CloseDialogOnOverlayClick</c>, MudBlazor
    /// <c>DisableBackdropClick=true</c>, FluentUI <c>PreventDismissOnOverlayClick</c>):
    /// <c>false</c> — backdrop clicks are IGNORED so the user can't accidentally lose
    /// in-progress edits. Opt-in via the parameter only for throw-away overlays
    /// (Command Palette, tooltips, picker popovers).</summary>
    [Parameter] public bool CloseOnBackdrop { get; set; } = false;

    /// <summary>Whether Esc closes the modal (default true). Set false for modals that
    /// guard against accidental dismissal (unsaved confirmations).</summary>
    [Parameter] public bool CloseOnEscape { get; set; } = true;

    [Parameter] public EventCallback OnClose { get; set; }

    [CascadingParameter] internal DesignerFocusScope? FocusScope { get; set; }

    private ElementReference _dialog;
    private bool _shown;
    private bool _focusDialog;
    private bool _restoreFocus;

    protected override void OnParametersSet()
    {
        // Focus follows the visibility edge, not every render: re-focusing the dialog on each parameter update would
        // pull focus out of the field the user is typing in.
        if (Visible && !_shown)
        {
            _focusDialog = true;
            _restoreFocus = false;
        }
        else if (!Visible && _shown)
        {
            _restoreFocus = true;
            _focusDialog = false;
        }
        _shown = Visible;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_focusDialog)
        {
            _focusDialog = false;
            FocusScope?.Opened(this, _dialog);
            await TryFocusAsync(_dialog);
        }
        else if (_restoreFocus)
        {
            _restoreFocus = false;
            if (FocusScope?.Closed(this) is { } target)
            {
                await TryFocusAsync(target);
            }
        }
    }

    public void Dispose()
    {
        if (_shown)
        {
            FocusScope?.Closed(this);
        }
    }

    // Focus is a convenience: a closed circuit or a pre-rendered page must not turn it into an error.
    private static async Task TryFocusAsync(ElementReference element)
    {
        try { await element.FocusAsync(preventScroll: true); }
        catch (JSDisconnectedException) { }
        catch (JSException) { }
        catch (InvalidOperationException) { }
    }

    private Task OnCloseClicked() => OnClose.InvokeAsync();
    private Task OnBackdropClicked() => CloseOnBackdrop ? OnClose.InvokeAsync() : Task.CompletedTask;

    private Task OnKeyDown(KeyboardEventArgs e)
    {
        if (CloseOnEscape && e.Key == "Escape")
        {
            return OnClose.InvokeAsync();
        }
        return Task.CompletedTask;
    }
}
