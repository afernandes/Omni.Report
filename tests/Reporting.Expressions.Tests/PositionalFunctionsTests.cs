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

    [Fact]
    public void CountDistinct_treats_equal_numerics_across_types_as_one()
    {
        var ev = new ExpressionEvaluator();
        var ctx = new ReportExpressionContext(ev);
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["V"] = 1 });    // int
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["V"] = 1m });   // decimal — same value
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["V"] = 2.0 });  // double
        ev.Evaluate("CountDistinct(Fields.V)", ctx).Should().Be(2);          // {1, 2}
    }

    [Fact]
    public void Previous_respects_group_scope()
    {
        var ev = new ExpressionEvaluator();
        var ctx = new ReportExpressionContext(ev);
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["V"] = 10 });
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["V"] = 20 });
        ctx.ResetGroup(); // a new group starts
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["V"] = 30 });

        // At the first row of the new group there's no previous IN-GROUP, but report scope still has one.
        ev.Evaluate("Previous(Fields.V, 'Group')", ctx).Should().BeNull();
        ev.Evaluate("Previous(Fields.V, 'Report')", ctx).Should().Be(20);

        ctx.SetCurrentRow(new Dictionary<string, object?> { ["V"] = 40 });
        ev.Evaluate("Previous(Fields.V, 'Group')", ctx).Should().Be(30); // previous within the group
    }
}
