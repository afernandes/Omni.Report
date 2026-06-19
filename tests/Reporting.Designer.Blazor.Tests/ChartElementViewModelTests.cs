using FluentAssertions;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Elements;
using Reporting.Geometry;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// Covers the first slice of designer support for the Chart element: the <see cref="ElementViewModel"/>
/// round-trips a <see cref="ChartElement"/> (kind, title, legend, series) to and from the domain
/// model, and clones it deeply. The toolbox can now add a Chart, and the canvas shows a placeholder;
/// preview/export render the real chart.
/// </summary>
public class ChartElementViewModelTests
{
    [Fact]
    public void Chart_view_model_round_trips_through_the_domain_model()
    {
        var vm = new ElementViewModel(DesignerElementKind.Chart, "chart-1")
        {
            X = Unit.FromMm(0),
            Y = Unit.FromMm(0),
            Width = Unit.FromMm(80),
            Height = Unit.FromMm(55),
            ChartKind = ChartKind.Pie,
            ChartTitle = "Vendas",
            ShowLegend = false,
        };
        vm.ChartSeries.Add(new ChartSeriesRule
        {
            Name = "Receita",
            CategoryExpression = "Fields.mes",
            ValueExpression = "Fields.total",
        });

        var element = vm.ToElement();

        element.Should().BeOfType<ChartElement>();
        var chart = (ChartElement)element;
        chart.Kind.Should().Be(ChartKind.Pie);
        chart.Title.Should().Be("Vendas");
        chart.ShowLegend.Should().BeFalse();
        chart.Series.Should().HaveCount(1);
        chart.Series[0].Name.Should().Be("Receita");
        chart.Series[0].CategoryExpression.Should().Be("Fields.mes");
        chart.Series[0].ValueExpression.Should().Be("Fields.total");

        var back = ElementViewModel.FromElement(chart);
        back.Kind.Should().Be(DesignerElementKind.Chart);
        back.ChartKind.Should().Be(ChartKind.Pie);
        back.ChartTitle.Should().Be("Vendas");
        back.ShowLegend.Should().BeFalse();
        back.ChartSeries.Should().ContainSingle();
        back.ChartSeries[0].ValueExpression.Should().Be("Fields.total");
    }

    [Fact]
    public void Cloning_a_chart_deep_copies_its_series()
    {
        var vm = new ElementViewModel(DesignerElementKind.Chart, "c")
        {
            ChartKind = ChartKind.Line,
            ChartTitle = "T",
        };
        vm.ChartSeries.Add(new ChartSeriesRule { Name = "A" });

        var clone = vm.Clone();

        clone.Kind.Should().Be(DesignerElementKind.Chart);
        clone.ChartKind.Should().Be(ChartKind.Line);
        clone.ChartSeries.Should().ContainSingle();
        clone.ChartSeries[0].Name.Should().Be("A");

        // Independent copies — mutating the clone must not touch the original.
        clone.ChartSeries[0].Name = "B";
        vm.ChartSeries[0].Name.Should().Be("A");
    }

    [Fact]
    public void Bubble_and_stock_series_fields_round_trip_in_the_designer()
    {
        var bubbleVm = new ElementViewModel(DesignerElementKind.Chart, "c") { ChartKind = ChartKind.Bubble };
        bubbleVm.ChartSeries.Add(new ChartSeriesRule
        {
            Name = "Bolhas",
            CategoryExpression = "Fields.x",
            ValueExpression = "Fields.y",
            SizeExpression = "Fields.peso",
        });

        var bubble = (ChartElement)bubbleVm.ToElement();
        bubble.Kind.Should().Be(ChartKind.Bubble);
        bubble.Series[0].SizeExpression.Should().Be("Fields.peso");
        ElementViewModel.FromElement(bubble).ChartSeries[0].SizeExpression.Should().Be("Fields.peso");

        var stockVm = new ElementViewModel(DesignerElementKind.Chart, "s") { ChartKind = ChartKind.Stock };
        stockVm.ChartSeries.Add(new ChartSeriesRule
        {
            Name = "Preço",
            CategoryExpression = "Fields.dia",
            ValueExpression = "Fields.fech",
            HighExpression = "Fields.alta",
            LowExpression = "Fields.baixa",
        });

        var stock = (ChartElement)stockVm.ToElement();
        stock.Series[0].HighExpression.Should().Be("Fields.alta");
        stock.Series[0].LowExpression.Should().Be("Fields.baixa");
        ElementViewModel.FromElement(stock).ChartSeries[0].LowExpression.Should().Be("Fields.baixa");
    }
}
