using Reporting.Common;
using Reporting.Geometry;

namespace Reporting.Elements;

/// <summary>A single column of a <see cref="TableElement"/>, with header, detail, and footer cell expressions.</summary>
public sealed record TableColumn(
    string Name,
    Unit Width,
    string? HeaderText = null,
    string? DetailExpression = null,
    string? FooterExpression = null);

/// <summary>A simple banded table — header row, repeating detail row, optional footer row.
/// More flexible composition (multi-row headers, nested groups) is achieved via subreports.</summary>
public sealed record TableElement : ReportElement
{
    public EquatableArray<TableColumn> Columns { get; init; } = EquatableArray<TableColumn>.Empty;

    public Unit HeaderHeight { get; init; } = Unit.FromMm(7);
    public Unit DetailHeight { get; init; } = Unit.FromMm(6);
    public Unit FooterHeight { get; init; } = Unit.Zero;

    /// <summary>Optional expression yielding the data source (else the band's source is used).</summary>
    public string? DataExpression { get; init; }
}
