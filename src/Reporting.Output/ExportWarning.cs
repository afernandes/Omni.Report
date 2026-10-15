namespace Reporting.Output;

/// <summary>
/// A fidelity loss an exporter took while writing. Some formats genuinely cannot carry everything a report
/// contains — a CSV has no place for a chart — and that is fine; what is not fine is the loss being
/// <b>silent</b>, which is how a user discovers at the worst moment that their logo never made it into the
/// spreadsheet.
/// </summary>
/// <param name="Code">Stable, machine-readable reason (e.g. <c>"primitive-not-representable"</c>) so hosts
/// can filter or aggregate without parsing prose.</param>
/// <param name="Message">Human-readable explanation, already localised to the library's pt-BR/English mix
/// used elsewhere in user-facing strings.</param>
/// <param name="Count">How many items were affected (1 when the warning is about a single thing).</param>
public sealed record ExportWarning(string Code, string Message, int Count = 1)
{
    /// <summary>Code used when a layout primitive has no representation in the target format.</summary>
    public const string PrimitiveNotRepresentable = "primitive-not-representable";

    /// <inheritdoc/>
    public override string ToString() => $"[{Code}] {Message}";
}
