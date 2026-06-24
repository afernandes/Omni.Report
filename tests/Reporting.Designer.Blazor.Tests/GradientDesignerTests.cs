using FluentAssertions;
using Reporting.Designer.Blazor.Services;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Styling;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// Designer support for the background gradient: the metadata grid auto-exposes the two new Style props
/// (color-picker for BackColorEnd, enum for BackgroundGradient — no custom editor), and — critically — the
/// ElementViewModel materialises them through ToElement/LoadFrom so editing ANY property doesn't silently
/// zero the gradient (the "designer materializes Style" trap).
/// </summary>
public class GradientDesignerTests
{
    private static ElementViewModel TextBoxVm() => ElementViewModel.FromElement(new TextBoxElement
    {
        Id = "tb",
        Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(50), Unit.FromMm(10)),
        Expression = "x",
    });

    [Fact]
    public void The_grid_exposes_the_gradient_props_with_the_existing_editors()
    {
        var descs = PropertyGridDescriptors.For(typeof(TextBoxElement));
        descs.Should().Contain(d => d.Path == "Style.BackColorEnd" && d.Editor == "color-picker",
            "the gradient end colour reuses the color-picker editor");
        descs.Should().Contain(d => d.Path == "Style.BackgroundGradient" && d.Editor == "enum",
            "the gradient direction reuses the enum editor");
    }

    [Fact]
    public void The_gradient_survives_ToElement()
    {
        var vm = TextBoxVm();
        vm.BackColor = Color.FromRgb(255, 0, 0);
        vm.BackColorEnd = Color.FromRgb(0, 0, 255);
        vm.BackgroundGradient = BackgroundGradientType.TopBottom;

        var style = vm.ToElement().Style;
        style.BackColor.Should().Be(Color.FromRgb(255, 0, 0));
        style.BackColorEnd.Should().Be(Color.FromRgb(0, 0, 255));
        style.BackgroundGradient.Should().Be(BackgroundGradientType.TopBottom);
    }

    [Fact]
    public void Editing_another_property_does_not_drop_the_gradient()
    {
        // Every ApplyMetaSet round-trips ToElement→LoadFrom; a Style field not reconstructed there is silently
        // zeroed when ANY other property is edited. This guards the materialisation wiring.
        var vm = TextBoxVm();
        vm.BackColor = Color.FromRgb(255, 0, 0);
        vm.BackColorEnd = Color.FromRgb(0, 0, 255);
        vm.BackgroundGradient = BackgroundGradientType.LeftRight;

        var fmt = PropertyGridDescriptors.For(typeof(TextBoxElement)).First(d => d.Path == "Style.Format");
        vm.ApplyMetaSet(fmt, "C2"); // edit an unrelated property

        vm.BackColorEnd.Should().Be(Color.FromRgb(0, 0, 255), "the gradient must survive editing another property");
        vm.BackgroundGradient.Should().Be(BackgroundGradientType.LeftRight);
    }
}
