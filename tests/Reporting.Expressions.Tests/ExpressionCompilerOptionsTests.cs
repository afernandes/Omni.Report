using System.Globalization;
using FluentAssertions;
using Reporting.Expressions;
using Xunit;

namespace Reporting.Expressions.Tests;

/// <summary>
/// Pins the two evaluation options the whole engine depends on: decimal arithmetic and
/// case-insensitive built-in functions.
/// </summary>
/// <remarks>
/// <para>These were written during the NCalc 6 → 7 migration, and the reason is the point: NCalc 7
/// restructured options into <c>ExpressionConfiguration</c>, splitting parse-time from evaluation-time
/// settings and moving <c>DecimalAsDefault</c> into <c>MathOptions.FloatingPointNumberType</c>. The
/// translation from our <c>ExpressionOptions</c> flags happens inside an implicit conversion — library
/// code, not ours.</para>
///
/// <para>The 165 expression tests all passed on the migration, and <b>none of them would have caught a
/// silent switch to <c>double</c></b>: they compare values, and at test magnitudes a double compares
/// equal to the decimal. The engine would have gone on producing <c>1450.8999999999999</c> in currency
/// columns of real reports with a green suite.</para>
/// </remarks>
public class ExpressionCompilerOptionsTests
{
    private static (ReportExpressionContext ctx, ExpressionEvaluator ev) NewContext()
    {
        var ev = new ExpressionEvaluator();
        return (new ReportExpressionContext(ev), ev);
    }

    /// <summary>
    /// <c>DecimalAsDefault</c>: a floating literal must evaluate to <see cref="decimal"/>.
    /// </summary>
    [Fact]
    public void Floating_literals_evaluate_as_decimal_not_double()
    {
        var (ctx, ev) = NewContext();

        ev.Evaluate("2.5", ctx).Should().BeOfType<decimal>(
            "money math in a report must not go through binary floating point");
    }

    /// <summary>
    /// The same guarantee, stated so it cannot pass by coincidence: <c>0.1 + 0.2</c> is exactly
    /// <c>0.3</c> in decimal and <c>0.30000000000000004</c> in double.
    /// </summary>
    [Fact]
    public void Decimal_arithmetic_is_exact()
    {
        var (ctx, ev) = NewContext();

        var result = ev.Evaluate("0.1 + 0.2", ctx);

        result.Should().BeOfType<decimal>();
        ((decimal)result!).Should().Be(0.3m);

        // A type assertion alone would not be enough: the point is the arithmetic, and this is the
        // canonical case where binary floating point visibly fails. Formatted with the invariant
        // culture on purpose — a bare ToString() picks up the machine's separator and this assertion
        // would read "0,3" on a pt-BR box, failing for a reason that has nothing to do with NCalc.
        ((decimal)result).ToString(CultureInfo.InvariantCulture).Should().Be("0.3");
    }

    /// <summary>
    /// <c>IgnoreCaseAtBuiltInFunctions</c>: report authors write <c>Sum</c>, SSRS writes <c>SUM</c>,
    /// and NCalc's own name is <c>Abs</c>. All three spellings must resolve.
    /// </summary>
    [Theory]
    [InlineData("Abs(-5)")]
    [InlineData("ABS(-5)")]
    [InlineData("abs(-5)")]
    public void Built_in_functions_resolve_regardless_of_case(string expression)
    {
        var (ctx, ev) = NewContext();

        ev.Evaluate(expression, ctx).Should().Be(5m);
    }

    /// <summary>
    /// <c>StringConcat</c> must stay OFF: with it, every <c>+</c> routes through string concatenation
    /// and <c>1 + 2</c> silently becomes <c>"12"</c>. The option is deliberately absent from
    /// <c>DefaultOptions</c>, and a restructuring that turned it on by default would be invisible
    /// without this.
    /// </summary>
    [Fact]
    public void Plus_stays_arithmetic_and_does_not_concatenate()
    {
        var (ctx, ev) = NewContext();

        ev.Evaluate("1 + 2", ctx).Should().Be(3m);
    }
}
