using Bunit;
using FluentAssertions;
using Reporting.Designer.Blazor.Components;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// "Abrir .repx…" / "Importar RDL…" wrap a hidden <c>&lt;InputFile&gt;</c> in a <c>&lt;label&gt;</c> that uses
/// <c>@onclick:stopPropagation</c>. Without it the dropdown panel's own <c>@onclick</c> closes the menu the instant
/// the label is clicked — removing the <c>&lt;InputFile&gt;</c> from the DOM before the OS file dialog returns, so
/// the file silently fails to open. This guards that the file items keep the menu open while a normal item closes it.
/// </summary>
public class DropdownFileItemTests : Bunit.BunitContext
{
    public DropdownFileItemTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private IRenderedComponent<TopBar> OpenArquivoMenu()
    {
        var cut = Render<TopBar>(p => p.Add(x => x.DocumentName, "r"));
        cut.FindAll("button.menu-item").First(b => b.TextContent.Contains("Arquivo")).Click();
        cut.FindAll(".dropdown-panel").Should().ContainSingle("the Arquivo menu is open");
        return cut;
    }

    [Fact]
    public void Clicking_Abrir_repx_keeps_the_menu_open()
    {
        var cut = OpenArquivoMenu();
        cut.FindAll("label").First(l => l.TextContent.Contains("Abrir .repx")).Click();
        cut.FindAll(".dropdown-panel").Should().ContainSingle(
            "stopPropagation keeps the dropdown open so the <InputFile> survives until the file dialog returns");
    }

    [Fact]
    public void Clicking_Importar_RDL_keeps_the_menu_open()
    {
        var cut = OpenArquivoMenu();
        cut.FindAll("label").First(l => l.TextContent.Contains("Importar RDL")).Click();
        cut.FindAll(".dropdown-panel").Should().ContainSingle("the RDL import item also keeps the menu open");
    }

    [Fact]
    public void Clicking_a_normal_item_still_closes_the_menu()
    {
        var cut = OpenArquivoMenu();
        cut.FindAll("button.dropdown-item").First(b => b.TextContent.Contains("Salvar")).Click();
        cut.FindAll(".dropdown-panel").Should().BeEmpty("a normal item's click bubbles to the panel and closes it");
    }
}
