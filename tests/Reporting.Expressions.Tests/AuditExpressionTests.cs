using System.Collections;
using System.Reflection;
using FluentAssertions;
using Reporting.Aggregates;
using Xunit;
namespace Reporting.Expressions.Tests;

public sealed class AuditExpressionTests
{
    [Fact]
    public void Evaluate_SomaDeExpressaoComposta_PreservaArvoreEAcumulaNovasLinhas()
    {
        var evaluator = new ExpressionEvaluator();
        var context = new ReportExpressionContext(evaluator);
        for (int row = 1; row <= 100; row++)
        {
            context.SetCurrentRow(new Dictionary<string, object?> { ["Preco"] = 1.25m, ["Quantidade"] = 2 });
            evaluator.Evaluate("Sum(Fields.Preco * Fields.Quantidade)", context).Should().Be(row * 2.5m);
        }
    }
    [Fact]
    public void EvaluateAggregate_ParametroAlterado_NaoReutilizaAcumuladorDeExpressaoDinamica()
    {
        var context = new ReportExpressionContext();
        context.SetCurrentRow(new Dictionary<string, object?> { ["Total"] = 2m });
        context.ParametersStore.Set("Taxa", 1m);
        context.EvaluateAggregate("Sum", "Fields.Total * Parameters.Taxa", AggregateScope.Report).Should().Be(2m);
        context.ParametersStore.Set("Taxa", 3m);
        context.EvaluateAggregate("Sum", "Fields.Total * Parameters.Taxa", AggregateScope.Report).Should().Be(6m);
    }
    [Fact]
    public void Compile_MuitasExpressoesDistintas_LimitaCacheERecompilaEvictadas()
    {
        var compiler = new ExpressionCompiler();
        for (int i = 0; i < 2000; i++) compiler.Compile($"{i} + 1").Evaluate().Should().Be(i + 1);
        var cache = typeof(ExpressionCompiler).GetField("_cache", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(compiler)!;
        var items = (IDictionary)cache.GetType().GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(cache)!;
        items.Count.Should().BeLessThanOrEqualTo(1024);
        compiler.Compile("0 + 1").Evaluate().Should().Be(1);
    }
    [Fact]
    public void EvaluateAggregate_ReiniciaPaginaComMesmoNumeroDeLinhas_NaoReutilizaTotalAnterior()
    {
        var context = new ReportExpressionContext();
        context.SetCurrentRow(new Dictionary<string, object?> { ["Total"] = 1.25m });
        context.EvaluateAggregate("Sum", "Fields.Total", AggregateScope.Page).Should().Be(1.25m);
        context.ResetPage();
        context.SetCurrentRow(new Dictionary<string, object?> { ["Total"] = 7.50m });
        context.EvaluateAggregate("Sum", "Fields.Total", AggregateScope.Page).Should().Be(7.50m);
    }
    [Fact]
    public void EvaluateAggregate_MilTotaisParciais_CalculaValoresSemContaminarCampoAtual()
    {
        var context = new ReportExpressionContext();
        for (int i = 1; i <= 1000; i++)
        {
            context.SetCurrentRow(new Dictionary<string, object?> { ["Total"] = 0.01m });
            context.EvaluateAggregate("Sum", "Fields.Total", AggregateScope.Report).Should().Be(i * 0.01m);
            context.Fields["Total"].Should().Be(0.01m);
        }
    }
    [Fact]
    public void Evaluate_ResolverCancelaExecucao_PropagaOperationCanceledException()
    {
        using var cancellation = new CancellationTokenSource();
        var evaluator = new ExpressionEvaluator { CodeFunctionResolver = (_, _) => { cancellation.Cancel(); return 1; } };
        var context = new ReportExpressionContext(evaluator) { CancellationToken = cancellation.Token };
        var action = () => evaluator.Evaluate("Code.Cancelar()", context);
        action.Should().Throw<OperationCanceledException>();
    }
}
