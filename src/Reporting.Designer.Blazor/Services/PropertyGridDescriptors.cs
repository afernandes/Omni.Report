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
    Func<ReportElement, object?> Get,
    Func<ReportElement, object?, ReportElement> Set,
    bool Bindable = false,
    string? PropertyPath = null)
{
    /// <summary>The dotted path used to bind this property to an expression in
    /// <see cref="ReportElement.PropertyExpressions"/>. Equals <see cref="Name"/> for a direct property
    /// (e.g. <c>"Direction"</c>, <c>"FillColor"</c>); a nested-record property would carry a dotted path
    /// (e.g. <c>"Style.ForeColor"</c>).</summary>
    public string Path => PropertyPath ?? Name;
}

/// <summary>
/// Discovers the <see cref="PropertyGridAttribute"/>-annotated properties of a
/// <see cref="ReportElement"/> type via reflection — which naturally includes properties
/// <b>inherited</b> from base records — picks an editor by the property's <b>type</b>, and builds an
/// immutable setter equivalent to <c>element with { Prop = value }</c>. Results are cached per type.
/// </summary>
/// <remarks>
/// This reads metadata only. It never participates in code-first / low-level authoring, rendering, or
/// serialization — those keep working untouched whether or not an element is annotated.
/// </remarks>
public static class PropertyGridDescriptors
{
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<PropertyGridDescriptor>> Cache = new();

    /// <summary>Returns the editable descriptors for an element type (base + derived properties),
    /// grouped-ready (ordered by <c>Order</c> then label). Cached.</summary>
    public static IReadOnlyList<PropertyGridDescriptor> For(Type elementType)
        => Cache.GetOrAdd(elementType, Build);

    private static IReadOnlyList<PropertyGridDescriptor> Build(Type type)
    {
        var list = new List<PropertyGridDescriptor>();
        // GetProperties on the concrete type already returns BASE + DERIVED properties — this is what
        // makes inheritance automatic: a derived element shows its base's [PropertyGrid] editors.
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var attr = prop.GetCustomAttribute<PropertyGridAttribute>();
            if (attr is null || attr.Nested)
            {
                continue; // [Nested] flattening of e.g. Style is a later phase
            }
            list.Add(new PropertyGridDescriptor(
                Name: prop.Name,
                Label: attr.Label ?? prop.Name,
                Placeholder: attr.Placeholder,
                Type: prop.PropertyType,
                Editor: attr.Editor ?? InferEditor(prop.PropertyType),
                Category: attr.Category ?? "Geral",
                Order: attr.Order,
                Get: prop.GetValue,
                Set: BuildSetter(type, prop),
                Bindable: attr.Bindable,
                PropertyPath: prop.Name));
        }
        return list.OrderBy(d => d.Order).ThenBy(d => d.Label, StringComparer.Ordinal).ToList();
    }

    /// <summary>Maps a property's CLR type to a default editor id. An explicit
    /// <c>[PropertyGrid(Editor=…)]</c> overrides this.</summary>
    public static string InferEditor(Type t)
    {
        var u = Nullable.GetUnderlyingType(t) ?? t;
        if (u == typeof(bool)) return "toggle";
        if (u.IsEnum) return "enum";
        if (u == typeof(Color)) return "color-picker";
        if (u == typeof(Unit)) return "unit-spinner";
        if (u == typeof(int) || u == typeof(long) || u == typeof(double) || u == typeof(decimal) || u == typeof(float))
        {
            return "number";
        }
        if (u.IsGenericType && u.GetGenericTypeDefinition() == typeof(EquatableArray<>))
        {
            return "list";
        }
        return "text";
    }

    /// <summary>Builds an immutable setter using the record's synthesized <c>&lt;Clone&gt;$</c> method:
    /// clones the element (so the original is untouched, preserving value semantics) and sets the
    /// init-only property on the copy. Equivalent to <c>element with { Prop = value }</c>, generically.
    /// (Setting an <c>init</c>-only property via reflection is allowed — <c>init</c> is a compile-time
    /// constraint, not a runtime one — and here it targets a fresh copy, never the original.)</summary>
    private static Func<ReportElement, object?, ReportElement> BuildSetter(Type type, PropertyInfo prop)
    {
        var clone = type.GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException($"{type} is not a record (no <Clone>$ method).");
        return (element, value) =>
        {
            var copy = (ReportElement)clone.Invoke(element, null)!;
            prop.SetValue(copy, value);
            return copy;
        };
    }
}
