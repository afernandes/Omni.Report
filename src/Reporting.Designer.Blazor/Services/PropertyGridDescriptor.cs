using System.Collections.Concurrent;
using System.Reflection;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Metadata;
using Reporting.Styling;

namespace Reporting.Designer.Blazor.Services;

/// <summary>One editable property surfaced to the metadata-driven PropertyGrid. <c>Get</c> reads the
/// current value; <c>Set</c> returns a NEW element with the property changed (immutable
/// <c>record with</c>) without mutating the input.</summary>
public sealed record PropertyGridDescriptor(
    string Name,
    string Label,
    string? Placeholder,
    Type Type,
    string Editor,
    string Category,
    int Order,
    Func<object, object?> Get,
    Func<object, object?, object> Set,
    bool Bindable = false,
    string? PropertyPath = null)
{
    /// <summary>The dotted path used to bind this property to an expression in
    /// <see cref="ReportElement.PropertyExpressions"/>. Equals <see cref="Name"/> for a direct property
    /// (e.g. <c>"Direction"</c>, <c>"FillColor"</c>); a nested-record property would carry a dotted path
    /// (e.g. <c>"Style.ForeColor"</c>).</summary>
    public string Path => PropertyPath ?? Name;

    /// <summary>Whether the property can hold null — a nullable value type (e.g. <c>Color?</c>) or any
    /// reference type. Editors use it to offer a "clear" affordance back to null (e.g. a chart series
    /// colour reverting to the auto palette).</summary>
    public bool IsNullable => Nullable.GetUnderlyingType(Type) is not null || !Type.IsValueType;
}
