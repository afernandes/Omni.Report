using FluentAssertions;
using Reporting.Expressions;
using Xunit;

namespace Reporting.Expressions.Tests;

/// <summary>The Concat (target of VB's &amp; operator) and Like (VB pattern match) runtime functions.</summary>
public class ConcatLikeTests
{
    private static ExpressionEvaluator Ev() => new();
    private static ReportExpressionContext Ctx(ExpressionEvaluator ev)
    {
        var c = new ReportExpressionContext(ev);
        c.SetCurrentRow(new Dictionary<string, object?> { ["Nome"] = "Ana", ["N"] = 3 });
        return c;
    }

    [Fact]
    public void Concat_joins_strings_and_stringifies_values()
    {
        var ev = Ev();
        var ctx = Ctx(ev);
        ev.Evaluate("Concat('a', 'b', 'c')", ctx).Should().Be("abc");
        ev.Evaluate("Concat('Olá, ', Fields.Nome)", ctx).Should().Be("Olá, Ana");
        ev.Evaluate("Concat('n=', Fields.N)", ctx).Should().Be("n=3");
    }

    [Theory]
    [InlineData("hello", "h*o", true)]
    [InlineData("hello", "h?llo", true)]
    [InlineData("hello", "x*", false)]
    [InlineData("A1", "A#", true)]
    [InlineData("AB", "A#", false)]
    [InlineData("ABC", "abc", true)] // case-insensitive
    public void Like_matches_vb_patterns(string value, string pattern, bool expected)
    {
        var ev = Ev();
        var ctx = Ctx(ev);
        ev.Evaluate($"Like('{value}', '{pattern}')", ctx).Should().Be(expected);
    }
}
