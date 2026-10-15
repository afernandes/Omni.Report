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
namespace Reporting.Designer.Blazor.Components;

public partial class GroupBindingDialog
{
    [Parameter] public bool Visible { get; set; }
    [Parameter] public BandViewModel? Band { get; set; }
    [Parameter] public IReadOnlyList<DesignerDataSource> DataSources { get; set; } = Array.Empty<DesignerDataSource>();
    [Parameter] public EventCallback OnApplied { get; set; }
    [Parameter] public EventCallback OnClosed { get; set; }

    private string _expression = string.Empty;
    private string _selectedField = string.Empty;
    private BandViewModel? _tracked;

    protected override void OnParametersSet()
    {
        if (Visible && !ReferenceEquals(_tracked, Band))
        {
            _tracked = Band;
            _expression = Band?.GroupExpression ?? string.Empty;
            // If the existing expression is just a Fields.X reference, preselect it in the
            // dropdown so the user sees their current choice; otherwise leave blank (custom).
            _selectedField = _expression.StartsWith("Fields.") ? _expression : string.Empty;
        }
        else if (!Visible)
        {
            _tracked = null;
        }
    }

    private void OnFieldPicked()
    {
        if (!string.IsNullOrEmpty(_selectedField))
        {
            _expression = _selectedField;
        }
    }

    private async Task HandleApply()
    {
        if (Band is null)
        {
            return;
        }
        Band.GroupExpression = string.IsNullOrWhiteSpace(_expression) ? null : _expression.Trim();
        await OnApplied.InvokeAsync();
        await OnClosed.InvokeAsync();
    }

    private async Task HandleClear()
    {
        if (Band is null)
        {
            return;
        }
        Band.GroupExpression = null;
        await OnApplied.InvokeAsync();
        await OnClosed.InvokeAsync();
    }
}
