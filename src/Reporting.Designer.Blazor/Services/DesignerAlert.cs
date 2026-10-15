namespace Reporting.Designer.Blazor.Services;

/// <summary>A message shown to the author in the Designer's alert banner until dismissed: a failure (preview,
/// export, opening a file) or the outcome of a check such as expression validation.</summary>
/// <param name="Title">Short heading, e.g. "Falha na exportação".</param>
/// <param name="Message">What happened, in terms the author can act on.</param>
/// <param name="Details">Optional list of items (e.g. one line per invalid expression).</param>
/// <param name="IsError">False for a successful outcome, rendered in the success colour.</param>
internal sealed record DesignerAlert(string Title, string Message, IReadOnlyList<string>? Details = null, bool IsError = true);
