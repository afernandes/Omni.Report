namespace Reporting.Metadata;

/// <summary>
/// Marks a <see cref="Reporting.Elements.ReportElement"/> type as rendering TEXT, so the designer
/// flattens the shared <c>Style</c>'s appearance properties (font, colour, alignment, …) into the
/// element's metadata grid. <see cref="AttributeUsageAttribute.Inherited"/> is true, so a new element
/// that derives from a text element (e.g. <c>MyHeading : TextBoxElement</c>) shows the appearance
/// editors automatically — no hand-coded UI and no kind-list to maintain.
/// </summary>
/// <remarks>This is metadata only, read by the designer. It does not affect construction, rendering, or
/// serialization, and the shared <c>Style</c> stays on the base element for code-first/low-level use.</remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class TextStyledAttribute : Attribute
{
}
