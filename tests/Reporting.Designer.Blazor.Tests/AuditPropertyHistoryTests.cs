using Bunit;
using FluentAssertions;
using Reporting.Designer.Blazor.Components;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Geometry;
using Xunit;
namespace Reporting.Designer.Blazor.Tests;

public sealed class AuditPropertyHistoryTests : BunitContext
{
    [Fact]
    public async Task Change_PropriedadePeloFormulario_DesfazERefazValor()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var history = new CommandHistory();
        var element = new ElementViewModel(DesignerElementKind.Label, "rotulo") { X = 1.Mm() };
        var component = Render<PropertyGrid>(parameters => parameters.Add(x => x.Element, element).AddCascadingValue(history));
        component.Find("input[type=number]").Change("12.5");
        element.X.Should().Be(12.5.Mm());
        history.CanUndo.Should().BeTrue();
        await component.InvokeAsync(() => history.Undo());
        element.X.Should().Be(1.Mm());
        await component.InvokeAsync(() => history.Redo());
        element.X.Should().Be(12.5.Mm());
    }
}
