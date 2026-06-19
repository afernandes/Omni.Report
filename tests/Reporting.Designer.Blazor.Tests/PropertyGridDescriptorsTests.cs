using FluentAssertions;
using Reporting.Designer.Blazor.Services;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Metadata;
using Reporting.Styling;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// Foundation of the metadata-driven PropertyGrid (Phase 0): the descriptor service discovers
/// [PropertyGrid]-annotated properties via reflection (including inherited ones), infers the editor
/// from the property's type, and builds an immutable setter — so a new element type gets its (and its
/// base's) editors automatically, with no hand-coded UI per kind. None of this touches code-first /
/// low-level authoring.
/// </summary>
public class PropertyGridDescriptorsTests
{
    [Fact]
    public void Discovers_an_annotated_property_with_a_type_inferred_editor()
    {
        var descriptors = PropertyGridDescriptors.For(typeof(LineElement));

        var direction = descriptors.Should().ContainSingle(d => d.Name == "Direction").Subject;
        direction.Editor.Should().Be("enum", "a LineDirection enum infers the dropdown editor");
        direction.Category.Should().Be("Linha");
        direction.Label.Should().Be("Orientação");
    }

    [Fact]
    public void Immutable_setter_returns_a_changed_copy_without_mutating_the_original()
    {
        var line = new LineElement { Direction = LineDirection.Horizontal };
        var direction = PropertyGridDescriptors.For(typeof(LineElement)).Single(d => d.Name == "Direction");

        var updated = (LineElement)direction.Set(line, LineDirection.Vertical);

        updated.Direction.Should().Be(LineDirection.Vertical);
        line.Direction.Should().Be(LineDirection.Horizontal, "the original immutable record must be untouched (record `with` semantics)");
        updated.Should().NotBeSameAs(line);
        ((LineDirection)direction.Get(updated)!).Should().Be(LineDirection.Vertical);
    }

    [Fact]
    public void Editor_inference_maps_the_core_types()
    {
        PropertyGridDescriptors.InferEditor(typeof(bool)).Should().Be("toggle");
        PropertyGridDescriptors.InferEditor(typeof(LineDirection)).Should().Be("enum");
        PropertyGridDescriptors.InferEditor(typeof(Color)).Should().Be("color-picker");
        PropertyGridDescriptors.InferEditor(typeof(Color?)).Should().Be("color-picker");
        PropertyGridDescriptors.InferEditor(typeof(Unit)).Should().Be("unit-spinner");
        PropertyGridDescriptors.InferEditor(typeof(double)).Should().Be("number");
        PropertyGridDescriptors.InferEditor(typeof(string)).Should().Be("text");
    }

    // A nested-record property (like the shared Style) flattened into the element grid.
    private sealed record StyledFixture : ReportElement
    {
        [PropertyGrid(Nested = true)]
        public Style Look { get; init; } = Style.Default;
    }

    [Fact]
    public void Nested_property_flattens_into_dotted_path_descriptors()
    {
        var descriptors = PropertyGridDescriptors.For(typeof(StyledFixture));

        var foreColor = descriptors.Should().ContainSingle(d => d.Name == "Look.ForeColor").Subject;
        foreColor.Editor.Should().Be("color-picker", "Style.ForeColor's Color? type infers the colour editor");
        foreColor.Path.Should().Be("Look.ForeColor");
        foreColor.Bindable.Should().BeTrue();
        foreColor.Category.Should().Be("Aparência");
        foreColor.Label.Should().Be("Cor do texto");
        // Font/Border/Padding are NOT annotated, so they don't flatten.
        descriptors.Should().NotContain(d => d.Name == "Look.Font");
    }

    [Fact]
    public void Nested_setter_rebuilds_the_record_chain_immutably()
    {
        var el = new StyledFixture { Look = Style.Default with { ForeColor = Color.Black } };
        var foreColor = PropertyGridDescriptors.For(typeof(StyledFixture)).Single(d => d.Name == "Look.ForeColor");

        var updated = (StyledFixture)foreColor.Set(el, Color.FromHex("#FF0000"));

        updated.Look.ForeColor.Should().Be(Color.FromHex("#FF0000"));
        el.Look.ForeColor.Should().Be(Color.Black, "the original element AND its nested Style must be untouched");
        updated.Should().NotBeSameAs(el);
        updated.Look.Should().NotBeSameAs(el.Look, "the nested record is cloned, not mutated in place");
        ((Color?)foreColor.Get(updated)).Should().Be(Color.FromHex("#FF0000"));
    }
}
