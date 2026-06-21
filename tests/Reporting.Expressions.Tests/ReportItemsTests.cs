using FluentAssertions;
using Reporting.Expressions;
using Xunit;

namespace Reporting.Expressions.Tests;

/// <summary>The RDL <c>ReportItems!Name.Value</c> collection — reads the value another named text box
/// rendered to, exposed via the context's report-item registry.</summary>
public class ReportItemsTests
{
    [Fact]
    public void ReportItems_resolves_a_recorded_value()
    {
        var ev = new ExpressionEvaluator();
        var ctx = new ReportExpressionContext(ev);
        ctx.SetReportItem("Titulo", "Relatório de Vendas");

        ev.Evaluate("ReportItems.Titulo", ctx).Should().Be("Relatório de Vendas");
        ev.Evaluate("Concat('=> ', ReportItems.Titulo)", ctx).Should().Be("=> Relatório de Vendas");
    }

    [Fact]
    public void Unknown_or_unset_report_item_is_null()
    {
        var ev = new ExpressionEvaluator();
        var ctx = new ReportExpressionContext(ev);
        ev.Evaluate("ReportItems.Inexistente", ctx).Should().BeNull();
    }
}
