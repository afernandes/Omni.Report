using Reporting.Geometry;

namespace Reporting.Styling;

/// <summary>Visual style applied to a <c>ReportElement</c>. All properties are optional —
/// unset (null) means "inherit from parent or use renderer default".</summary>
public sealed record Style(
    Font? Font = null,
    Color? ForeColor = null,
    Color? BackColor = null,
    Border? Border = null,
    Thickness? Padding = null,
    HorizontalAlignment HorizontalAlignment = HorizontalAlignment.Left,
    VerticalAlignment VerticalAlignment = VerticalAlignment.Top,
    bool WordWrap = true,
    string? Format = null)
{
    public static readonly Style Default = new();
}
