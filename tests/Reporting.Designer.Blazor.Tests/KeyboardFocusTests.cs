using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Reporting.Designer.Blazor.Components;
using Reporting.Designer.Blazor.Services;
using Reporting.Designer.Blazor.ViewModels;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// Shortcuts live on the Designer root, so where keyboard focus sits decides what a key does. These pin that the
/// palette and the dialogs keep their keys to themselves (Esc, Delete, Ctrl+Z must not act on the report behind them)
/// and hand focus back to the root when they close, so the shortcuts keep working without another click.
/// </summary>
public sealed class KeyboardFocusTests : BunitContext
{
    public KeyboardFocusTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    [Fact]
    public void CommandPalette_RenderizadaJaAberta_FocaABusca()
    {
        Render<CommandPalette>(p => p.Add(x => x.Visible, true).Add(x => x.Commands, DesignerCommandCatalog.Build([])));

        JSInterop.VerifyFocusAsyncInvoke().Arguments[0].Should().NotBeNull("the first display focuses the search box too");
    }

    [Fact]
    public async Task Paleta_EscNaBusca_FechaSemLimparASelecao()
    {
        var (designer, state, element) = RenderWithSelectedElement();
        await designer.InvokeAsync(() => state.CommandPaletteOpen = true);

        await designer.Find(".cmdp-input input").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        state.CommandPaletteOpen.Should().BeFalse();
        state.SelectedElement.Should().BeSameAs(element, "the Escape that closes the palette must not also reach the canvas");
    }

    [Fact]
    public async Task Paleta_AoFecharOuExecutar_DevolveOFocoAoDesigner()
    {
        var (designer, state, _) = RenderWithSelectedElement();
        var root = RootReference();

        await designer.InvokeAsync(() => state.CommandPaletteOpen = true);
        await designer.Find(".cmdp-input input").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        LastFocused().Should().Be(root, "closing the palette removes the focused search box");

        await designer.InvokeAsync(() => state.CommandPaletteOpen = true);
        await designer.Find(".cmdp-item[data-command-id='view.grid']").ClickAsync(new MouseEventArgs());
        LastFocused().Should().Be(root, "running a command closes the palette the same way");
    }

    [Fact]
    public async Task Atalhos_AoAbrir_FocaODialogoEBloqueiaOsAtalhosDoCanvas()
    {
        var (designer, state, element) = RenderWithSelectedElement();
        var band = state.Report.FindBand(DesignerBandKind.Detail)!;

        await designer.Find(".report-designer").KeyDownAsync(new KeyboardEventArgs { Key = "/", CtrlKey = true });

        var dialog = designer.Find(".dlg-modal");
        LastFocused().Should().Be(ReferenceOf(dialog), "keys typed next must land in the dialog");
        await dialog.KeyDownAsync(new KeyboardEventArgs { Key = "Delete" });
        await designer.Find(".report-designer").KeyDownAsync(new KeyboardEventArgs { Key = "Delete" });
        band.Elements.Should().Contain(element, "Delete must not remove an element behind an open dialog");
        state.SelectedElement.Should().BeSameAs(element);
    }

    [Fact]
    public async Task Atalhos_EscFechaODialogoSemLimparASelecaoEDevolveOFoco()
    {
        var (designer, state, element) = RenderWithSelectedElement();
        var root = RootReference();
        await designer.Find(".report-designer").KeyDownAsync(new KeyboardEventArgs { Key = "/", CtrlKey = true });

        await designer.Find(".dlg-modal").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        designer.FindAll(".shortcuts-table").Should().BeEmpty("Esc closes the dialog");
        state.SelectedElement.Should().BeSameAs(element, "the same Escape must not clear the selection behind it");
        LastFocused().Should().Be(root, "focus goes back where the shortcuts are handled");
    }

    private (IRenderedComponent<ReportDesigner> Designer, DesignerState State, ElementViewModel Element) RenderWithSelectedElement()
    {
        var state = new DesignerState();
        state.Parameters.Clear();
        var element = new ElementViewModel(DesignerElementKind.TextBox, "caixa") { Expression = "{Fields.Produto}" };
        state.Report.FindBand(DesignerBandKind.Detail)!.Elements.Add(element);
        var designer = Render<ReportDesigner>(p => p.Add(x => x.InitialState, state));
        designer.InvokeAsync(() => state.SelectedElement = element).GetAwaiter().GetResult();
        return (designer, state, element);
    }

    // bUnit renders a freshly inserted @ref as a `blazor:elementreference` attribute holding the reference id.
    private static string ReferenceOf(IElement element)
        => element.GetAttribute("blazor:elementreference") is { Length: > 0 } id
            ? id
            : throw new InvalidOperationException("Element has no @ref; focus can only target a captured reference.");

    // The root is the element the Designer registers with its JS module on first render.
    private string RootReference()
        => ((ElementReference)JSInterop.Invocations["omniDesigner.register"][0].Arguments[0]!).Id;

    private string LastFocused()
        => JSInterop.Invocations
            .Where(i => i.Identifier.EndsWith(".focus", StringComparison.Ordinal))
            .Select(i => i.Arguments[0])
            .OfType<ElementReference>()
            .Last().Id;
}
