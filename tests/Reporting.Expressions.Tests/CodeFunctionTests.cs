using FluentAssertions;
using Reporting.Expressions;
using Xunit;

namespace Reporting.Expressions.Tests;

/// <summary>
/// Proves the <c>Code.MethodName(...)</c> extension point in <see cref="ExpressionEvaluator"/>:
/// NCalc surfaces the dotted call as a function named <c>"Code.MethodName"</c>, which is routed
/// to the opt-in <see cref="ExpressionEvaluator.CodeFunctionResolver"/>. The Roslyn package plugs
/// a real C# compiler in here; the core stays free of any code-execution dependency.
/// </summary>
public class CodeFunctionTests
{
    [Fact]
    public void Code_call_routes_to_the_registered_resolver()
    {
        var evaluator = new ExpressionEvaluator
        {
            CodeFunctionResolver = (method, args) =>
                method == "Dobro" ? Convert.ToDouble(args[0]) * 2 : null,
        };
        var ctx = new ReportExpressionContext(evaluator);

        var result = evaluator.Evaluate("Code.Dobro(21)", ctx);

        Convert.ToDouble(result).Should().Be(42d);
    }

    [Fact]
    public void Code_resolver_receives_method_name_without_prefix_and_all_args()
    {
        string? seenMethod = null;
        object?[]? seenArgs = null;
        var evaluator = new ExpressionEvaluator
        {
            CodeFunctionResolver = (method, args) =>
            {
                seenMethod = method;
                seenArgs = args;
                return 0;
            },
        };
        var ctx = new ReportExpressionContext(evaluator);

        evaluator.Evaluate("Code.Soma(2, 3)", ctx);

        seenMethod.Should().Be("Soma");
        seenArgs.Should().HaveCount(2);
        Convert.ToInt32(seenArgs![0]).Should().Be(2);
        Convert.ToInt32(seenArgs![1]).Should().Be(3);
    }

    [Fact]
    public void Code_call_is_inert_when_no_resolver_is_registered()
    {
        var evaluator = new ExpressionEvaluator();
        var ctx = new ReportExpressionContext(evaluator);

        // No resolver → the core must not execute anything. Either an unresolved-function error
        // or a null result is acceptable; what matters is no C# runs and nothing crashes the host.
        var act = () => evaluator.Evaluate("Code.Qualquer(1)", ctx);
        act.Should().NotThrow<NotImplementedException>();
    }
}
