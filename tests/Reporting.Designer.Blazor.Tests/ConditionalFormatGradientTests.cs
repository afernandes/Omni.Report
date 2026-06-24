using FluentAssertions;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Styling;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// A conditional-format rule can carry a gradient fill (BackColor → BackColorEnd along BackgroundGradient).
/// The render path already honours it (StyleResolver.Merge, #179); this verifies the Designer rule VM round-trips
/// the gradient through ToElement/FromElement so the editor's new inputs persist.
/// </summary>
public class ConditionalFormatGradientTests
{
    private static readonly Color Red = Color.FromRgb(255, 0, 0);
    private static readonly Color Blue = Color.FromRgb(0, 0, 255);

    private static ElementViewModel TextBoxVm() => ElementViewModel.FromElement(new TextBoxElement
    {
        Id = "tb",
        Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(50), Unit.FromMm(10)),
        Expression = "x",
    });

    [Fact]
    public void A_conditional_format_gradient_survives_to_element_and_back()
    {
        var vm = TextBoxVm();
        vm.ConditionalFormats.Add(new ConditionalFormatRule
        {
            Condition = "Fields.Total < 0",
            BackColor = Red,
            BackColorEnd = Blue,
            BackgroundGradient = BackgroundGradientType.TopBottom,
        });

        var element = vm.ToElement();
        var cf = element.ConditionalFormats.First();
        cf.Style.BackColor.Should().Be(Red);
        cf.Style.BackColorEnd.Should().Be(Blue);
        cf.Style.BackgroundGradient.Should().Be(BackgroundGradientType.TopBottom);

        // Round-trip back into a rule VM.
        var back = ElementViewModel.FromElement(element).ConditionalFormats.First();
        back.BackColorEnd.Should().Be(Blue);
        back.BackgroundGradient.Should().Be(BackgroundGradientType.TopBottom);
    }
}
