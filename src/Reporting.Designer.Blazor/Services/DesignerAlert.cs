namespace Reporting.Designer.Blazor.Services;

/// <summary>A failure shown to the author in the Designer's alert banner (preview or export), until dismissed.</summary>
/// <param name="Title">Short heading, e.g. "Falha na exportação".</param>
/// <param name="Message">What went wrong, in terms the author can act on.</param>
internal sealed record DesignerAlert(string Title, string Message);
