using Microsoft.AspNetCore.Components;
using Reporting.Designer.Blazor.Services;

namespace Reporting.Designer.Blazor.Components;

/// <summary>One menu entry rendered from a <see cref="DesignerCommand"/>, with the separator that precedes its group.</summary>
public partial class MenuCommandItem
{
    /// <summary>The command this entry runs.</summary>
    [Parameter, EditorRequired] public DesignerCommand Command { get; set; } = default!;

    /// <summary>Receives the command id when the entry is chosen.</summary>
    [Parameter] public EventCallback<string> OnSelect { get; set; }

    private Task Select() => OnSelect.InvokeAsync(Command.Id);
}
