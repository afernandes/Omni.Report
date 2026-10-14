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
