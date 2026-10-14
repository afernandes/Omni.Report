using Reporting.Common;

namespace Reporting.Elements;

/// <summary>Parameter passed to a drillthrough target report. Mirrors RDL
/// <c>&lt;Parameter&gt;</c> inside <c>&lt;Drillthrough&gt;</c>.</summary>
/// <param name="Name">Name of the parameter on the target report.</param>
/// <param name="Value">Expression evaluated in the SOURCE report's context; the value is
/// passed to the target report as the parameter's bound value.</param>
/// <param name="Omit">If true, the parameter is not passed — useful for conditionally
/// suppressing parameters (RDL <c>&lt;Omit&gt;</c>).</param>
public sealed record DrillthroughParameter(string Name, string Value, bool Omit = false);
