using Reporting.Common;
using Reporting.Geometry;
using Reporting.Metadata;

namespace Reporting.Elements;

// ─────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// RDL <c>Code</c> element — a block of custom C# (originally VB.NET in SSRS) that
/// declares helper functions reachable from expressions as <c>Code.MethodName(...)</c>.
/// We keep the source text alongside the optional language tag (default C#) so the
/// .repx round-trips lossless independently of whether code execution is enabled.
/// </summary>
/// <remarks>
/// Source is preserved verbatim and round-trips losslessly. Execution is OPT-IN via the Roslyn
/// code resolver (<c>Reporting.Expressions.Roslyn</c>) — RCE by design, disabled by default; enable
/// only for trusted report sources. Without a resolver configured, <c>Code.X</c> calls are unavailable.
/// </remarks>
public sealed record CodeElement : ReportElement
{
    /// <summary>Source text of the code block (typically a few helper methods).</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>Source language. Defaults to <c>CSharp</c>; legacy <c>VB</c> kept for
    /// SSRS interop.</summary>
    public CodeLanguage Language { get; init; } = CodeLanguage.CSharp;
}
