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
using System.Globalization;
namespace Reporting.Designer.Blazor.Components;

public partial class Toolbar
{
    [Parameter] public bool CanUndo { get; set; }
    [Parameter] public bool CanRedo { get; set; }
    [Parameter] public bool HasSelection { get; set; }
    [Parameter] public bool HasClipboard { get; set; }
    [Parameter] public bool GridVisible { get; set; } = true;
    [Parameter] public bool SnapEnabled { get; set; } = true;
    [Parameter] public bool RulersVisible { get; set; } = true;
    [Parameter] public double Zoom { get; set; } = 1.0;

    [Parameter] public EventCallback OnUndo { get; set; }
    [Parameter] public EventCallback OnRedo { get; set; }
    [Parameter] public EventCallback OnDelete { get; set; }
    [Parameter] public EventCallback OnPreview { get; set; }
    [Parameter] public EventCallback OnCut { get; set; }
    [Parameter] public EventCallback OnCopy { get; set; }
    [Parameter] public EventCallback OnPaste { get; set; }
    [Parameter] public EventCallback OnPageSetup { get; set; }
    [Parameter] public EventCallback OnAlignLeft { get; set; }
    [Parameter] public EventCallback OnAlignCenterH { get; set; }
    [Parameter] public EventCallback OnAlignRight { get; set; }
    [Parameter] public EventCallback OnAlignTop { get; set; }
    [Parameter] public EventCallback OnAlignMiddleV { get; set; }
    [Parameter] public EventCallback OnAlignBottom { get; set; }
    [Parameter] public EventCallback OnDistributeH { get; set; }
    [Parameter] public EventCallback OnDistributeV { get; set; }
    [Parameter] public EventCallback OnSameWidth { get; set; }
    [Parameter] public EventCallback OnSameHeight { get; set; }
    [Parameter] public EventCallback OnSameBoth { get; set; }
    [Parameter] public EventCallback OnBringForward { get; set; }
    [Parameter] public EventCallback OnSendBackward { get; set; }
    [Parameter] public EventCallback OnBringToFront { get; set; }
    [Parameter] public EventCallback OnSendToBack { get; set; }
    [Parameter] public bool MultiSelected { get; set; }
    [Parameter] public bool CanDistribute { get; set; } // distribute needs 3+ elements to space the middle ones
    [Parameter] public EventCallback<DesignerElementKind> OnInsert { get; set; }
    [Parameter] public EventCallback<bool> OnToggleGrid { get; set; }
    [Parameter] public EventCallback<bool> OnToggleSnap { get; set; }
    [Parameter] public EventCallback<bool> OnToggleRulers { get; set; }
    [Parameter] public EventCallback<double> OnZoomChange { get; set; }

    private Task OnUndoClicked() => OnUndo.InvokeAsync();
    private Task OnRedoClicked() => OnRedo.InvokeAsync();
    private Task OnDeleteClicked() => OnDelete.InvokeAsync();
    private Task OnPreviewClicked() => OnPreview.InvokeAsync();
    private Task OnCutClicked() => OnCut.InvokeAsync();
    private Task OnCopyClicked() => OnCopy.InvokeAsync();
    private Task OnPasteClicked() => OnPaste.InvokeAsync();
    private Task OnPageSetupClicked() => OnPageSetup.InvokeAsync();
}
