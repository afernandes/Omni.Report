using FluentAssertions;
using Reporting.Common;
using Reporting.Designer.Blazor.Services;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Elements;
using Reporting.Geometry;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// Foundation for the SSRS-style <c>fx</c> binding in the metadata PropertyGrid (Phase 1a):
/// <see cref="PropertyGridDescriptor"/> exposes which properties are expression-bindable and their
/// path, and <see cref="ElementViewModel"/> tracks <see cref="ReportElement.PropertyExpressions"/> so a
/// load → edit → save cycle through the designer preserves them (previously dropped) and the grid can
/// get/set per-property expressions.
/// </summary>
public class PropertyGridBindingTests
{
    [Fact]
    public void Bindable_descriptors_expose_their_binding_path_and_editor()
    {
        var direction = PropertyGridDescriptors.For(typeof(LineElement)).Single(d => d.Name == "Direction");
        direction.Bindable.Should().BeTrue();
        direction.Path.Should().Be("Direction");

        var rect = PropertyGridDescriptors.For(typeof(RectangleElement));
        var fill = rect.Single(d => d.Name == "FillColor");
        fill.Bindable.Should().BeTrue();
        fill.Editor.Should().Be("color-picker");
        fill.Category.Should().Be("Forma");
        fill.Path.Should().Be("FillColor");

        var radius = rect.Single(d => d.Name == "CornerRadius");
        radius.Bindable.Should().BeTrue();
        radius.Editor.Should().Be("unit-spinner");
    }

    [Fact]
    public void Designer_view_model_round_trips_property_expressions_for_a_basic_element()
    {
        // Regression guard: a Rectangle is a "basic" VM (property-bag, not _sourceElement-backed), so
        // before this the designer dropped its PropertyExpressions on load → save.
        var rect = new RectangleElement
        {
            Id = "r1",
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(40), Unit.FromMm(20)),
            PropertyExpressions = new EquatableDictionary<string, string>(new Dictionary<string, string>
            {
                ["FillColor"] = "Fields.Status == 'OK' ? '#0A0' : '#A00'",
                ["Visible"] = "Fields.Total > 0",
            }),
        };

        var roundTripped = ElementViewModel.FromElement(rect).ToElement();

        roundTripped.PropertyExpressions.Should().BeEquivalentTo(rect.PropertyExpressions);
    }

    [Fact]
    public void Set_property_expression_binds_then_clears_reverting_to_static()
    {
        var vm = ElementViewModel.FromElement(new LineElement
        {
            Id = "l1",
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(40), Unit.FromMm(0)),
        });

        vm.GetPropertyExpression("Direction").Should().BeNull("nothing is bound initially");

        vm.SetPropertyExpression("Direction", "Fields.Horizontal ? 0 : 1");
        vm.GetPropertyExpression("Direction").Should().Be("Fields.Horizontal ? 0 : 1");
        vm.ToElement().PropertyExpressions["Direction"].Should().Be("Fields.Horizontal ? 0 : 1");

        vm.SetPropertyExpression("Direction", null);
        vm.GetPropertyExpression("Direction").Should().BeNull("a blank expression clears the binding");
        vm.ToElement().PropertyExpressions.Should().BeEmpty();
    }

    [Fact]
    public void Clone_carries_property_expressions()
    {
        var vm = ElementViewModel.FromElement(new EllipseElement
        {
            Id = "e1",
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(20), Unit.FromMm(20)),
        });
        vm.SetPropertyExpression("FillColor", "Fields.Cor");

        var clone = vm.Clone();

        clone.GetPropertyExpression("FillColor").Should().Be("Fields.Cor");
        clone.ToElement().PropertyExpressions["FillColor"].Should().Be("Fields.Cor");
    }
}
