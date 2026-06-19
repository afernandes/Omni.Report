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

    // The whole point of the metadata approach: a new element that DERIVES from another reuses the
    // base's editors automatically (reflection walks the type hierarchy) and adds its own.
    private record BaseWidget : ReportElement
    {
        [PropertyGrid(Category = "Base", Order = 1, Label = "Comum", Bindable = true)]
        public Color? Common { get; init; }
    }

    private sealed record DerivedWidget : BaseWidget
    {
        [PropertyGrid(Category = "Derivado", Order = 2, Label = "Extra")]
        public LineDirection Extra { get; init; }
    }

    [Fact]
    public void A_derived_element_inherits_the_base_editors_plus_its_own()
    {
        var descriptors = PropertyGridDescriptors.For(typeof(DerivedWidget));

        // Inherited from BaseWidget with ZERO extra code on the derived type — the core promise.
        var common = descriptors.Should().ContainSingle(d => d.Name == "Common").Subject;
        common.Editor.Should().Be("color-picker");
        common.Bindable.Should().BeTrue();
        common.Category.Should().Be("Base");

        // Declared on DerivedWidget, with the editor inferred from its own type.
        var extra = descriptors.Should().ContainSingle(d => d.Name == "Extra").Subject;
        extra.Editor.Should().Be("enum");

        // And the immutable setter for an INHERITED property still works on the derived record.
        var widget = new DerivedWidget();
        var updated = (DerivedWidget)common.Set(widget, Color.FromHex("#ABCDEF"));
        updated.Common.Should().Be(Color.FromHex("#ABCDEF"));
        widget.Common.Should().BeNull("the original is untouched");
    }

    [Fact]
    public void Map_properties_flatten_with_their_inferred_and_explicit_editors()
    {
        var map = PropertyGridDescriptors.For(typeof(MapElement));
        map.Should().Contain(d => d.Name == "Basemap" && d.Editor == "text");
        map.Should().Contain(d => d.Name == "ShowGraticule" && d.Editor == "toggle");
        map.Should().Contain(d => d.Name == "ShapesGeoJson" && d.Editor == "textarea");
        map.Should().Contain(d => d.Name == "ShapeFill" && d.Editor == "color-hex");
    }

    [Fact]
    public void Databar_and_sparkline_scalars_flatten_with_their_editors()
    {
        var bar = PropertyGridDescriptors.For(typeof(DataBarElement));
        bar.Should().Contain(d => d.Name == "ValueExpression" && d.Editor == "text");
        bar.Should().Contain(d => d.Name == "FillColor" && d.Editor == "color-hex");

        PropertyGridDescriptors.For(typeof(SparklineElement))
            .Should().Contain(d => d.Name == "Kind" && d.Editor == "enum");
    }

    [Fact]
    public void Descriptors_work_on_non_report_element_list_items()
    {
        // The descriptor machinery is generalized to any record, so a list item (GaugeRange, not a
        // ReportElement) can be discovered and edited immutably — this is what powers the list editor.
        var fields = PropertyGridDescriptors.For(typeof(GaugeRange));
        fields.Should().ContainSingle(d => d.Name == "ColorHex").Which.Editor.Should().Be("color-hex");

        var color = fields.Single(d => d.Name == "ColorHex");
        var range = new GaugeRange("0", "50", "#16A34A");
        var updated = (GaugeRange)color.Set(range, "#FF0000");
        updated.ColorHex.Should().Be("#FF0000");
        range.ColorHex.Should().Be("#16A34A", "the original list item is untouched");
    }

    [Fact]
    public void Indicator_flattens_scalars_plus_its_states_list()
    {
        var ind = PropertyGridDescriptors.For(typeof(IndicatorElement));
        ind.Should().Contain(d => d.Name == "Kind" && d.Editor == "enum");
        ind.Should().Contain(d => d.Name == "States" && d.Editor == "list");
        PropertyGridDescriptors.For(typeof(IndicatorState)).Should().Contain(d => d.Name == "IconName");
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
        // Complex records flatten with their custom editors (font/border).
        descriptors.Should().ContainSingle(d => d.Name == "Look.Font").Which.Editor.Should().Be("font");
        descriptors.Should().ContainSingle(d => d.Name == "Look.Border").Which.Editor.Should().Be("border");
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

    [Fact]
    public void Text_elements_flatten_the_shared_style_appearance_via_TextStyled()
    {
        var text = PropertyGridDescriptors.For(typeof(TextBoxElement));
        text.Should().ContainSingle(d => d.Name == "Style.Font").Which.Editor.Should().Be("font");
        text.Should().Contain(d => d.Name == "Style.ForeColor" && d.Editor == "color-picker");
        text.Should().Contain(d => d.Name == "Style.HorizontalAlignment" && d.Editor == "h-align");
        text.Should().Contain(d => d.Name == "Style.Border" && d.Editor == "border");
        text.Should().Contain(d => d.Name == "Style.Padding" && d.Editor == "padding");

        // A shape is NOT [TextStyled] → its Style appearance is NOT flattened (font/alignment would be noise).
        PropertyGridDescriptors.For(typeof(RectangleElement)).Should().NotContain(d => d.Name == "Style.Font");
    }

    [Fact]
    public void Editing_a_flattened_style_font_rebuilds_element_then_style_then_font_immutably()
    {
        var tb = new TextBoxElement { Expression = "x", Style = Style.Default with { Font = new Font("Arial", 10) } };
        var fontDesc = PropertyGridDescriptors.For(typeof(TextBoxElement)).Single(d => d.Name == "Style.Font");

        var updated = (TextBoxElement)fontDesc.Set(tb, new Font("Calibri", 14, FontStyle.Bold));

        updated.Style.Font!.Family.Should().Be("Calibri");
        updated.Style.Font.Size.Should().Be(14);
        tb.Style.Font!.Family.Should().Be("Arial", "the original element, its Style and its Font are all untouched");
    }
}
