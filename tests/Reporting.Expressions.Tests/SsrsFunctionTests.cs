using FluentAssertions;
using Reporting.Expressions;
using Xunit;

namespace Reporting.Expressions.Tests;

/// <summary>
/// SSRS/VB-style scalar functions (conditional / text / date parts) added on top of NCalc's native
/// math. Case-insensitive, like SSRS.
/// </summary>
public class SsrsFunctionTests
{
    private static (ReportExpressionContext ctx, ExpressionEvaluator ev) NewContext()
    {
        var ev = new ExpressionEvaluator();
        var ctx = new ReportExpressionContext(ev);
        return (ctx, ev);
    }

    [Theory]
    [InlineData("IIf(1 < 2, 'sim', 'nao')", "sim")]
    [InlineData("IIf(2 < 1, 'sim', 'nao')", "nao")]
    [InlineData("Switch(false, 'a', true, 'b')", "b")]
    [InlineData("Choose(2, 'a', 'b', 'c')", "b")]
    [InlineData("Left('Relatorio', 4)", "Rela")]
    [InlineData("Right('Relatorio', 3)", "rio")]
    [InlineData("Mid('Relatorio', 4, 3)", "ato")] // 1-based start
    [InlineData("Mid('Relatorio', 7)", "rio")]    // no length → to the end
    [InlineData("UCase('abc')", "ABC")]
    [InlineData("LCase('ABC')", "abc")]
    [InlineData("Trim('  x  ')", "x")]
    [InlineData("Replace('a-b-c', '-', '/')", "a/b/c")]
    [InlineData("left('abc', 2)", "ab")] // case-insensitive
    public void Evaluates_string_returning_functions(string expr, string expected)
    {
        var (ctx, ev) = NewContext();
        ev.Evaluate(expr, ctx).Should().Be(expected);
    }

    [Theory]
    [InlineData("Len('abcd')", 4)]
    [InlineData("Left('ab', 10)", 2)] // clamps, no overflow
    public void Len_and_clamping(string expr, int expectedLen)
    {
        var (ctx, ev) = NewContext();
        var r = ev.Evaluate(expr, ctx);
        (r is int i ? i : ((string)r!).Length).Should().Be(expectedLen);
    }

    [Fact]
    public void Extracts_date_parts()
    {
        var (ctx, ev) = NewContext();
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["D"] = new DateTime(2026, 6, 20) });
        ev.Evaluate("Year(Fields.D)", ctx).Should().Be(2026);
        ev.Evaluate("Month(Fields.D)", ctx).Should().Be(6);
        ev.Evaluate("Day(Fields.D)", ctx).Should().Be(20);
    }

    [Fact]
    public void IsNothing_detects_null()
    {
        var (ctx, ev) = NewContext();
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["X"] = null });
        ev.Evaluate("IsNothing(Fields.X)", ctx).Should().Be(true);
    }

    [Fact]
    public void IIf_evaluates_only_the_taken_branch()
    {
        var (ctx, ev) = NewContext();
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["X"] = 0m });
        // The else branch (100 / X) would divide by zero if eagerly evaluated — IIf must skip it.
        ev.Evaluate("IIf(Fields.X = 0, 99, 100 / Fields.X)", ctx).Should().Be(99);
    }

    [Fact]
    public void Switch_and_Choose_skip_non_selected_branches()
    {
        var (ctx, ev) = NewContext();
        // The non-selected value (100 / 0) must never be evaluated.
        ev.Evaluate("Switch(true, 'a', true, 100 / 0)", ctx).Should().Be("a");
        ev.Evaluate("Choose(1, 'a', 100 / 0)", ctx).Should().Be("a");
    }

    [Fact]
    public void Uncoercible_argument_degrades_to_null_instead_of_crashing()
    {
        var (ctx, ev) = NewContext();
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["T"] = "not a date" });
        // Year of a non-date text → null (SSRS-style #Error), not an exception that kills the cell.
        ev.Evaluate("Year(Fields.T)", ctx).Should().BeNull();
    }
}
