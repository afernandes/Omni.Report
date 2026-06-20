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

    [Fact]
    public void DateAdd_and_DateDiff_with_vb_intervals()
    {
        var (ctx, ev) = NewContext();
        ctx.SetCurrentRow(new Dictionary<string, object?>
        {
            ["A"] = new DateTime(2026, 1, 10),
            ["B"] = new DateTime(2026, 3, 15),
        });
        ev.Evaluate("DateAdd('d', 5, Fields.A)", ctx).Should().Be(new DateTime(2026, 1, 15));
        ev.Evaluate("DateAdd('m', 2, Fields.A)", ctx).Should().Be(new DateTime(2026, 3, 10));
        ev.Evaluate("DateAdd('yyyy', 1, Fields.A)", ctx).Should().Be(new DateTime(2027, 1, 10));
        ev.Evaluate("DateDiff('d', Fields.A, Fields.B)", ctx).Should().Be(64);
        ev.Evaluate("DateDiff('m', Fields.A, Fields.B)", ctx).Should().Be(2);
    }

    [Theory]
    [InlineData("InStr('abcdef', 'cd')", 3)]
    [InlineData("InStr('abc', 'z')", 0)] // not found → 0
    [InlineData("InStr('abcabc', 'bc')", 2)]
    public void InStr_is_1_based(string expr, int expected)
    {
        var (ctx, ev) = NewContext();
        ev.Evaluate(expr, ctx).Should().Be(expected);
    }

    [Fact]
    public void Conversions_and_more_date_parts()
    {
        var (ctx, ev) = NewContext();
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["D"] = new DateTime(2026, 6, 20, 14, 30, 45) });
        ev.Evaluate("CStr(123)", ctx).Should().Be("123");
        ev.Evaluate("CInt('5')", ctx).Should().Be(5);
        ev.Evaluate("CInt('2.5')", ctx).Should().Be(2);        // parses + rounds, never crashes to null
        ev.Evaluate("CDbl('1.5')", ctx).Should().Be(1.5);      // invariant parse — '1.5' is 1.5, not 15 under pt-BR
        ev.Evaluate("CBool('true')", ctx).Should().Be(true);
        ev.Evaluate("Hour(Fields.D)", ctx).Should().Be(14);
        ev.Evaluate("Minute(Fields.D)", ctx).Should().Be(30);
        ev.Evaluate("Second(Fields.D)", ctx).Should().Be(45);
    }
}
