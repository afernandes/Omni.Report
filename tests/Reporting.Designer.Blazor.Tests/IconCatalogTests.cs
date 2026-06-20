using Bunit;
using FluentAssertions;
using Reporting.Designer.Blazor.Icons;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// Toolbar icons must exist in the catalog — a missing name falls back to a plain circle (and the
/// same-size / z-order buttons were rendering as bare circles/square because these Lucide names weren't
/// in the catalog). Each here renders its real path, not the fallback.
/// </summary>
public class IconCatalogTests : Bunit.BunitContext
{
    [Theory]
    [InlineData("move-horizontal", "M2 12h20")]   // toolbar: "mesma largura"
    [InlineData("move-vertical", "M12 2v20")]     // toolbar: "mesma altura"
    [InlineData("chevrons-up", "m17 11-5-5-5 5")] // toolbar: "trazer para a frente"
    public void Toolbar_icon_renders_its_real_path_not_the_fallback_circle(string name, string pathFragment)
    {
        var cut = Render<Icon>(p => p.Add(x => x.Name, name));

        cut.Markup.Should().Contain(pathFragment, $"'{name}' must resolve to its real Lucide path");
        cut.Markup.Should().NotContain("r='9'", "the fallback circle (r=9) means the name is missing from the catalog");
    }
}
