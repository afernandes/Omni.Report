using Reporting.Common;
using Reporting.Styling;

namespace Reporting.Elements;

/// <summary>A literal text label — no expressions, no data binding.</summary>
public sealed record LabelElement : ReportElement
{
    public required string Text { get; init; }
}

/// <summary>A data-bound text cell. <see cref="Expression"/> is either a raw expression
/// (e.g. <c>Fields.Total</c>) or a template containing <c>{expr:format}</c> placeholders.</summary>
/// <remarks>
/// <para>RDL F1.8 <em>TextRun + Placeholder</em>: when <see cref="TextRuns"/> is non-empty,
/// the text box renders as a <em>concatenation of styled runs</em> inside the same bounds.
/// Each run carries its own optional <see cref="Style"/> override and its own optional
/// <see cref="ElementAction"/> for inline hyperlinks (so a paragraph can mix bold/italic
/// + plain text + a clickable link without the user creating multiple TextBoxes).</para>
///
/// <para>When <see cref="TextRuns"/> is empty, the legacy single-expression path runs.
/// The renderer falls back to plain <see cref="Expression"/> in that case, preserving
/// behaviour for every existing report.</para>
///
/// <para><b>Current renderer status</b>: runs are concatenated and rendered with the
/// TextBox's <see cref="ReportElement.Style"/>. Per-run style overrides + inline
/// actions round-trip through <c>.repx</c> but the renderer treats them uniformly
/// until the mixed-font drawing path lands. The contract guarantees: a report saved
/// with TextRuns loads back identically, never silently drops them.</para>
/// </remarks>
public sealed record TextBoxElement : ReportElement
{
    public required string Expression { get; init; }

    /// <summary>If true, the element grows vertically to fit wrapped content.</summary>
    public bool CanGrow { get; init; }

    /// <summary>If true, the element shrinks vertically when content is shorter than bounds.</summary>
    public bool CanShrink { get; init; }

    /// <summary>RDL <c>&lt;TextRuns&gt;</c> — ordered list of formatted runs. Each run
    /// has its own value (expression/literal), optional style override, and optional
    /// inline action. Empty = single-expression mode (legacy).</summary>
    public EquatableArray<TextRun> TextRuns { get; init; } = EquatableArray<TextRun>.Empty;
}

/// <summary>
/// RDL <c>&lt;TextRun&gt;</c>: one formatted segment of text inside a TextBox. The
/// renderer concatenates every TextRun of a TextBox in declaration order; layout sees
/// them as a single string but the wire format keeps per-run formatting metadata so
/// future render passes can use it for mixed-style output.
/// </summary>
/// <param name="Value">Expression or literal. Templates are resolved by the engine
/// at render time, same as <see cref="TextBoxElement.Expression"/>.</param>
/// <param name="Style">Optional style override. When null, the run inherits the
/// parent TextBox's style. When non-null, the FIELDS provided here override the
/// matching ones in the parent style (additive merge — null fields are ignored).</param>
/// <param name="Action">Optional inline action — turns this run into a hyperlink /
/// bookmark jump / drillthrough trigger, independent of whether other runs are
/// interactive. Mirrors RDL's per-run <c>&lt;ActionInfo&gt;</c>.</param>
public sealed record TextRun(string Value, Style? Style = null, ElementAction? Action = null);
