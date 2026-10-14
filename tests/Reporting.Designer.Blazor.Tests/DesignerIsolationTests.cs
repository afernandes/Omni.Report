using Bunit;
using FluentAssertions;
using Reporting.Designer.Blazor.Components;
using Reporting.Designer.Blazor.Services;
using Reporting.Designer.Blazor.ViewModels;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

public sealed class DesignerIsolationTests : BunitContext
{
    [Theory]
    [InlineData("Bloquear")]
    [InlineData("Ocultar")]
    public async Task MenuContextual_AlteraPropriedade_DesfazERefazSemAfetarOutroDesigner(string command)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var first = new DesignerState();
        var second = new DesignerState();
        var element = new ElementViewModel(DesignerElementKind.Label, "elemento");
        first.Report.FindBand(DesignerBandKind.Detail)!.Elements.Add(element);
        var component = Render<ReportDesigner>(parameters => parameters.Add(p => p.InitialState, first));
        using var other = Render<ReportDesigner>(parameters => parameters.Add(p => p.InitialState, second));
        await component.InvokeAsync(() => component.Instance.OpenElementContextMenu(element, 100, 100));
        component.FindAll(".ctx-item").Single(button => button.TextContent.Trim() == command).Click();
        bool Actual() => command == "Bloquear" ? element.IsLocked : !element.IsVisible;
        Actual().Should().BeTrue();
        second.History.CanUndo.Should().BeFalse();
        await component.InvokeAsync(() => first.History.Undo());
        Actual().Should().BeFalse();
        await component.InvokeAsync(() => first.History.Redo());
        Actual().Should().BeTrue();
    }

    [Fact]
    public void ElementSnapshotCommand_SomenteBloqueio_PreservaInstanciasEBloqueioDosFilhos()
    {
        var container = ElementViewModel.FromElement(new Reporting.Elements.RectangleElement
        {
            Id = "rect",
            Bounds = new(Reporting.Geometry.Unit.Zero, Reporting.Geometry.Unit.Zero,
                Reporting.Geometry.Unit.FromMm(100), Reporting.Geometry.Unit.FromMm(60)),
            Children = Reporting.Common.EquatableArray.Create<Reporting.Elements.ReportElement>(
                new Reporting.Elements.LabelElement
                {
                    Id = "filho",
                    Text = "Filho",
                    Bounds = new(Reporting.Geometry.Unit.Zero, Reporting.Geometry.Unit.Zero,
                        Reporting.Geometry.Unit.FromMm(40), Reporting.Geometry.Unit.FromMm(6)),
                })
        });
        var filho = container.Children.Single();
        filho.IsLocked = true;
        var snapshot = container.ToElement();
        var command = new ElementSnapshotCommand(container, snapshot, snapshot, beforeLocked: false, afterLocked: true);

        command.Execute();
        command.Undo();

        container.IsLocked.Should().BeFalse();
        container.Children.Single().Should().BeSameAs(filho);
        filho.IsLocked.Should().BeTrue();
    }

    [Fact]
    public void Open_MenusEmCoordenadoresDiferentes_NaoFechaOutraInstancia()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var first = new DesignerMenuCoordinator();
        var second = new DesignerMenuCoordinator();
        using var left = Render<DropdownMenu>(p => p.Add(x => x.Label, "Primeiro").AddCascadingValue(first));
        using var right = Render<DropdownMenu>(p => p.Add(x => x.Label, "Segundo").AddCascadingValue(second));
        left.Find("button").Click();
        right.Find("button").Click();
        left.FindAll(".dropdown-panel").Should().ContainSingle();
        right.FindAll(".dropdown-panel").Should().ContainSingle();
    }
}
