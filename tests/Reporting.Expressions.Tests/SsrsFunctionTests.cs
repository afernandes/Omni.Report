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

    private static (ReportExpressionContext ctx, ExpressionEvaluator ev) NewContext(string culture)
    {
        var ev = new ExpressionEvaluator();
        var ctx = new ReportExpressionContext(ev, System.Globalization.CultureInfo.GetCultureInfo(culture));
        return (ctx, ev);
    }

    [Fact]
    public void Vb_format_functions_use_the_context_culture()
    {
        var (us, evUs) = NewContext("en-US");
        evUs.Evaluate("FormatCurrency(1234.5)", us).Should().Be("$1,234.50");
        evUs.Evaluate("FormatCurrency(1234.56, 0)", us).Should().Be("$1,235"); // no midpoint ambiguity
        evUs.Evaluate("FormatNumber(1234.5, 2)", us).Should().Be("1,234.50");
        evUs.Evaluate("FormatPercent(0.25, 1)", us).Should().Be("25.0%");
        // Non-numeric value: ValueFormatter degrades gracefully to the string itself (never crashes).
        evUs.Evaluate("FormatCurrency('abc')", us).Should().Be("abc");

        var (br, evBr) = NewContext("pt-BR");
        evBr.Evaluate("FormatNumber(1234.5, 2)", br).Should().Be("1.234,50");
    }

    [Fact]
    public void Vb_FormatDateTime_maps_the_named_formats()
    {
        var (us, ev) = NewContext("en-US");
        // CDate parses ISO via InvariantCulture; assert the named-format branches don't throw and differ.
        ev.Evaluate("FormatDateTime(CDate('2026-06-21'), 2)", us).Should().Be("6/21/2026"); // vbShortDate
        ev.Evaluate("FormatDateTime(CDate('2026-06-21 14:05:00'), 4)", us).Should().Be("2:05 PM"); // vbShortTime
        ev.Evaluate("FormatDateTime('nao-data')", us).Should().BeNull(); // #Error → null
    }

    [Theory]
    [InlineData("DatePart('yyyy', CDate('2026-06-21'))", 2026)]
    [InlineData("DatePart('m', CDate('2026-06-21'))", 6)]
    [InlineData("DatePart('d', CDate('2026-06-21'))", 21)]
    [InlineData("DatePart('q', CDate('2026-06-21'))", 2)]   // quarter
    [InlineData("DatePart('y', CDate('2026-01-10'))", 10)]  // day of year
    [InlineData("Sign(-5)", -1)]
    [InlineData("Sign(0)", 0)]
    public void Vb_int_returning_functions(string expr, int expected)
    {
        var (ctx, ev) = NewContext();
        System.Convert.ToInt32(ev.Evaluate(expr, ctx)).Should().Be(expected);
    }

    [Fact]
    public void Vb_Fix_and_Int_differ_on_negatives()
    {
        var (ctx, ev) = NewContext();
        System.Convert.ToDouble(ev.Evaluate("Fix(-2.7)", ctx)).Should().Be(-2d); // truncate toward zero
        System.Convert.ToDouble(ev.Evaluate("Int(-2.7)", ctx)).Should().Be(-3d); // floor toward -inf
        System.Convert.ToDouble(ev.Evaluate("Fix(2.7)", ctx)).Should().Be(2d);
    }

    [Fact]
    public void Vb_MonthName_uses_culture()
    {
        var (us, evUs) = NewContext("en-US");
        evUs.Evaluate("MonthName(6)", us).Should().Be("June");
        evUs.Evaluate("MonthName(6, true)", us).Should().Be("Jun");
        var (br, evBr) = NewContext("pt-BR");
        ((string)evBr.Evaluate("MonthName(6)", br)!).ToLowerInvariant().Should().Be("junho");
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
