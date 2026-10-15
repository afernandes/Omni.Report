using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using Reporting.Designer.Blazor.ViewModels;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// Keys typed in a text field bubble up to the Designer root, whose shortcuts act on the selection. Backspace in the
/// property grid used to delete the selected element. designer.js reports when focus is in a text field
/// (<c>OnTextFieldFocusChanged</c>); these pin that the root then leaves editing keys to the field while the
/// Designer-wide shortcuts keep working.
/// </summary>
public sealed class TextFieldKeysTests : BunitContext
{
    public TextFieldKeysTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    [Theory]
    [InlineData("Backspace")]
    [InlineData("Delete")]
    public async Task TeclaDeApagar_DigitadaNumCampo_NaoApagaOElementoSelecionado(string key)
    {
        var (designer, state, element) = RenderWithSelectedElement();
        await designer.InvokeAsync(() => designer.Instance.OnTextFieldFocusChanged(true));

        await PropertyField(designer).KeyDownAsync(new KeyboardEventArgs { Key = key });

        Band(state).Elements.Should().Contain(element, $"{key} in a field erases text, not the selected element");
    }

    [Fact]
    public async Task TeclasDeEdicao_DigitadasNumCampo_NaoMexemNaSelecaoNemNoHistorico()
    {
        var (designer, state, element) = RenderWithSelectedElement();
        var x = element.X;
        await designer.InvokeAsync(() => designer.Instance.OnTextFieldFocusChanged(true));
        var field = PropertyField(designer);

        await field.KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight" });
        await PropertyField(designer).KeyDownAsync(new KeyboardEventArgs { Key = "a", CtrlKey = true });
        await PropertyField(designer).KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        element.X.Should().Be(x, "arrows move the caret, not the element");
        state.SelectedElements.Should().ContainSingle().Which.Should().BeSameAs(element,
            "Ctrl+A selects the field's text and Escape stays in the field");
    }

    [Fact]
    public async Task AtalhosGlobais_DigitadosNumCampo_ContinuamFuncionando()
    {
        var (designer, state, _) = RenderWithSelectedElement();
        await designer.InvokeAsync(() => designer.Instance.OnTextFieldFocusChanged(true));

        await PropertyField(designer).KeyDownAsync(new KeyboardEventArgs { Key = "L", CtrlKey = true, ShiftKey = true });
        await PropertyField(designer).KeyDownAsync(new KeyboardEventArgs { Key = "k", CtrlKey = true });

        state.Theme.Should().Be("dark");
        state.CommandPaletteOpen.Should().BeTrue("Ctrl+K opens the palette from anywhere in the Designer");
    }

    [Fact]
    public async Task Backspace_ComOFocoDeVoltaAoCanvas_ApagaOElemento()
    {
        var (designer, state, element) = RenderWithSelectedElement();
        await designer.InvokeAsync(() => designer.Instance.OnTextFieldFocusChanged(true));
        await designer.InvokeAsync(() => designer.Instance.OnTextFieldFocusChanged(false));

        await designer.Find(".report-designer").KeyDownAsync(new KeyboardEventArgs { Key = "Backspace" });

        Band(state).Elements.Should().NotContain(element, "outside a field Backspace is still the delete shortcut");
    }

    private (IRenderedComponent<ReportDesigner> Designer, DesignerState State, ElementViewModel Element) RenderWithSelectedElement()
    {
        var state = new DesignerState();
        state.Parameters.Clear();
        var element = new ElementViewModel(DesignerElementKind.TextBox, "caixa") { Expression = "{Fields.Produto}" };
        Band(state).Elements.Add(element);
        var designer = Render<ReportDesigner>(p => p.Add(x => x.InitialState, state));
        designer.InvokeAsync(() => state.SelectedElement = element).GetAwaiter().GetResult();
        return (designer, state, element);
    }

    private static BandViewModel Band(DesignerState state) => state.Report.FindBand(DesignerBandKind.Detail)!;

    // A text input of the property grid; keydown on it bubbles to the root like a real keystroke.
    private static AngleSharp.Dom.IElement PropertyField(IRenderedComponent<ReportDesigner> designer)
        => designer.FindAll(".right-panel input[type=text], aside input[type=text]").First();
}
