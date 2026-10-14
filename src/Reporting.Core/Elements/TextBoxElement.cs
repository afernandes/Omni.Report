using Reporting.Common;
using Reporting.Metadata;
using Reporting.Styling;

namespace Reporting.Elements;

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
[TextStyled]
public sealed record TextBoxElement : ReportElement
{
    /// <summary>Either a raw expression (<c>Fields.Total</c>) or a template mixing literal text with
    /// <c>{expr:format}</c> placeholders. Ignored when <c>TextRuns</c> is non-empty.</summary>
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
