namespace Reporting.Metadata;

/// <summary>
/// Marks a property of a <see cref="Reporting.Elements.ReportElement"/> (or of a nested record such as
/// its <c>Style</c>) as editable in the visual designer's PropertyGrid.
/// </summary>
/// <remarks>
/// <para>A metadata-driven grid discovers these via reflection — which naturally includes properties
/// <b>inherited</b> from base records — so a new element type that derives from an existing one shows
/// the base's editors automatically, with the editor chosen by the property's <b>type</b>. This
/// replaces the hand-coded <c>@if (Kind == X)</c> / <c>HasTextContent</c> branches in the designer.</para>
/// <para><b>Code-first / low-level authoring is unaffected</b>: this attribute is pure metadata, read
/// only by the designer. It never participates in construction, rendering, or serialization.</para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class PropertyGridAttribute : Attribute
{
    /// <summary>Section the property is grouped under (e.g. <c>"Forma"</c>, <c>"Appearance"</c>).
    /// Defaults to <c>"Geral"</c> when unset.</summary>
    public string? Category { get; init; }

    /// <summary>Sort order within the category (ascending). Defaults to 0.</summary>
    public int Order { get; init; }

    /// <summary>Explicit editor id, overriding the type-based inference. Known ids:
    /// <c>"text"</c>, <c>"textarea"</c>, <c>"toggle"</c>, <c>"enum"</c>, <c>"color-picker"</c>,
    /// <c>"unit-spinner"</c>, <c>"number"</c>, <c>"list"</c>.</summary>
    public string? Editor { get; init; }

    /// <summary>Display label for the row. Defaults to the property name when unset.</summary>
    public string? Label { get; init; }

    /// <summary>Placeholder/hint text shown in the editor when empty.</summary>
    public string? Placeholder { get; init; }

    /// <summary>When true, the property's own type is a nested record whose <see cref="PropertyGridAttribute"/>
    /// properties are flattened into this element's grid (e.g. the shared <c>Style</c>), instead of the
    /// property itself getting a single editor.</summary>
    public bool Nested { get; init; }
}
