using FluentAssertions;
using Reporting;
using Reporting.Bands;
using Reporting.Common;
using Reporting.DataSources;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Reporting.Paper;
using Reporting.Parameters;
using Xunit;

namespace Reporting.Layout.Tests;

/// <summary>
/// A parameter's <see cref="ReportParameter.DefaultValueExpression"/> (SSRS <c>=Today()</c>, <c>=DateAdd(...)</c>)
/// is evaluated at run start to seed the value when no prompted/host value and no literal default are supplied.
/// </summary>
public class ParameterDefaultExpressionTests
{
    private static ReportDefinition WithHeaderShowing(string paramExpression, ReportParameter parameter)
        => new("R", PageSetup.A4Portrait, DetailBand.Empty)
        {
            Parameters = new EquatableArray<ReportParameter>(new[] { parameter }),
            ReportHeader = new ReportBand(BandKind.ReportHeader, Unit.FromMm(10),
                new EquatableArray<ReportElement>(new ReportElement[]
                {
                    new TextBoxElement { Expression = paramExpression, Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(80), Unit.FromMm(8)) },
                })),
        };

    [Fact]
    public async Task Expression_default_seeds_the_parameter_at_run_start()
    {
        var def = WithHeaderShowing("Parameters.Hoje",
            new ReportParameter("Hoje", typeof(DateTime)) { DefaultValueExpression = "Today()" });

        var report = await new ReportPaginator().PaginateAsync(
            new PaginationRequest { Definition = def, DataSources = new DataSourceRegistry() });

        var text = report.Pages.SelectMany(p => p.Primitives).OfType<DrawTextPrimitive>().First().Text;
        text.Should().Contain(DateTime.Today.Year.ToString()); // =Today() was evaluated and seeded
    }

    [Fact]
    public async Task A_prompted_value_overrides_the_expression_default()
    {
        var def = WithHeaderShowing("Parameters.N",
            new ReportParameter("N", typeof(int)) { DefaultValueExpression = "1 + 1" });

        var report = await new ReportPaginator().PaginateAsync(new PaginationRequest
        {
            Definition = def,
            DataSources = new DataSourceRegistry(),
            Parameters = new Dictionary<string, object?> { ["N"] = 99 }, // explicit value wins
        });

        report.Pages.SelectMany(p => p.Primitives).OfType<DrawTextPrimitive>().First().Text.Should().Be("99");
    }

    [Fact]
    public async Task A_literal_default_takes_precedence_over_an_expression_default()
    {
        // Both set (degenerate) → the literal wins; the expression is the fallback for when there is no literal.
        var def = WithHeaderShowing("Parameters.N",
            new ReportParameter("N", typeof(int), DefaultValue: 7) { DefaultValueExpression = "1 + 1" });

        var report = await new ReportPaginator().PaginateAsync(
            new PaginationRequest { Definition = def, DataSources = new DataSourceRegistry() });

        report.Pages.SelectMany(p => p.Primitives).OfType<DrawTextPrimitive>().First().Text.Should().Be("7");
    }
}
