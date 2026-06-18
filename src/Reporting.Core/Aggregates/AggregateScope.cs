namespace Reporting.Aggregates;

/// <summary>Scope over which an aggregate function (Sum, Avg, …) is computed.</summary>
public enum AggregateScope
{
    /// <summary>The entire dataset (default for aggregates in report footer).</summary>
    Report,

    /// <summary>The current page (recomputed per page).</summary>
    Page,

    /// <summary>The current group (footer of a <c>GroupBand</c>).</summary>
    Group,

    /// <summary>Running total from the start of the scope up to the current row.</summary>
    Running,
}
