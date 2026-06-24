using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Elements;
using Reporting.Styling;
using Xunit;

namespace Reporting.CodeFirst.Tests;

/// <summary>Code-first authoring of named/reusable styles: <c>ReportBuilderRoot.NamedStyle</c> defines a reusable
/// style and <c>BandContent.BasedOn</c> references it, both landing in the built <see cref="ReportDefinition"/>.</summary>
public class NamedStyleCodeFirstTests
{
    [Fact]
    public void NamedStyle_and_BasedOn_build_into_the_definition()
    {
        var def = ReportBuilder.Create("c")
            .NamedStyle("titulo", s => s with
            {
                ForeColor = Color.FromRgb(0, 0, 128),
                HorizontalAlignment = HorizontalAlignment.Center,
            })
            .ReportHeader(h => h.Text("Fields.X").BasedOn("titulo"))
            .Build().Definition;

        def.NamedStyles.ContainsKey("titulo").Should().BeTrue("the named style is registered on the report");
        var named = def.NamedStyles["titulo"];
        named.ForeColor.Should().Be(Color.FromRgb(0, 0, 128));
        named.HorizontalAlignment.Should().Be(HorizontalAlignment.Center);

        var tb = def.ReportHeader!.Elements.OfType<TextBoxElement>().Single();
        tb.Style.BasedOn.Should().Be("titulo", "the element references the named style");
    }
}
