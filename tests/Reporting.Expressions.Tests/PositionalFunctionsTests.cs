using FluentAssertions;
using Reporting.Expressions;
using Xunit;

namespace Reporting.Expressions.Tests;

/// <summary>
/// SSRS positional/scope functions: RowNumber, CountRows, Previous (positional) and First/Last/
/// CountDistinct (scope reductions). Rows accumulate via SetCurrentRow; the current row is the last one.
/// </summary>
public class PositionalFunctionsTests
{
    private static (ReportExpressionContext ctx, ExpressionEvaluator ev) WithRows(params int[] values)
    {
        var ev = new ExpressionEvaluator();
        var ctx = new ReportExpressionContext(ev);
        foreach (var v in values)
        {
            ctx.SetCurrentRow(new Dictionary<string, object?> { ["V"] = v });
        }
        return (ctx, ev);
    }

    [Fact]
    public void RowNumber_is_the_1based_position_of_the_current_row()
    {
        var (ctx, ev) = WithRows(10, 20, 30);
        ev.Evaluate("RowNumber()", ctx).Should().Be(3);
    }

    [Fact]
    public void CountRows_counts_the_rows_in_scope()
    {
        var (ctx, ev) = WithRows(10, 20, 30);
        ev.Evaluate("CountRows()", ctx).Should().Be(3);
    }

    [Fact]
    public void Previous_returns_the_prior_rows_value_and_null_on_the_first()
    {
        var (ctx, ev) = WithRows(10, 20, 30);
        ev.Evaluate("Previous(Fields.V)", ctx).Should().Be(20); // row before current (30) is 20

        var (first, ev2) = WithRows(99);
        ev2.Evaluate("Previous(Fields.V)", first).Should().BeNull();
    }

    [Fact]
    public void First_and_Last_return_the_scope_endpoints()
    {
        var (ctx, ev) = WithRows(10, 20, 30);
        ev.Evaluate("First(Fields.V)", ctx).Should().Be(10);
        ev.Evaluate("Last(Fields.V)", ctx).Should().Be(30);
    }

    [Fact]
    public void CountDistinct_counts_distinct_non_null_values()
    {
        var (ctx, ev) = WithRows(10, 20, 10, 30, 20);
        ev.Evaluate("CountDistinct(Fields.V)", ctx).Should().Be(3); // {10,20,30}
    }
}
