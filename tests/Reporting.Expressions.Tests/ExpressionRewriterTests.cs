using FluentAssertions;
using Reporting.Expressions;
using Xunit;

namespace Reporting.Expressions.Tests;

public class ExpressionRewriterTests
{
    [Theory]
    [InlineData("Fields.Total", "[Fields.Total]")]
    [InlineData("Fields.A + Fields.B", "[Fields.A] + [Fields.B]")]
    [InlineData("Parameters.From", "[Parameters.From]")]
    [InlineData("Variables.X * 2", "[Variables.X] * 2")]
    [InlineData("Page.Number", "[Page.Number]")]
    [InlineData("not_scoped", "not_scoped")]
    public void Rewrites_dotted_scope_names(string input, string expected)
        => ExpressionRewriter.Rewrite(input).Should().Be(expected);

    [Fact]
    public void Does_not_rewrite_inside_string_literals()
    {
        ExpressionRewriter.Rewrite("'Fields.Total is bound' + Fields.Total")
            .Should().Be("'Fields.Total is bound' + [Fields.Total]");
    }

    [Fact]
    public void Does_not_re_wrap_already_bracketed_names()
    {
        ExpressionRewriter.Rewrite("[Fields.Total] + Fields.X")
            .Should().Be("[Fields.Total] + [Fields.X]");
    }

    [Theory]
    [InlineData("Fields.Total", true, "Fields", "Total")]
    [InlineData("Parameters.A", true, "Parameters", "A")]
    [InlineData("NoScope.X", false, "", "")]
    [InlineData("Plain", false, "", "")]
    public void Try_split_scope(string name, bool expected, string scope, string member)
    {
        ExpressionRewriter.TrySplitScope(name, out var s, out var m).Should().Be(expected);
        s.Should().Be(scope);
        m.Should().Be(member);
    }
}
