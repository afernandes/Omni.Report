using Reporting.Common;
using Reporting.Metadata;
using Reporting.Styling;

namespace Reporting.Elements;

/// <summary>A literal text label — no expressions, no data binding.</summary>
[TextStyled]
public sealed record LabelElement : ReportElement
{
    /// <summary>The literal text drawn. Not an expression — it is rendered exactly as written.</summary>
    public required string Text { get; init; }
}
