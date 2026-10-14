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
