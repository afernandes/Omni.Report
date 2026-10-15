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

public partial class MonacoEditor
{
    /// <summary>Stable DOM id — required because Monaco mounts on a real element by id.</summary>
    [Parameter] public string DomId { get; set; } = "monaco-" + Guid.NewGuid().ToString("n")[..8];

    [Parameter] public string Height { get; set; } = "220px";

    /// <summary>Initial editor text. Updates to this parameter AFTER mount are ignored;
    /// the editor owns the state. Use <see cref="SetValueAsync"/> for programmatic writes.</summary>
    [Parameter] public string InitialValue { get; set; } = string.Empty;

    [Parameter] public string Language { get; set; } = "omnireport-expression";

    [Parameter] public string Theme { get; set; } = "light";

    /// <summary>Auto-completion items shown when the user types. Each item: label, kind
    /// ("field" / "parameter" / "function" / "keyword" / "snippet"), insertText, detail.</summary>
    [Parameter] public IReadOnlyList<MonacoCompletionItem> Completions { get; set; } = Array.Empty<MonacoCompletionItem>();

    /// <summary>Fires whenever the user edits the value. The host should NOT push the value
    /// back via SetValue — Monaco owns the source of truth while mounted.</summary>
    [Parameter] public EventCallback<string> OnValueChanged { get; set; }

    private DotNetObjectReference<MonacoEditor>? _selfRef;
    private bool _mounted;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !_mounted)
        {
            _mounted = true;
            _selfRef ??= DotNetObjectReference.Create(this);
            try
            {
                await Js.InvokeVoidAsync("omniMonaco.mount", new
                {
                    domId = DomId,
                    initialValue = InitialValue,
                    theme = Theme,
                    language = Language,
                    completions = Completions,
                    dotnetRef = _selfRef,
                });
            }
            catch (JSException ex)
            {
                Console.Error.WriteLine($"Monaco mount failed: {ex.Message}");
            }
            catch (InvalidOperationException) { /* pre-render */ }
        }
    }

    [JSInvokable]
    public Task OnEditorValueChanged(string value)
        => OnValueChanged.HasDelegate ? OnValueChanged.InvokeAsync(value) : Task.CompletedTask;

    public async Task<string?> GetValueAsync()
    {
        try { return await Js.InvokeAsync<string?>("omniMonaco.getValue", DomId); }
        catch { return null; }
    }

    public async Task SetValueAsync(string value)
    {
        try { await Js.InvokeVoidAsync("omniMonaco.setValue", DomId, value); }
        catch { }
    }

    public async Task InsertAsync(string text)
    {
        try { await Js.InvokeVoidAsync("omniMonaco.insertAtCursor", DomId, text); }
        catch { }
    }

    public async ValueTask DisposeAsync()
    {
        if (_mounted)
        {
            try { await Js.InvokeVoidAsync("omniMonaco.unmount", DomId); }
            catch { }
        }
        _selfRef?.Dispose();
    }
}
