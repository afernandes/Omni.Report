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
