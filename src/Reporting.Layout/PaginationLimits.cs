namespace Reporting.Layout;

/// <summary>Optional work budgets for one execution, shared by subreports and layout passes.
/// Null limits preserve the unrestricted behavior. Counts measure work, not final output size.</summary>
public sealed record PaginationLimits
{
    /// <summary>Maximum rows materialized from all providers combined. A provider may yield one extra row to detect overflow.</summary>
    public long? MaxSourceRows { get; init; }

    /// <summary>Maximum iteration rows constructed, including expanded relations and subreports.</summary>
    public long? MaxIterationRows { get; init; }

    /// <summary>Maximum pages completed across all passes and subreports.</summary>
    public long? MaxPages { get; init; }

    /// <summary>Maximum primitives accumulated across all passes and subreports.</summary>
    public long? MaxPrimitives { get; init; }
}
