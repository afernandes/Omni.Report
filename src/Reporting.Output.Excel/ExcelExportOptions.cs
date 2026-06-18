namespace Reporting.Output.Excel;

public sealed record ExcelExportOptions
{
    /// <summary>Name used for the worksheet. Defaults to the report's name (truncated to 31 chars).</summary>
    public string? SheetName { get; init; }

    /// <summary>Document author embedded in the workbook properties.</summary>
    public string? Author { get; init; }

    /// <summary>Document title; defaults to the report's name.</summary>
    public string? Title { get; init; }

    /// <summary>If true, group-footer sums are emitted as live <c>=SUM(...)</c> formulas
    /// referencing the detail rows of the group rather than the precomputed value.</summary>
    public bool EmitFormulas { get; init; } = true;

    /// <summary>If true, freeze the header row so it stays visible while scrolling.</summary>
    public bool FreezeHeader { get; init; } = true;

    /// <summary>If true, applies a light banded style (alternating row colors) to detail rows.</summary>
    public bool AlternateRowColors { get; init; } = true;

    public static readonly ExcelExportOptions Default = new();
}
