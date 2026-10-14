using Reporting.Common;
using Reporting.Geometry;
using Reporting.Metadata;

namespace Reporting.Elements;

/// <summary>One axis of a Tablix grouping — name + group-key expression + sort.</summary>
public sealed record TablixGroup(
    string Name,
    string? GroupExpression = null,
    string? SortExpression = null,
    bool SortDescending = false);
