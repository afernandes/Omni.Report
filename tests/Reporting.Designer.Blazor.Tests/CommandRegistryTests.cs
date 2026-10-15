using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using Reporting.Designer.Blazor.Components;
using Reporting.Designer.Blazor.Services;
using Reporting.Designer.Blazor.ViewModels;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// One command list drives the menus, the palette and the shortcuts dialog. These pin that every command in it is
/// actually routed (no dead entries), that the palette and the Inserir menu reach everything, and that "Validar" and
/// "Atalhos de teclado" do what they say.
/// </summary>
public sealed class CommandRegistryTests : BunitContext
{
    public CommandRegistryTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    [Fact]
    public async Task OnMenuCommand_TodoComandoDoRegistro_ERoteadoPeloDesigner()
    {
        var state = new DesignerState();
        state.Parameters.Clear();
        var designer = Render<ReportDesigner>(p => p.Add(x => x.InitialState, state));
        var unrouted = new List<string>();

        foreach (var command in DesignerCommandCatalog.Build([]))
        {
            await designer.InvokeAsync(() => state.CommandPaletteOpen = true);
            var item = designer.FindAll(".cmdp-item").First(i => i.TextContent.Contains(command.Title, StringComparison.Ordinal));
            await designer.InvokeAsync(() => item.Click());
            if (designer.FindAll(".designer-alert").Any(a => a.TextContent.Contains("Comando não reconhecido")))
            {
                unrouted.Add(command.Id);
                designer.Find(".designer-alert button").Click();
            }
        }

        unrouted.Should().BeEmpty("a command in the list must do something when chosen");
    }

    [Fact]
    public void CommandPalette_AoAbrir_ListaTodosOsComandosEFocaABusca()
    {
        var commands = DesignerCommandCatalog.Build([]);
        var palette = Render<CommandPalette>(p => p.Add(x => x.Visible, false).Add(x => x.Commands, commands));

        palette.Render(p => p.Add(x => x.Visible, true));

        palette.FindAll(".cmdp-item").Should().HaveCount(commands.Count);
        palette.FindAll(".cmdp-item").Select(i => i.TextContent).Should().Contain(t => t.Contains("Alinhar à esquerda"));
        JSInterop.VerifyFocusAsyncInvoke().Arguments[0].Should().NotBeNull("the search box gets focus on open");
    }

    [Fact]
    public void TopBar_MenuInserir_TemUmItemPorTipoDaToolbox()
    {
        var topBar = Render<TopBar>(p => p.Add(x => x.Commands, DesignerCommandCatalog.Build([]))
            .AddCascadingValue(new DesignerMenuCoordinator()));

        topBar.FindAll("button").First(b => b.TextContent.Trim() == "Inserir").Click();

        topBar.FindAll(".dropdown-item").Should().HaveCount(ToolboxCatalog.Groups.Sum(g => g.Items.Count));
    }

    [Fact]
    public void TopBar_MenuOrganizar_ExpoeAlinhamentoECamadas()
    {
        var topBar = Render<TopBar>(p => p.Add(x => x.Commands, DesignerCommandCatalog.Build([]))
            .AddCascadingValue(new DesignerMenuCoordinator()));

        topBar.FindAll("button").First(b => b.TextContent.Trim() == "Organizar").Click();

        var items = topBar.FindAll(".dropdown-item").Select(i => i.TextContent.Trim()).ToList();
        items.Should().Contain(t => t.Contains("Alinhar à esquerda")).And.Contain(t => t.Contains("Enviar para trás"));
    }

    [Fact]
    public async Task Validar_ExpressaoComCampoInexistente_ListaOElemento()
    {
        var state = new DesignerState();
        var band = state.Report.FindBand(DesignerBandKind.Detail)!;
        band.Elements.Add(new ElementViewModel(DesignerElementKind.TextBox, "caixa")
        {
            Name = "TotalErrado",
            Expression = "{Fields.CampoQueNaoExiste}",
        });
        var designer = Render<ReportDesigner>(p => p.Add(x => x.InitialState, state));

        await RunFromPalette(designer, state, "Validar expressões");

        designer.Find(".designer-alert").TextContent.Should().Contain("TotalErrado").And.Contain("1 expressão");
    }

    [Fact]
    public async Task Validar_RelatorioSemProblemas_InformaSucesso()
    {
        var state = new DesignerState();
        var designer = Render<ReportDesigner>(p => p.Add(x => x.InitialState, state));

        await RunFromPalette(designer, state, "Validar expressões");

        var alert = designer.Find(".designer-alert");
        alert.ClassList.Should().Contain("is-ok");
        alert.TextContent.Should().Contain("Nenhum problema");
    }

    [Fact]
    public void Teclado_CtrlBarra_AbreOsAtalhosComOsDoRegistro()
    {
        var designer = Render<ReportDesigner>(p => p.Add(x => x.InitialState, new DesignerState()));

        designer.Find(".report-designer").KeyDown(new KeyboardEventArgs { Key = "/", CtrlKey = true });

        var table = designer.Find(".shortcuts-table").TextContent;
        table.Should().Contain("Ctrl+Z").And.Contain("Desfazer").And.Contain("Shift+Setas");
    }

    [Fact]
    public void Teclado_CtrlShiftL_AlternaOTema()
    {
        var state = new DesignerState();
        var designer = Render<ReportDesigner>(p => p.Add(x => x.InitialState, state));

        designer.Find(".report-designer").KeyDown(new KeyboardEventArgs { Key = "L", CtrlKey = true, ShiftKey = true });

        state.Theme.Should().Be("dark", "Ctrl+Shift+L is the theme shortcut the menu advertises");
    }

    private static async Task RunFromPalette(IRenderedComponent<ReportDesigner> designer, DesignerState state, string title)
    {
        await designer.InvokeAsync(() => state.CommandPaletteOpen = true);
        var item = designer.FindAll(".cmdp-item").First(i => i.TextContent.Contains(title, StringComparison.Ordinal));
        await designer.InvokeAsync(() => item.Click());
    }
}
