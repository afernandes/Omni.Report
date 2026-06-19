using FluentAssertions;
using Reporting.Elements;
using Xunit;

namespace Reporting.CodeFirst.Tests;

/// <summary>
/// Code-first surface for SSRS-style per-property expression bindings: <c>.Bind(path, expression)</c>
/// adds to the element's <see cref="ReportElement.PropertyExpressions"/> without disturbing the static
/// value — so low-level/code-first authoring keeps setting plain values and only opts into binding
/// where wanted.
/// </summary>
public class PropertyExpressionBindTests
{
    [Fact]
    public void Bind_adds_property_expressions_without_disturbing_the_static_value()
    {
        var report = ReportBuilder.Create("X")
            .Page(p => p.A4().Portrait())
            .Detail(d => d.Text("Fields.Nome").Bounds(0, 0, 60, 8)
                .Bind("Style.ForeColor", "Fields.Cor")
                .Bind("Visible", "Fields.Total > 0"))
            .Build();

        var tb = report.Definition.Detail.Elements.OfType<TextBoxElement>().Single();
        tb.Expression.Should().Be("Fields.Nome", "the static expression is untouched by Bind");
        tb.PropertyExpressions["Style.ForeColor"].Should().Be("Fields.Cor");
        tb.PropertyExpressions["Visible"].Should().Be("Fields.Total > 0");
    }
}
