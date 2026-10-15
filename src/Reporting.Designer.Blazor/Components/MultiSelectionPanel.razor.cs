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
using System.Globalization;
namespace Reporting.Designer.Blazor.Components;

public partial class MultiSelectionPanel
{
    [Parameter, EditorRequired] public IReadOnlyList<ElementViewModel> Selection { get; set; } = [];

    private readonly HashSet<ElementViewModel> _subscribed = new();

    protected override void OnParametersSet()
    {
        // Subscribe/unsubscribe per-element so any external mutation re-renders the panel.
        var current = new HashSet<ElementViewModel>(Selection);
        foreach (var stale in _subscribed.Except(current).ToArray())
        {
            stale.Changed -= StateHasChanged;
            _subscribed.Remove(stale);
        }
        foreach (var added in current.Except(_subscribed).ToArray())
        {
            added.Changed += StateHasChanged;
            _subscribed.Add(added);
        }
    }

    public void Dispose()
    {
        foreach (var e in _subscribed)
        {
            e.Changed -= StateHasChanged;
        }
        _subscribed.Clear();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────
    private void Apply(Action<ElementViewModel> mutate)
    {
        foreach (var el in Selection)
        {
            mutate(el);
        }
    }

    // Positional align / distribute: compute every new top-left from the whole selection's bounds first,
    // then write them back (each X/Y setter raises Changed, re-rendering the canvas).
    private void ApplyArrange(Arrange.Op op)
    {
        if (Selection.Count < 2)
        {
            return;
        }
        var positions = Arrange.Compute(op, Selection.Select(e => e.Bounds).ToList());
        for (var i = 0; i < Selection.Count; i++)
        {
            var b = Selection[i].Bounds;
            Selection[i].Bounds = new Rectangle(positions[i].X, positions[i].Y, b.Width, b.Height);
        }
    }

    private string CommonValue(Func<ElementViewModel, string?> selector, string fallback)
    {
        if (Selection.Count == 0)
        {
            return fallback;
        }
        var first = selector(Selection[0]);
        foreach (var e in Selection.Skip(1))
        {
            if (!Equals(selector(e), first))
            {
                return fallback;
            }
        }
        return first ?? fallback;
    }

    private T? CommonValue<T>(Func<ElementViewModel, T> selector, T? fallback) where T : struct
    {
        if (Selection.Count == 0)
        {
            return fallback;
        }
        var first = selector(Selection[0]);
        foreach (var e in Selection.Skip(1))
        {
            if (!EqualityComparer<T>.Default.Equals(selector(e), first))
            {
                return fallback;
            }
        }
        return first;
    }

    private Color? CommonColor(Func<ElementViewModel, Color> selector)
    {
        if (Selection.Count == 0)
        {
            return null;
        }
        var first = selector(Selection[0]);
        foreach (var e in Selection.Skip(1))
        {
            if (!selector(e).Equals(first))
            {
                return null;
            }
        }
        return first;
    }

    private TriBool TriState(Func<ElementViewModel, bool> selector)
    {
        if (Selection.Count == 0)
        {
            return TriBool.AllOff;
        }
        var any = false; var all = true;
        foreach (var e in Selection)
        {
            var v = selector(e);
            any |= v; all &= v;
        }
        return all ? TriBool.AllOn : any ? TriBool.Mixed : TriBool.AllOff;
    }

    private void ToggleBool(Func<ElementViewModel, bool> getter, Action<ElementViewModel, bool> setter)
    {
        // If mixed or all-off → flip to all-on. If all-on → flip to all-off. Matches the
        // VS WinForms behaviour where clicking a "mixed" toggle unifies upward.
        var target = TriState(getter) != TriBool.AllOn;
        foreach (var e in Selection)
        {
            setter(e, target);
        }
    }

    private RenderFragment MixedRow(string label, double[] values, Action<double> setter) => builder =>
    {
        var first = values.Length > 0 ? values[0] : 0;
        var same = values.All(v => Math.Abs(v - first) < 0.001);
        var display = same ? first.ToString("F1", CultureInfo.InvariantCulture) : "";

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
                builder.AddAttribute(10, "value", display);
                builder.AddAttribute(11, "placeholder", same ? "" : "—");
                builder.AddAttribute(12, "style", "flex:1;font-family:var(--font-mono);font-size:11.5px;");
                builder.AddAttribute(13, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, e =>
                {
                    var s = e.Value?.ToString();
                    if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                    {
                        setter(d);
                    }
                }));
                builder.CloseElement();

                builder.OpenElement(14, "span");
                builder.AddAttribute(15, "class", "p-unit");
                builder.AddContent(16, "mm");
                builder.CloseElement();
            }
            builder.CloseElement();
        }
        builder.CloseElement();
    };

    private void SetFontSize(string? raw)
    {
        if (!double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
        {
            return;
        }
        Apply(el => { if (HasTextContent(el)) { el.FontSize = d; } });
    }

    private void SetForeColor(string? hex)
    {
        if (string.IsNullOrEmpty(hex))
        {
            return;
        }
        try
        {
            var c = Color.FromHex(hex);
            Apply(el => { if (HasTextContent(el)) { el.ForeColor = c; } });
        }
        catch { /* invalid hex — ignore */ }
    }

    private static bool HasTextContent(ElementViewModel e)
        => e.Kind is DesignerElementKind.Label or DesignerElementKind.TextBox or DesignerElementKind.Barcode;

    private static string ColorHex(Color c)
    {
        var h = c.ToHex();
        return h.Length == 9 ? "#" + h[3..] : h;
    }

    private string KindsBreakdown()
    {
        var groups = Selection.GroupBy(e => e.Kind).OrderByDescending(g => g.Count()).ToArray();
        if (groups.Length == 1)
        {
            return $"{groups[0].Count()}× {groups[0].Key}";
        }
        return string.Join(" · ", groups.Select(g => $"{g.Count()}× {g.Key}"));
    }
}
