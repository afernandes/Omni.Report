namespace Reporting.Layout.Internal;

internal sealed class PaginationBudget
{
    private readonly PaginationLimits _limits;
    private long _sourceRows;
    private long _iterationRows;
    private long _pages;
    private long _primitives;

    public PaginationBudget(PaginationLimits? limits)
    {
        _limits = limits ?? new();
        Validate(_limits.MaxSourceRows, nameof(PaginationLimits.MaxSourceRows));
        Validate(_limits.MaxIterationRows, nameof(PaginationLimits.MaxIterationRows));
        Validate(_limits.MaxPages, nameof(PaginationLimits.MaxPages));
        Validate(_limits.MaxPrimitives, nameof(PaginationLimits.MaxPrimitives));
    }

    public void SourceRow() => Consume(ref _sourceRows, _limits.MaxSourceRows, nameof(PaginationLimits.MaxSourceRows));
    public void IterationRow() => Consume(ref _iterationRows, _limits.MaxIterationRows, nameof(PaginationLimits.MaxIterationRows));
    public void Page() => Consume(ref _pages, _limits.MaxPages, nameof(PaginationLimits.MaxPages));
    public void Primitive() => Consume(ref _primitives, _limits.MaxPrimitives, nameof(PaginationLimits.MaxPrimitives));

    private static void Validate(long? limit, string name)
    {
        if (limit < 0) throw new ArgumentOutOfRangeException(name, "A budget cannot be negative.");
    }

    private static void Consume(ref long count, long? limit, string name)
    {
        if (limit is { } maximum && count >= maximum)
            throw new PaginationLimitExceededException(name, maximum);
        count = checked(count + 1);
    }
}
