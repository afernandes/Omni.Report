using Reporting.Rendering;

namespace Reporting.Layout.Primitives;

/// <summary>Draws a run of text within <see cref="LayoutPrimitive.Bounds"/> using the given style.</summary>
public sealed record DrawTextPrimitive : LayoutPrimitive
{
    /// <summary>The literal text to draw. Already resolved — expressions and formatting ran during layout,
    /// so a backend never evaluates anything.</summary>
    public required string Text { get; init; }

    /// <summary>Original scalar value before display formatting. Null falls back to literal Text.</summary>
    public object? SemanticValue { get; init; }

    /// <summary>Explicit spreadsheet formula supplied by a trusted producer. Never inferred from labels.</summary>
    public string? SpreadsheetFormula { get; init; }

    /// <summary>Font, colour, alignment and wrapping to draw it with.</summary>
    public required TextStyle Style { get; init; }
}
