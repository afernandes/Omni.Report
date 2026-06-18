using FluentAssertions;
using Reporting.Aggregates;
using Reporting.Expressions;
using Xunit;

namespace Reporting.Expressions.Tests;

public class ExpressionEvaluatorTests
{
    private static (ReportExpressionContext ctx, ExpressionEvaluator ev) NewContext()
    {
        var ev = new ExpressionEvaluator();
        var ctx = new ReportExpressionContext(ev);
        return (ctx, ev);
    }

    [Fact]
    public void Evaluates_a_field_value()
    {
        var (ctx, ev) = NewContext();
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["Total"] = 100m });
        ev.Evaluate("Fields.Total", ctx).Should().Be(100m);
    }

    [Fact]
    public void Evaluates_arithmetic_on_fields()
    {
        var (ctx, ev) = NewContext();
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["Quantidade"] = 3m, ["Preco"] = 10m });
        ev.Evaluate("Fields.Quantidade * Fields.Preco", ctx).Should().Be(30m);
    }

    [Fact]
    public void Reads_parameter()
    {
        var (ctx, ev) = NewContext();
        ctx.ParametersStore.Set("Limite", 50m);
        ev.Evaluate("Parameters.Limite + 10", ctx).Should().Be(60m);
    }

    [Fact]
    public void Sum_aggregates_over_report_scope()
    {
        var (ctx, ev) = NewContext();
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["Total"] = 10m });
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["Total"] = 25m });
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["Total"] = 5m });
        ev.Evaluate("Sum(Fields.Total)", ctx).Should().Be(40m);
    }

    [Fact]
    public void Sum_with_group_scope_resets_on_group_reset()
    {
        var (ctx, ev) = NewContext();
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["Total"] = 10m });
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["Total"] = 20m });
        var firstGroupSum = ctx.EvaluateAggregate("Sum", "Fields.Total", AggregateScope.Group);
        firstGroupSum.Should().Be(30m);

        ctx.ResetGroup();
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["Total"] = 7m });
        var secondGroupSum = ctx.EvaluateAggregate("Sum", "Fields.Total", AggregateScope.Group);
        secondGroupSum.Should().Be(7m);
    }

    [Fact]
    public void Sum_via_dsl_call_with_group_scope_string()
    {
        var (ctx, ev) = NewContext();
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["Total"] = 5m });
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["Total"] = 15m });
        ev.Evaluate("Sum(Fields.Total, 'Group')", ctx).Should().Be(20m);
    }

    [Fact]
    public void Count_returns_row_count()
    {
        var (ctx, ev) = NewContext();
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["X"] = 1 });
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["X"] = 2 });
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["X"] = 3 });
        ev.Evaluate("Count(Fields.X)", ctx).Should().Be(3);
    }

    [Fact]
    public void Avg_handles_empty_dataset()
    {
        var (ctx, ev) = NewContext();
        ctx.EvaluateAggregate("Avg", "Fields.Total", AggregateScope.Report)
            .Should().Be(0m);
    }

    [Fact]
    public void Min_and_max_over_decimal_field()
    {
        var (ctx, ev) = NewContext();
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["V"] = 10m });
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["V"] = 3m });
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["V"] = 50m });
        ev.Evaluate("Min(Fields.V)", ctx).Should().Be(3m);
        ev.Evaluate("Max(Fields.V)", ctx).Should().Be(50m);
    }

    [Fact]
    public void Now_today_username_resolve_from_context()
    {
        var (ctx, ev) = NewContext();
        ctx.Now = new DateTime(2026, 5, 23, 14, 30, 0);
        ctx.UserName = "ana";
        ev.Evaluate("Today", ctx).Should().Be(new DateTime(2026, 5, 23));
        ev.Evaluate("UserName", ctx).Should().Be("ana");
    }

    [Fact]
    public void Page_number_and_total_pages()
    {
        var (ctx, ev) = NewContext();
        ctx.PageNumber = 3;
        ctx.TotalPages = 7;
        ev.Evaluate("Page.Number", ctx).Should().Be(3);
        ev.Evaluate("Page.Total", ctx).Should().Be(7);
    }

    [Fact]
    public void Coalesce_returns_first_non_null()
    {
        var (ctx, ev) = NewContext();
        ctx.SetCurrentRow(new Dictionary<string, object?>
        {
            ["A"] = null,
            ["B"] = 42,
        });
        ev.Evaluate("Coalesce(Fields.A, Fields.B, 0)", ctx).Should().Be(42);
    }

    [Fact]
    public void Parse_failure_yields_descriptive_exception()
    {
        var (ctx, ev) = NewContext();
        Action act = () => ev.Evaluate("Fields. * 2", ctx);
        act.Should().Throw<ExpressionParseException>();
    }

    [Fact]
    public void Evaluation_failure_yields_descriptive_exception()
    {
        var (ctx, ev) = NewContext();
        // Calling an unregistered function causes NCalc to throw, which we wrap.
        Action act = () => ev.Evaluate("UnknownFunc(1)", ctx);
        act.Should().Throw<ExpressionEvaluationException>()
            .WithMessage("*UnknownFunc*");
    }
}
