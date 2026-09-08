using System.Globalization;
using FluentAssertions;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Geometry;
using Xunit;
namespace Reporting.Designer.Blazor.Tests;

public sealed class AuditDocumentTests
{
    [Fact]
    public void ActiveTab_DoisDocumentos_IsolaCatalogosHistoricoEPreview()
    {
        var state = new DesignerState();
        var first = state.ActiveTab;
        var band = state.Report.FindBand(DesignerBandKind.Detail)!;
        var element = new ElementViewModel(DesignerElementKind.Label, "primeiro");
        state.History.Push(new AddElementCommand(band, element));
        state.SelectedElement = element;
        state.PreviewDataRegistry = new();
        first.ParameterValues["Valor"] = "1.25";
        var second = state.OpenNewDocument("Segundo");
        state.DataSources.Should().BeEmpty();
        state.Parameters.Should().BeEmpty();
        state.ActiveBand.Should().BeNull();
        state.SelectedElements.Should().BeEmpty();
        state.History.CanUndo.Should().BeFalse();
        state.PreviewDataRegistry.Should().BeNull();
        second.ParameterValues.Should().BeEmpty();
        state.ActiveTab = first;
        state.History.Undo().Should().BeTrue();
        band.Elements.Should().BeEmpty();
        state.PreviewDataRegistry.Should().NotBeNull();
        first.ParameterValues["Valor"].Should().Be("1.25");
    }
    [Fact]
    public void ReplaceActiveReport_AlteracoesAntigasEDoNovo_ReportaSomenteNovoDocumento()
    {
        var state = new DesignerState();
        var old = state.Report;
        var current = new ReportDefinitionViewModel("Novo");
        state.ReplaceActiveReport(current);
        old.Name = "Não está aberto";
        state.IsDirty.Should().BeFalse();
        current.Name = "Alterado";
        state.IsDirty.Should().BeTrue();
    }
    [Fact]
    public void Undo_RemocaoNoMeio_RestauraOrdemOriginal()
    {
        var state = new DesignerState();
        var band = state.Report.FindBand(DesignerBandKind.Detail)!;
        var elements = Enumerable.Range(0, 3).Select(i => new ElementViewModel(DesignerElementKind.Label, i.ToString())).ToArray();
        foreach (var element in elements) band.AddElement(element);
        state.History.Push(new RemoveElementCommand(band, elements[1]));
        state.History.Undo();
        band.Elements.Should().Equal(elements);
        state.History.Redo();
        band.Elements.Should().Equal(elements[0], elements[2]);
    }
    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en-US")]
    [InlineData("")]
    public void SaveLoad_DefaultMonetario_PreservaValorEntreCulturas(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var state = new DesignerState();
            state.Parameters.Clear();
            state.Parameters.Add(new DesignerParameter("Valor", DesignerFieldType.Money, "1,25"));
            state.Load(state.Save());
            var parameter = state.Parameters.Single();
            parameter.DefaultValue.Should().Be("1.25");
            ParameterValueConverter.Parse(parameter.DefaultValue, parameter.Type).Should().Be(1.25m);
            state.BuildDefinition().Parameters.Single().DefaultValue.Should().Be(1.25m);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
