using Reporting.Layout;

namespace Reporting.Designer.Blazor.Services;

/// <summary>Lightweight printer descriptor — what the dialog needs to render a usable
/// dropdown. Kept independent of <c>Reporting.Printing.PrinterInfo</c> so the designer
/// package doesn't drag the Printing assembly in browser-only scenarios.</summary>
/// <param name="Name">Stable identifier — passed back as <see cref="PrintRequest.PrinterName"/>.</param>
/// <param name="DisplayName">Human label shown in the dropdown. Often matches <paramref name="Name"/>.</param>
/// <param name="IsDefault">True for the OS default printer — pre-selected in the dialog.</param>
/// <param name="Kind">Driver hint (Spooler / EscPos / Pdf …). Drives the icon next to the name.</param>
public sealed record DesignerPrinter(
    string Name,
    string DisplayName,
    bool IsDefault = false,
    string? Kind = null);
