using FluentAssertions;
using Reporting.Aggregates;
using Reporting.Expressions;
using Xunit;

namespace Reporting.Expressions.Tests;

/// <summary>
/// SSRS <c>RunningValue(expression, function, [scope])</c> — a cumulative aggregate whose 2nd argument is the
/// inner aggregate's NAME (a bare identifier, kept as raw text rather than evaluated). The running/cumulative
/// behaviour rides on the scope buffer the engine already maintains for RunningTotal.
/// </summary>
public class RunningValueTests
{
    private static (ReportExpressionContext ctx, ExpressionEvaluator ev) ContextWithRows(params decimal[] totals)
    {
        var ev = new ExpressionEvaluator();
        var ctx = new ReportExpressionContext(ev);
        foreach (var t in totals)
        {
            ctx.SetCurrentRow(new Dictionary<string, object?> { ["Total"] = t });
        }
        return (ctx, ev);
    }

    [Fact]
    public void RunningValue_with_sum_accumulates_over_the_scope_buffer()
    {
        var (ctx, ev) = ContextWithRows(10m, 25m, 5m);
        // The 2nd arg "Sum" is the inner function (raw identifier) — proven by getting 40 rather than an error
        // (it is NOT evaluated as a field/parameter).
        ev.Evaluate("RunningValue(Fields.Total, Sum)", ctx).Should().Be(40m);
    }

    [Fact]
    public void RunningValue_honours_the_inner_function()
    {
        var (ctx, ev) = ContextWithRows(10m, 25m, 5m);
        ev.Evaluate("RunningValue(Fields.Total, Count)", ctx).Should().Be(3);
        ev.Evaluate("RunningValue(Fields.Total, Max)", ctx).Should().Be(25m);
        ev.Evaluate("RunningValue(Fields.Total, Min)", ctx).Should().Be(5m);
    }

    [Fact]
    public void RunningValue_accepts_an_explicit_scope_argument()
    {
        var (ctx, ev) = ContextWithRows(10m, 20m);
        ev.Evaluate("RunningValue(Fields.Total, Sum, 'Group')", ctx).Should().Be(30m);
    }

    [Fact]
    public void RunningValue_falls_back_to_sum_for_an_unknown_inner_function()
    {
        var (ctx, ev) = ContextWithRows(10m, 25m, 5m);
        ev.Evaluate("RunningValue(Fields.Total, Bogus)", ctx).Should().Be(40m);
    }

    [Fact]
    public void RunningValue_composes_inside_a_larger_expression()
    {
        var (ctx, ev) = ContextWithRows(10m, 25m, 5m);
        ev.Evaluate("RunningValue(Fields.Total, Sum) * 2", ctx).Should().Be(80m);
    }

    [Fact]
    public void RunningValue_accumulates_a_composite_inner_expression()
    {
        var ev = new ExpressionEvaluator();
        var ctx = new ReportExpressionContext(ev);
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["A"] = 2m, ["B"] = 3m });
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["A"] = 4m, ["B"] = 1m });
        // Sum of (A + B) over both rows = 5 + 5 = 10.
        ev.Evaluate("RunningValue(Fields.A + Fields.B, Sum)", ctx).Should().Be(10m);
    }
}
