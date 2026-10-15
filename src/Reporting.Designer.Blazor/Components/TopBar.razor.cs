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

public partial class TopBar
{
    [Parameter] public string? DocumentName { get; set; }
    [Parameter] public bool IsDarkTheme { get; set; }
    [Parameter] public EventCallback OnToggleTheme { get; set; }
    [Parameter] public EventCallback OnOpenPalette { get; set; }
    [Parameter] public EventCallback OnSave { get; set; }
    [Parameter] public EventCallback<InputFileChangeEventArgs> OnLoad { get; set; }
    [Parameter] public EventCallback<InputFileChangeEventArgs> OnLoadRdl { get; set; }
    [Parameter] public EventCallback<string> OnMenuCommand { get; set; }
    /// <summary>Registered exporters; the File menu lists exactly these (empty or null = no export entries).</summary>
    [Parameter] public IReadOnlyList<Reporting.Designer.Blazor.Services.ExporterDescriptor>? Exporters { get; set; }

    private Task OnToggleThemeClicked() => OnToggleTheme.InvokeAsync();
    private Task OnOpenPaletteClicked() => OnOpenPalette.InvokeAsync();
    private Task OnSaveClicked() => OnSave.InvokeAsync();
    private Task OnLoadFile(InputFileChangeEventArgs e) => OnLoad.InvokeAsync(e);
    private Task OnLoadRdlFile(InputFileChangeEventArgs e) => OnLoadRdl.InvokeAsync(e);
    // A managed handler is required for the @onclick:stopPropagation modifier to take effect (the label's
    // real work — opening the file dialog — happens via its wrapped <InputFile>, natively).
    private void NoOp() { }
}
