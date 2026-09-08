using FluentAssertions;
using Reporting.Aggregates;
using Xunit;

namespace Reporting.Expressions.Tests;

public sealed class NestedGroupScopeTests
{
    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en-US")]
    [InlineData("")]
    public void Evaluate_GruposAninhados_PreservaTotaisEPosicoesDosAncestrais(string cultura)
    {
        var evaluator = new ExpressionEvaluator();
        var context = new ReportExpressionContext(evaluator, System.Globalization.CultureInfo.GetCultureInfo(cultura));
        context.BeginGroup("Cliente", "Ana");
        foreach (var valor in new[] { 1.25m, 2.50m, 3.75m })
        {
            context.BeginGroup("Produto", valor);
            context.SetCurrentRow(new Dictionary<string, object?> { ["Total"] = valor });
            evaluator.Evaluate("Sum(Fields.Total, 'Group')", context).Should().Be(valor);
            context.EndGroup();
        }

        evaluator.Evaluate("Sum(Fields.Total, 'Group')", context).Should().Be(7.50m);
        evaluator.Evaluate("RunningValue(Fields.Total, Sum, 'Cliente')", context).Should().Be(7.50m);
        evaluator.Evaluate("CountRows('Cliente')", context).Should().Be(3);
        evaluator.Evaluate("RowNumber('Cliente')", context).Should().Be(3);
        evaluator.Evaluate("Previous(Fields.Total, 'Cliente')", context).Should().Be(2.50m);
        context.GroupKey.Should().Be("Ana");
        context.EndGroup();
        context.GroupKey.Should().BeNull();
        context.EvaluateAggregate("Sum", "Fields.Total", AggregateScope.Group).Should().Be(0m);
        context.EvaluateAggregate("Sum", "Fields.Total", AggregateScope.Report).Should().Be(7.50m);
    }

    [Fact]
    public void UseGroup_SelecaoAninhada_RestauraChaveEEscopoMesmoComFalha()
    {
        var context = new ReportExpressionContext();
        context.BeginGroup("Externo", "A");
        context.BeginGroup("Interno", "B");
        context.SetCurrentRow(new Dictionary<string, object?> { ["Total"] = 1.25m });
        Action act = () =>
        {
            using var selection = context.UseGroup("Externo");
            context.GroupKey.Should().Be("A");
            throw new InvalidOperationException("Falha de renderização");
        };
        act.Should().Throw<InvalidOperationException>();
        context.GroupKey.Should().Be("B");
        context.BeginGroup("Vazio", "C");
        context.EvaluateAggregate("Sum", "Fields.Total", AggregateScope.Group).Should().Be(0m);
        context.EvaluateGroupAggregate("Sum", "Fields.Total", "Externo").Should().Be(1.25m);
        context.GroupKey.Should().Be("C");
        context.EndGroup();
        context.ResetAll();
        Action unknown = () => context.EvaluateGroupAggregate("Sum", "Fields.Total", "Externo");
        unknown.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Evaluate_GrupoNomeadoInexistente_RejeitaSemUsarTotalDoRelatorio()
    {
        var evaluator = new ExpressionEvaluator();
        var context = new ReportExpressionContext(evaluator);
        context.SetCurrentRow(new Dictionary<string, object?> { ["Total"] = 100m });
        Action act = () => evaluator.Evaluate("Sum(Fields.Total, 'Inexistente')", context);
        act.Should().Throw<ExpressionEvaluationException>().WithInnerException<ArgumentException>();
    }
}
