using Reporting.Common;
using Reporting.Metadata;
using Reporting.Styling;

namespace Reporting.Elements;

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
