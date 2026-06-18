using FluentAssertions;
using Reporting.Aggregates;
using Reporting.Expressions;
using Xunit;

namespace Reporting.Expressions.Tests;

/// <summary>
/// Coverage for Sum/Avg/Count/Min/Max + scopes (Report/Page/Group/Running) — the gap the
/// coverage audit flagged as highest production risk (group footers with empty groups,
/// page totals across page breaks, etc.).
/// </summary>
public class AggregateCalculatorTests
{
    private static ReportExpressionContext NewCtx() => new();

    private static void PushRow(ReportExpressionContext ctx, decimal total, string? customer = null)
    {
        ctx.SetCurrentRow(new Dictionary<string, object?>
        {
            ["Total"] = total,
            ["Customer"] = customer,
        });
    }

    [Fact]
    public void Sum_report_scope_aggregates_every_row()
    {
        var ctx = NewCtx();
        PushRow(ctx, 10m);
        PushRow(ctx, 20m);
        PushRow(ctx, 30m);
        ctx.EvaluateAggregate("Sum", "[Fields.Total]", AggregateScope.Report)
            .Should().Be(60m);
    }

    [Fact]
    public void Sum_group_scope_resets_when_group_resets()
    {
        var ctx = NewCtx();
        PushRow(ctx, 10m);
        PushRow(ctx, 20m);
        ctx.EvaluateAggregate("Sum", "[Fields.Total]", AggregateScope.Group)
            .Should().Be(30m);

        ctx.ResetGroup();
        PushRow(ctx, 5m);
        ctx.EvaluateAggregate("Sum", "[Fields.Total]", AggregateScope.Group)
            .Should().Be(5m, "after ResetGroup the group window is the new rows only");
    }

    [Fact]
    public void Sum_on_empty_group_returns_zero_not_null_or_crash()
    {
        // Production bug-class: group footer evaluates Sum() but the group had no rows
        // (e.g. filtered out). Engine must return 0 — not throw, not null.
        var ctx = NewCtx();
        ctx.ResetGroup();
        ctx.EvaluateAggregate("Sum", "[Fields.Total]", AggregateScope.Group)
            .Should().Be(0m);
    }

    [Fact]
    public void Count_on_empty_group_returns_zero()
    {
        var ctx = NewCtx();
        ctx.ResetGroup();
        ctx.EvaluateAggregate("Count", "[Fields.Total]", AggregateScope.Group)
            .Should().Be(0);
    }

    [Fact]
    public void Avg_on_empty_group_returns_zero_not_division_by_zero()
    {
        var ctx = NewCtx();
        ctx.ResetGroup();
        ctx.EvaluateAggregate("Avg", "[Fields.Total]", AggregateScope.Group)
            .Should().Be(0m);
    }

    [Fact]
    public void Page_scope_resets_across_page_boundary()
    {
        var ctx = NewCtx();
        PushRow(ctx, 100m);
        PushRow(ctx, 200m);
        ctx.EvaluateAggregate("Sum", "[Fields.Total]", AggregateScope.Page)
            .Should().Be(300m);

        ctx.ResetPage();
        PushRow(ctx, 50m);
        ctx.EvaluateAggregate("Sum", "[Fields.Total]", AggregateScope.Page)
            .Should().Be(50m, "page total restarts on each page");

        // Report scope keeps the full history.
        ctx.EvaluateAggregate("Sum", "[Fields.Total]", AggregateScope.Report)
            .Should().Be(350m);
    }

    [Fact]
    public void Min_max_skip_nulls()
    {
        var ctx = NewCtx();
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["V"] = (decimal?)null });
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["V"] = 10m });
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["V"] = 5m });
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["V"] = 20m });

        ctx.EvaluateAggregate("Min", "[Fields.V]", AggregateScope.Report).Should().Be(5m);
        ctx.EvaluateAggregate("Max", "[Fields.V]", AggregateScope.Report).Should().Be(20m);
    }

    [Fact]
    public void Min_max_on_empty_scope_returns_null()
    {
        var ctx = NewCtx();
        ctx.EvaluateAggregate("Min", "[Fields.V]", AggregateScope.Report).Should().BeNull();
        ctx.EvaluateAggregate("Max", "[Fields.V]", AggregateScope.Report).Should().BeNull();
    }

    [Fact]
    public void Running_scope_uses_group_buffer()
    {
        var ctx = NewCtx();
        PushRow(ctx, 10m);
        PushRow(ctx, 15m);
        // Running == Group in the current implementation — both reset together.
        ctx.EvaluateAggregate("RunningTotal", "[Fields.Total]", AggregateScope.Running)
            .Should().Be(25m);
    }

    [Fact]
    public void Unknown_aggregate_function_throws()
    {
        var ctx = NewCtx();
        PushRow(ctx, 10m);
        FluentActions.Invoking(() =>
            ctx.EvaluateAggregate("Median", "[Fields.Total]", AggregateScope.Report))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*Median*");
    }

    [Fact]
    public void Large_decimal_sum_preserves_precision()
    {
        var ctx = NewCtx();
        // Two values whose sum would lose precision in double but not in decimal.
        PushRow(ctx, 0.1m);
        PushRow(ctx, 0.2m);
        ctx.EvaluateAggregate("Sum", "[Fields.Total]", AggregateScope.Report)
            .Should().Be(0.3m);
    }
}
