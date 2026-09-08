using Microsoft.AspNetCore.Components;
using Reporting.Designer.Blazor.ViewModels;
namespace Reporting.Designer.Blazor.Components;

/// <summary>Records one immutable element snapshot for each completed editor event.</summary>
public abstract class DesignerEditComponent : ComponentBase, IHandleEvent
{
    [CascadingParameter] public CommandHistory? EditHistory { get; set; }
    protected abstract ElementViewModel? EditedElement { get; }
    async Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object? arg)
    {
        var element = EditedElement;
        var history = EditHistory;
        var revision = history?.Revision;
        var before = element?.ToElement();
        try
        {
            await callback.InvokeAsync(arg);
        }
        finally
        {
            if (element is not null && before is not null && history is not null && history.Revision == revision)
            {
                var after = element.ToElement();
                if (!Equals(before, after)) history.RecordExecuted(new ElementSnapshotCommand(element, before, after));
            }
            StateHasChanged();
        }
    }
}
