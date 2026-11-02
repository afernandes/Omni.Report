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

public partial class MonacoEditor
{
    [Inject] private IServiceProvider Services { get; set; } = default!;

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

    /// <summary>Accessible name of the editor, read by screen readers (e.g. "Expressão", "Consulta SQL").</summary>
    [Parameter] public string Label { get; set; } = "Editor de código";

    private DotNetObjectReference<MonacoEditor>? _selfRef;
    private bool _mounted;

    // Monaco did not load (offline host with a remote MonacoOptions.BaseUrl, blocked script, timeout): the editor
    // becomes a plain text box so the expression stays editable. The value then lives here instead of in Monaco.
    private bool _fallback;
    private string _fallbackValue = string.Empty;

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
                    // null = the copy packaged with the Designer (resolved by monaco-host.js).
                    baseUrl = (Services.GetService(typeof(MonacoOptions)) as MonacoOptions)?.BaseUrl,
                    ariaLabel = Label,
                });
            }
            catch (JSException ex)
            {
                var logger = Services.GetService(typeof(Microsoft.Extensions.Logging.ILogger<MonacoEditor>))
                    as Microsoft.Extensions.Logging.ILogger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
                Reporting.Designer.Blazor.Services.DesignerLog.MonacoMountFailed(logger, ex);
                _fallbackValue = InitialValue;
                _fallback = true;
                StateHasChanged();
            }
            catch (InvalidOperationException) { /* pre-render */ }
        }
    }

    [JSInvokable]
    public Task OnEditorValueChanged(string value)
        => OnValueChanged.HasDelegate ? OnValueChanged.InvokeAsync(value) : Task.CompletedTask;

    public async Task<string?> GetValueAsync()
    {
        if (_fallback)
        {
            return _fallbackValue;
        }
        try { return await Js.InvokeAsync<string?>("omniMonaco.getValue", DomId); }
        catch { return null; }
    }

    public async Task SetValueAsync(string value)
    {
        if (_fallback)
        {
            await SetFallbackValueAsync(value);
            return;
        }
        try { await Js.InvokeVoidAsync("omniMonaco.setValue", DomId, value); }
        catch { }
    }

    /// <summary>Inserts at the cursor; in the plain-text fallback, which does not track the cursor, appends.</summary>
    public async Task InsertAsync(string text)
    {
        if (_fallback)
        {
            await SetFallbackValueAsync(_fallbackValue + text);
            return;
        }
        try { await Js.InvokeVoidAsync("omniMonaco.insertAtCursor", DomId, text); }
        catch { }
    }

    private Task OnFallbackInput(ChangeEventArgs e) => SetFallbackValueAsync(e.Value?.ToString() ?? string.Empty);

    // Same notification Monaco gives through OnEditorValueChanged, so the hosting dialog cannot tell the difference.
    // A programmatic write (SetValueAsync/InsertAsync) does not come from an event of this component, so it asks for
    // the render itself — otherwise the box keeps showing the old text and the next keystroke overwrites the new one.
    private Task SetFallbackValueAsync(string value)
    {
        _fallbackValue = value;
        StateHasChanged();
        return OnValueChanged.HasDelegate ? OnValueChanged.InvokeAsync(value) : Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_mounted && !_fallback)
        {
            try { await Js.InvokeVoidAsync("omniMonaco.unmount", DomId); }
            catch { }
        }
        _selfRef?.Dispose();
    }
}
