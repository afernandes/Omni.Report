namespace Reporting.Layout;

/// <summary>Execution stopped before exceeding an explicitly configured work budget.</summary>
public sealed class PaginationLimitExceededException : InvalidOperationException
{
    /// <summary>Creates a failure identifying the exhausted budget and configured maximum.</summary>
    public PaginationLimitExceededException(string budget, long limit)
        : base($"Pagination budget '{budget}' exceeded its limit of {limit}.")
    {
        Budget = budget;
        Limit = limit;
    }

    /// <summary>Name of the exhausted <see cref="PaginationLimits"/> property.</summary>
    public string Budget { get; }

    /// <summary>Configured maximum.</summary>
    public long Limit { get; }
}
