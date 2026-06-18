using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Expressions;
using Reporting.Expressions.Roslyn;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Xunit;

namespace Reporting.Expressions.Roslyn.Tests;

public sealed record Venda(decimal total);

/// <summary>
/// Tests the opt-in Roslyn code feature: compiling a C# block, routing <c>Code.Method(...)</c>
/// through the expression evaluator, and running it inside a full report pagination.
/// </summary>
public class RoslynCodeTests
{
    [Fact]
    public void Compiles_and_invokes_a_helper_method()
    {
        var evaluator = new RoslynCodeEvaluator("public double Dobro(double x) => x * 2;");

        var result = evaluator.Invoke("Dobro", [21.0]);

        Convert.ToDouble(result).Should().Be(42d);
    }

    [Fact]
    public void Unknown_method_returns_null()
    {
        var evaluator = new RoslynCodeEvaluator("public int Um() => 1;");
        evaluator.Invoke("NaoExiste", []).Should().BeNull();
    }

    [Fact]
    public void Invalid_code_throws_a_compilation_exception()
    {
        var act = () => new RoslynCodeEvaluator("this is not valid C#");
        act.Should().Throw<RoslynCodeCompilationException>();
    }

    [Fact]
    public void Code_call_routes_through_the_expression_evaluator()
    {
        var evaluator = new ExpressionEvaluator
        {
            CodeFunctionResolver = RoslynCode.CreateResolver("public decimal Imposto(decimal t) => t * 0.18m;"),
        };
        var ctx = new ReportExpressionContext(evaluator);

        var result = evaluator.Evaluate("Code.Imposto(100)", ctx);

        Convert.ToDecimal(result).Should().Be(18m);
    }

    [Fact]
    public async Task Report_renders_using_a_code_function()
    {
        var report = ReportBuilder.Create("Tributos")
            .Page(p => p.A4().Portrait().Margins(15))
            .DataSource("Vendas", new[] { new Venda(100m) })
            .Detail(d => d.Height(6)
                .Text("{Code.Imposto(Fields.total):C}").At(0, 0).Size(60, 6))
            .Build();

        var paginator = new ReportPaginator();
        var request = new PaginationRequest
        {
            Definition = report.Definition,
            DataSources = report.DataSources,
            CodeFunctionResolver = RoslynCode.CreateResolver(
                "public decimal Imposto(decimal t) => Math.Round(t * 0.18m, 2);"),
        };

        var rendered = await paginator.PaginateAsync(request);
        var texts = rendered.Pages.SelectMany(p => p.Primitives)
            .OfType<DrawTextPrimitive>().Select(t => t.Text).ToList();

        texts.Should().Contain(t => t.Contains("18"), "Code.Imposto(100) = 18,00 formatted as currency");
    }
}
