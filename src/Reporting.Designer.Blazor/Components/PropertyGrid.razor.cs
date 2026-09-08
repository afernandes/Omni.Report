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
using System.Globalization;
namespace Reporting.Designer.Blazor.Components;

public partial class PropertyGrid
{
    protected override ElementViewModel? EditedElement => Element;
    [Parameter] public ElementViewModel? Element { get; set; }
    [Parameter] public string BandLabel { get; set; } = "Detail";
    /// <summary>Report-level named-style names for the BasedOn picker (empty = no picker shown).</summary>
    [Parameter] public IReadOnlyList<string>? NamedStyleNames { get; set; }
    /// <summary>Raised when the user saves the selected element's style as a named style; the arg is the chosen name.</summary>
    [Parameter] public EventCallback<string> OnCreateNamedStyle { get; set; }
    /// <summary>Raised to rename a named style (Old → New) — the host re-points every reference.</summary>
    [Parameter] public EventCallback<(string OldName, string NewName)> OnRenameNamedStyle { get; set; }
    /// <summary>Raised to delete a named style by name — the host clears every reference.</summary>
    [Parameter] public EventCallback<string> OnDeleteNamedStyle { get; set; }

    private string? _renamingStyle;   // the named style currently being renamed inline (null = none)
    private string _renameBuffer = string.Empty;

    private void StartRename(string name)
    {
        _renamingStyle = name;
        _renameBuffer = name;
    }

    private async Task CommitRename()
    {
        var from = _renamingStyle;
        var to = _renameBuffer?.Trim();
        _renamingStyle = null;
        if (!string.IsNullOrEmpty(from) && !string.IsNullOrWhiteSpace(to) && !string.Equals(from, to, StringComparison.Ordinal))
        {
            await OnRenameNamedStyle.InvokeAsync((from!, to!));
        }
    }

    // Derives a name (the element's Name if free, else "Estilo N") and asks the host to capture the current style.
    private async Task CreateNamedStyle()
    {
        if (Element is null)
        {
            return;
        }
        var existing = new HashSet<string>(NamedStyleNames ?? Array.Empty<string>(), StringComparer.Ordinal);
        var name = !string.IsNullOrWhiteSpace(Element.Name) && !existing.Contains(Element.Name!)
            ? Element.Name!
            : NextStyleName(existing);
        await OnCreateNamedStyle.InvokeAsync(name);
    }

    private static string NextStyleName(HashSet<string> existing)
    {
        for (var i = 1; ; i++)
        {
            var n = $"Estilo {i}";
            if (!existing.Contains(n)) return n;
        }
    }
    /// <summary>Asks the host to open the rich expression editor. A null argument edits the element's
    /// primary Text/Expression (legacy "Data" fx); a non-null property path edits that property's
    /// expression binding (the metadata grid's per-property "fx").</summary>
    [Parameter] public EventCallback<string?> OnOpenExpressionEditor { get; set; }

    private Task OpenExpressionEditor() => OnOpenExpressionEditor.InvokeAsync(null);

    private ElementViewModel? _subscribed;

    private bool IsBound => Element is not null
        && Element.Kind is DesignerElementKind.TextBox or DesignerElementKind.Barcode
        && !string.IsNullOrEmpty(Element.Expression);

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_subscribed, Element))
        {
            if (_subscribed is not null) _subscribed.Changed -= StateHasChanged;
            _subscribed = Element;
            if (_subscribed is not null) _subscribed.Changed += StateHasChanged;
        }
    }

    public void Dispose()
    {
        if (_subscribed is not null) _subscribed.Changed -= StateHasChanged;
    }

    private RenderFragment Row(string label, string value, Action<string?> setter) => builder =>
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "prop-row");
        {
            builder.OpenElement(2, "div");
            builder.AddAttribute(3, "class", "p-label");
            builder.AddContent(4, label);
            builder.CloseElement();

            builder.OpenElement(5, "div");
            builder.AddAttribute(6, "class", "p-editor");
            {
                builder.OpenElement(7, "input");
                builder.AddAttribute(8, "type", "number");
                builder.AddAttribute(9, "step", "0.1");
                builder.AddAttribute(10, "value", value);
                builder.AddAttribute(11, "style", "flex:1;font-family:var(--font-mono);font-size:11.5px;");
                builder.AddAttribute(12, "onchange", Microsoft.AspNetCore.Components.EventCallback.Factory.Create<ChangeEventArgs>(
                    this, e => setter(e.Value?.ToString())));
                builder.CloseElement();

                builder.OpenElement(13, "span");
                builder.AddAttribute(14, "class", "p-unit");
                builder.AddContent(15, "mm");
                builder.CloseElement();
            }
            builder.CloseElement();
        }
        builder.CloseElement();
    };

    private static string ToMm(Unit u) => u.ToMm().ToString("F1", CultureInfo.InvariantCulture);

    private static string ColorHex(Color c)
    {
        var h = c.ToHex();
        return h.Length == 9 ? "#" + h[3..] : h; // strip alpha for HTML color picker
    }

    // Single per-element source (the [ToolboxElement] annotation via ToolboxCatalog) drives the header icon.
    private static string KindIcon(ElementViewModel e) => ToolboxCatalog.For(e.Kind).Icon;

    private static Unit? ParseMm(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var mm)
            ? Unit.FromMm(mm) : null;
    }

    // ─── Image upload ──────────────────────────────────────────────────────────
    private async Task OnImageSelected(InputFileChangeEventArgs e)
    {
        if (Element is null || e.File is null) return;
        using var ms = new MemoryStream();
        await using var src = e.File.OpenReadStream(maxAllowedSize: 4 * 1024 * 1024);
        await src.CopyToAsync(ms);
        Element.InlineImageData = ms.ToArray();
    }

    /// <summary>Parses a Tablix column-weight input. <c>type="number"</c> always posts an
    /// invariant (dot-decimal) string; blank or non-positive means "equal share" (weight 0).</summary>
    private static double ParseWeight(string? raw)
        => double.TryParse(raw, System.Globalization.NumberStyles.Any,
               System.Globalization.CultureInfo.InvariantCulture, out var w) && w > 0 ? w : 0;

    // ─── Conditional formats ───────────────────────────────────────────────────
    private void AddConditionalFormat()
    {
        Element?.ConditionalFormats.Add(new ConditionalFormatRule());
    }

    private static Color? TryColor(string? hex)
    {
        if (string.IsNullOrEmpty(hex)) return null;
        try { return Color.FromHex(hex); } catch { return null; }
    }

    // ─── RDL interactions (Bookmark / DocumentMap / Action) ────────────────────

    /// <summary>Returns the empty string as null so the PropertyGrid round-trips
    /// "cleared" inputs back to a clean null on the VM — matching how the serializer
    /// treats empty + null identically.</summary>
    private static string? AsNullable(string? v) => string.IsNullOrWhiteSpace(v) ? null : v;

    /// <summary>How many interaction fields are set — used as the section badge count.</summary>
    private static int InteractionCount(ElementViewModel e)
    {
        int n = 0;
        if (!string.IsNullOrWhiteSpace(e.Bookmark)) n++;
        if (!string.IsNullOrWhiteSpace(e.DocumentMapLabel)) n++;
        if (e.HasAction) n++;
        return n;
    }

    private void AddDrillthroughParameter()
    {
        Element?.DrillthroughParameters.Add(new DrillthroughParameterRule { Name = "Param", Value = "" });
    }

}
