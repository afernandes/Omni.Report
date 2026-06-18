using System.Globalization;
using FluentAssertions;
using Reporting.Expressions;
using Xunit;

namespace Reporting.Expressions.Tests;

public class TemplateRendererTests
{
    private static (ReportExpressionContext ctx, TemplateRenderer tpl) NewRenderer()
    {
        var ctx = new ReportExpressionContext(culture: CultureInfo.GetCultureInfo("pt-BR"));
        var tpl = new TemplateRenderer();
        return (ctx, tpl);
    }

    [Fact]
    public void Plain_string_passes_through()
    {
        var (ctx, tpl) = NewRenderer();
        tpl.Render("Olá mundo", ctx).Should().Be("Olá mundo");
    }

    [Fact]
    public void Simple_field_interpolation()
    {
        var (ctx, tpl) = NewRenderer();
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["Cliente"] = "Ana Beatriz" });
        tpl.Render("Cliente: {Fields.Cliente}", ctx).Should().Be("Cliente: Ana Beatriz");
    }

    [Fact]
    public void Currency_format_uses_culture()
    {
        var (ctx, tpl) = NewRenderer();
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["Total"] = 1234.56m });
        var rendered = tpl.Render("Total: {Fields.Total:C}", ctx);
        // pt-BR currency: R$ 1.234,56 (NBSP may separate)
        rendered.Should().StartWith("Total: ");
        rendered.Should().Contain("1.234,56");
    }

    [Fact]
    public void Date_format()
    {
        var (ctx, tpl) = NewRenderer();
        ctx.ParametersStore.Set("DataInicio", new DateTime(2026, 5, 23));
        tpl.Render("Em {Parameters.DataInicio:dd/MM/yyyy}", ctx)
            .Should().Be("Em 23/05/2026");
    }

    [Fact]
    public void Escaped_braces_render_as_literal()
    {
        var (ctx, tpl) = NewRenderer();
        tpl.Render("{{not a field}}", ctx).Should().Be("{not a field}");
    }

    [Fact]
    public void Unterminated_template_throws()
    {
        var (ctx, tpl) = NewRenderer();
        Action act = () => tpl.Render("Open {Fields.X", ctx);
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Unexpected_close_brace_throws()
    {
        var (ctx, tpl) = NewRenderer();
        Action act = () => tpl.Render("Bad } close", ctx);
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Has_placeholders_detection()
    {
        TemplateRenderer.HasPlaceholders("no braces").Should().BeFalse();
        TemplateRenderer.HasPlaceholders("a {x} b").Should().BeTrue();
        TemplateRenderer.HasPlaceholders("escaped {{").Should().BeFalse();
    }

    [Fact]
    public void Sum_inside_template()
    {
        var (ctx, tpl) = NewRenderer();
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["V"] = 10m });
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["V"] = 25m });
        tpl.Render("Subtotal: {Sum(Fields.V):0.00}", ctx)
            .Should().StartWith("Subtotal: 35");
    }

    // ─── Edge-case coverage added during the coverage audit ──────────────────────

    [Theory]
    [InlineData("pt-BR", 1234.56, "1.234,56")]
    [InlineData("en-US", 1234.56, "1,234.56")]
    [InlineData("de-DE", 1234.56, "1.234,56")]
    [InlineData("ja-JP", 1234.56, "1,234.56")]
    public void Number_format_follows_context_culture(string cultureName, double value, string expected)
    {
        var ctx = new ReportExpressionContext(culture: CultureInfo.GetCultureInfo(cultureName));
        var tpl = new TemplateRenderer();
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["V"] = (decimal)value });
        tpl.Render("{Fields.V:N2}", ctx).Should().Be(expected);
    }

    [Fact]
    public void Date_format_dd_MM_yyyy_in_ptBR()
    {
        var (ctx, tpl) = NewRenderer();
        ctx.SetCurrentRow(new Dictionary<string, object?>
        {
            ["Data"] = new DateTime(2026, 5, 24),
        });
        tpl.Render("Emitido em {Fields.Data:dd/MM/yyyy}", ctx)
            .Should().Be("Emitido em 24/05/2026");
    }

    [Fact]
    public void Literal_text_outside_braces_preserves_special_chars()
    {
        var (ctx, tpl) = NewRenderer();
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["Total"] = 10m });
        // Templates frequently contain XML-special characters in plain text (< > & "). Make
        // sure none of them are mangled — designers and serializers later escape on write.
        tpl.Render("Total <bold> & \"strong\" > {Fields.Total:N0}", ctx)
            .Should().Be("Total <bold> & \"strong\" > 10");
    }

    [Fact]
    public void Escaped_braces_emit_literal_braces()
    {
        var (ctx, tpl) = NewRenderer();
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["X"] = 1 });
        tpl.Render("{{Fields.X}} = {Fields.X}", ctx)
            .Should().Be("{Fields.X} = 1");
    }

    [Fact]
    public void Unterminated_placeholder_throws_format_exception()
    {
        var (ctx, tpl) = NewRenderer();
        FluentActions.Invoking(() => tpl.Render("Total: {Fields.Total", ctx))
            .Should().Throw<FormatException>();
    }

    [Fact]
    public void Stray_close_brace_throws_format_exception()
    {
        var (ctx, tpl) = NewRenderer();
        FluentActions.Invoking(() => tpl.Render("Total } end", ctx))
            .Should().Throw<FormatException>();
    }

    [Fact]
    public void Empty_template_returns_empty_string()
    {
        var (ctx, tpl) = NewRenderer();
        tpl.Render(string.Empty, ctx).Should().BeEmpty();
    }

    [Fact]
    public void Format_colon_inside_function_args_is_not_treated_as_format_separator()
    {
        var (ctx, tpl) = NewRenderer();
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["X"] = 10m });
        // Sum(Fields.X) inside a template — the parser must find the OUTER ':' for the format,
        // ignoring colons inside parens.
        tpl.Render("Resultado: {Sum(Fields.X):N2}", ctx)
            .Should().Be("Resultado: 10,00");
    }

    [Fact]
    public void Null_field_value_renders_as_empty_string()
    {
        var (ctx, tpl) = NewRenderer();
        ctx.SetCurrentRow(new Dictionary<string, object?> { ["X"] = null });
        tpl.Render("Vazio:[{Fields.X}]", ctx).Should().Be("Vazio:[]");
    }

    [Fact]
    public void HasPlaceholders_distinguishes_plain_text_from_template()
    {
        TemplateRenderer.HasPlaceholders("plain text").Should().BeFalse();
        TemplateRenderer.HasPlaceholders("with {Fields.X}").Should().BeTrue();
        // Note: the naive cheap-check returns true for unbalanced escapes like "{{ }}" —
        // the full template parser then escapes them correctly at Render time. This is
        // intentional: HasPlaceholders is a fast-path filter, not a strict parser.
    }
}
