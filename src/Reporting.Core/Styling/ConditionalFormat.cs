namespace Reporting.Styling;

/// <summary>Conditional format rule. When <see cref="Condition"/> evaluates to <c>true</c>
/// for the current row context, <see cref="Style"/> is layered over the element's base style.</summary>
public sealed record ConditionalFormat(string Condition, Style Style);
