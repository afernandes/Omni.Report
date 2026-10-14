using Reporting.Paper;

namespace Reporting.Printing;

/// <summary>Outcome of a print operation.</summary>
public sealed record PrintResult(
    bool Succeeded,
    int PagesPrinted,
    string? OutputPath = null,
    string? ErrorMessage = null,
    Exception? Exception = null);
