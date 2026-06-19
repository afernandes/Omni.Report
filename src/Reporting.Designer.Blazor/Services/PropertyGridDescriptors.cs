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
        Collect(type, chain: [], prefix: null, list);
        // A text element flattens the SHARED Style's appearance props ("Style.ForeColor", "Style.Font", …)
        // into its grid. Style lives on the base element (for code-first/low-level), so rather than mark it
        // [Nested] globally — which would show font/alignment on shapes too — we opt in per type via
        // [TextStyled] (Inherited: a derived text element gets appearance automatically).
        if (type.GetCustomAttribute<TextStyledAttribute>(inherit: true) is not null
            && type.GetProperty(nameof(ReportElement.Style), BindingFlags.Public | BindingFlags.Instance) is { } styleProp)
        {
            Collect(styleProp.PropertyType, chain: [styleProp], prefix: styleProp.Name, list);
        }
        return list.OrderBy(d => d.Order).ThenBy(d => d.Label, StringComparer.Ordinal).ToList();
    }

    /// <summary>Recursively collects descriptors. <c>GetProperties</c> on a concrete type already returns
    /// BASE + DERIVED properties — that's what makes inheritance automatic. A <c>[PropertyGrid(Nested)]</c>
    /// property (e.g. the shared <c>Style</c>) is FLATTENED: its own annotated properties become rows with
    /// a dotted path (<c>"Style.ForeColor"</c>), so a single grid edits the nested record immutably.</summary>
    private static void Collect(Type ownerType, IReadOnlyList<PropertyInfo> chain, string? prefix, List<PropertyGridDescriptor> list)
    {
        foreach (var prop in ownerType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var attr = prop.GetCustomAttribute<PropertyGridAttribute>();
            if (attr is null)
            {
                continue;
            }
            var path = prefix is null ? prop.Name : $"{prefix}.{prop.Name}";
            var nextChain = new List<PropertyInfo>(chain) { prop };
            if (attr.Nested)
            {
                Collect(prop.PropertyType, nextChain, path, list); // flatten the nested record's [PropertyGrid] props
                continue;
            }
            list.Add(new PropertyGridDescriptor(
                Name: path,
                Label: attr.Label ?? prop.Name,
                Placeholder: attr.Placeholder,
                Type: prop.PropertyType,
                Editor: attr.Editor ?? InferEditor(prop.PropertyType),
                Category: attr.Category ?? "Geral",
                Order: attr.Order,
                Get: BuildGetter(nextChain),
                Set: BuildSetter(nextChain),
                Bindable: attr.Bindable,
                PropertyPath: path));
        }
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

    /// <summary>Reads the value at the end of a property <paramref name="chain"/> (e.g.
    /// <c>element → Style → ForeColor</c>), returning null if any record on the way is null.</summary>
    private static Func<ReportElement, object?> BuildGetter(IReadOnlyList<PropertyInfo> chain)
        => element =>
        {
            object? current = element;
            foreach (var prop in chain)
            {
                if (current is null)
                {
                    return null;
                }
                current = prop.GetValue(current);
            }
            return current;
        };

    /// <summary>Builds an immutable setter over a property <paramref name="chain"/> using each record's
    /// synthesized <c>&lt;Clone&gt;$</c>: it rebuilds the chain bottom-up (clone the leaf's owner, set the
    /// property; clone its owner, point it at the new child; …) so the original element — and every record
    /// on the path — is untouched. Equivalent to <c>element with { Style = element.Style with { ForeColor
    /// = value } }</c>, generically. (Setting an <c>init</c>-only property via reflection is allowed —
    /// <c>init</c> is a compile-time constraint — and here it targets a fresh copy, never the original.)</summary>
    private static Func<ReportElement, object?, ReportElement> BuildSetter(IReadOnlyList<PropertyInfo> chain)
        => (element, value) => (ReportElement)SetChain(element, chain, 0, value)!;

    private static object? SetChain(object obj, IReadOnlyList<PropertyInfo> chain, int index, object? leaf)
    {
        var copy = CloneMethod(obj.GetType()).Invoke(obj, null)!;
        if (index == chain.Count - 1)
        {
            chain[index].SetValue(copy, leaf);
            return copy;
        }
        var child = chain[index].GetValue(obj);
        if (child is null)
        {
            return obj; // can't navigate into a null nested record — leave the element unchanged
        }
        chain[index].SetValue(copy, SetChain(child, chain, index + 1, leaf));
        return copy;
    }

    private static readonly ConcurrentDictionary<Type, MethodInfo> CloneCache = new();

    private static MethodInfo CloneMethod(Type type) => CloneCache.GetOrAdd(type, t =>
        t.GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException($"{t} is not a record (no <Clone>$ method)."));
}
