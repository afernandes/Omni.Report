using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Styling;

namespace Reporting.Serialization.Internal;

/// <summary>
/// Convention-based serialization for <see cref="ReportElement"/> subtypes that are NOT hand-wired in the
/// format switches. A new all-scalar element gets repx + repjson round-trip for free: its tag is derived
/// from the type name (minus the <c>Element</c> suffix) and its declared scalar properties are written as
/// child nodes (sparse) / read back via reflection. The hand-wired elements never reach this path — their
/// explicit switch arms take precedence; this only backs the <c>_ =&gt;</c> fallthrough.
///
/// Scope (PR 1): scalar leaf properties only (<c>string</c>/<c>bool</c>/<c>int</c>/<c>long</c>/<c>double</c>/
/// enum/<see cref="Unit"/>/<see cref="Color"/> and their nullable forms). A type with a non-scalar declared
/// property is NOT auto-serializable yet and still throws the original "unsupported" error (nested records /
/// collections / nested elements are a later increment). See docs/serialization-auto-wiring-design.md.
/// </summary>
internal static class ElementSerializationRegistry
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly PropertyInfo BoundsProp = typeof(ReportElement).GetProperty(nameof(ReportElement.Bounds))!;

    /// <summary>Wire names owned by the base envelope. A subtype declaring a property with one of these
    /// names would clash on the wire, so such a type is excluded from the generic path.</summary>
    private static readonly HashSet<string> EnvelopeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Id", "Name", "Bounds", "Visible", "VisibleExpression", "Style", "ConditionalFormats",
        "PropertyExpressions", "Bookmark", "DocumentMapLabel", "Action", "ToggleItemId", "InitiallyHidden",
    };

    /// <summary>One element-specific scalar property, with its wire names (PascalCase for repx, camelCase
    /// for repjson), its value on a default instance (for sparse omission), and whether it is required.</summary>
    internal sealed class Scalar
    {
        public required PropertyInfo Prop { get; init; }
        public required string XmlName { get; init; }
        public required string JsonName { get; init; }
        public required object? Default { get; init; }
        public required bool Required { get; init; }
    }

    private sealed record Schema(string Tag, IReadOnlyList<Scalar> Scalars);

    // null = type is not auto-serializable (hand-wired, has a non-scalar prop, or an envelope-name clash).
    private static readonly ConcurrentDictionary<Type, Schema?> _schemas = new();
    private static readonly Lazy<Dictionary<string, Type>> _tagToType = new(BuildTagMap);

    // ── Write side (has the live element) ────────────────────────────────────────

    /// <summary>The convention tag for an auto-serializable element, else throws (mirrors the original
    /// "unsupported element type" failure of the format switches).</summary>
    public static string TagFor(ReportElement element)
        => SchemaFor(element.GetType())?.Tag
           ?? throw new InvalidOperationException($"Unsupported element type: {element.GetType().Name}");

    public static bool IsAutoSerializable(ReportElement element) => SchemaFor(element.GetType()) is not null;

    /// <summary>The element-specific scalars to emit, sparse: a non-required property equal to its default
    /// is omitted (read back as that default); required properties always emit. <c>null</c> values are
    /// omitted (a missing required value then surfaces on read via post-construction validation). Each
    /// scalar is given both its repx text form and its native repjson node (bool/number stay typed in JSON
    /// for parity with the hand-wired elements).</summary>
    public static IEnumerable<(Scalar Member, string Text, JsonNode Json)> WriteScalars(ReportElement element)
    {
        var schema = SchemaFor(element.GetType())
            ?? throw new InvalidOperationException($"Unsupported element type: {element.GetType().Name}");
        foreach (var m in schema.Scalars)
        {
            var value = m.Prop.GetValue(element);
            if (value is null)
            {
                continue;
            }
            if (!m.Required && Equals(value, m.Default))
            {
                continue;
            }
            var text = ToText(value, m.Prop.PropertyType);
            yield return (m, text, ToJson(value, m.Prop.PropertyType, text));
        }
    }

    // ── Read side (has the tag string) ───────────────────────────────────────────

    public static bool TryGetType(string tag, out Type type) => _tagToType.Value.TryGetValue(tag, out type!);

    /// <summary>Materialise an element of <paramref name="type"/>: construct it, apply the bounds, then set
    /// each scalar whose wire value the <paramref name="lookup"/> returns (absent → keep the record default).
    /// Reflection ignores <c>required</c>/<c>init</c>, so a forgotten required value is caught explicitly.</summary>
    public static ReportElement Construct(Type type, Rectangle bounds, Func<Scalar, string?> lookup)
    {
        var schema = SchemaFor(type)
            ?? throw new FormatException($"No generic serialization schema for '{type.Name}'.");
        var element = (ReportElement)Activator.CreateInstance(type)!;
        BoundsProp.SetValue(element, bounds);
        foreach (var m in schema.Scalars)
        {
            var text = lookup(m);
            if (text is null)
            {
                if (m.Required)
                {
                    throw new FormatException($"Required '{m.XmlName}' missing for element <{schema.Tag}>.");
                }
                continue;
            }
            m.Prop.SetValue(element, FromText(text, m.Prop.PropertyType));
        }
        return element;
    }

    public static IReadOnlyList<Scalar> ScalarsOf(Type type) => SchemaFor(type)?.Scalars ?? Array.Empty<Scalar>();

    // ── Schema discovery ─────────────────────────────────────────────────────────

    private static Schema? SchemaFor(Type type) => _schemas.GetOrAdd(type, BuildSchema);

    private static Schema? BuildSchema(Type type)
    {
        if (!type.IsSubclassOf(typeof(ReportElement)) || type.IsAbstract)
        {
            return null;
        }
        // Only a direct subtype of ReportElement is auto-serializable: DeclaredOnly (below) would silently
        // drop the properties of an intermediate base, so a multi-level hierarchy stays on the hand-wired
        // path until that increment lands.
        if (type.BaseType != typeof(ReportElement))
        {
            return null;
        }
        // The generic reader constructs via the parameterless ctor (records have one unless positional;
        // positional records are a later increment).
        if (type.GetConstructor(Type.EmptyTypes) is null)
        {
            return null;
        }
        var defaultInstance = Activator.CreateInstance(type)!;
        var scalars = new List<Scalar>();
        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (!p.CanRead || !p.CanWrite)
            {
                continue; // computed/get-only — not round-tripped
            }
            if (EnvelopeNames.Contains(p.Name) || !IsSupportedScalar(p.PropertyType))
            {
                return null; // clash with the envelope, or a non-scalar property → defer to the hand-wired path
            }
            scalars.Add(new Scalar
            {
                Prop = p,
                XmlName = p.Name,
                JsonName = char.ToLowerInvariant(p.Name[0]) + p.Name[1..],
                Default = p.GetValue(defaultInstance),
                Required = p.IsDefined(typeof(RequiredMemberAttribute), inherit: false),
            });
        }
        return new Schema(ConventionTag(type), scalars);
    }

    private static string ConventionTag(Type type)
    {
        var name = type.Name;
        return name.EndsWith("Element", StringComparison.Ordinal) ? name[..^"Element".Length] : name;
    }

    private static Dictionary<string, Type> BuildTagMap()
    {
        var coreAsm = typeof(ReportElement).Assembly;
        var coreName = coreAsm.GetName().Name;
        var map = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm != coreAsm && !asm.GetReferencedAssemblies().Any(r => r.Name == coreName))
            {
                continue;
            }
            Type[] types;
            try
            {
                types = asm.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t is not null).ToArray()!;
            }
            foreach (var t in types)
            {
                if (t is null || !t.IsSubclassOf(typeof(ReportElement)) || t.IsAbstract)
                {
                    continue;
                }
                if (SchemaFor(t) is { } schema && !map.TryAdd(schema.Tag, t) && map[schema.Tag] != t)
                {
                    // Two distinct types deriving the same convention tag would resolve non-deterministically
                    // by assembly load order — fail loudly so one is given a distinct type name.
                    throw new InvalidOperationException(
                        $"Serialization tag '{schema.Tag}' is claimed by both '{map[schema.Tag].Name}' and " +
                        $"'{t.Name}'. Rename one element type so their tags differ.");
                }
            }
        }
        return map;
    }

    // ── Scalar leaf conversion ───────────────────────────────────────────────────

    private static bool IsSupportedScalar(Type t)
    {
        var u = Nullable.GetUnderlyingType(t) ?? t;
        return u.IsEnum
            || u == typeof(string) || u == typeof(bool) || u == typeof(int) || u == typeof(long)
            || u == typeof(double) || u == typeof(Unit) || u == typeof(Color);
    }

    private static string ToText(object value, Type declaredType)
    {
        var u = Nullable.GetUnderlyingType(declaredType) ?? declaredType;
        if (u == typeof(string)) return (string)value;
        if (u == typeof(bool)) return (bool)value ? "true" : "false";
        if (u == typeof(int)) return ((int)value).ToString(Inv);
        if (u == typeof(long)) return ((long)value).ToString(Inv);
        if (u == typeof(double)) return ((double)value).ToString("R", Inv);
        if (u.IsEnum) return value.ToString()!;
        if (u == typeof(Unit)) return Formats.FormatUnit((Unit)value);
        if (u == typeof(Color)) return Formats.FormatColor((Color)value);
        throw new InvalidOperationException($"No scalar converter for {declaredType}.");
    }

    // Native JSON node for a scalar — bool/number stay typed (parity with hand-wired elements); other
    // leaf types reuse the repx text form. The repjson reader reads any of these back via JsonNode.ToString().
    private static JsonNode ToJson(object value, Type declaredType, string text)
    {
        var u = Nullable.GetUnderlyingType(declaredType) ?? declaredType;
        if (u == typeof(bool)) return JsonValue.Create((bool)value);
        if (u == typeof(int)) return JsonValue.Create((int)value);
        if (u == typeof(long)) return JsonValue.Create((long)value);
        if (u == typeof(double)) return JsonValue.Create((double)value);
        return JsonValue.Create(text)!;
    }

    private static object FromText(string text, Type declaredType)
    {
        var u = Nullable.GetUnderlyingType(declaredType) ?? declaredType;
        if (u == typeof(string)) return text;
        if (u == typeof(bool)) return bool.Parse(text);
        if (u == typeof(int)) return int.Parse(text, Inv);
        if (u == typeof(long)) return long.Parse(text, Inv);
        if (u == typeof(double)) return double.Parse(text, NumberStyles.Any, Inv);
        if (u.IsEnum) return Enum.Parse(u, text);
        if (u == typeof(Unit)) return Formats.ParseUnit(text);
        if (u == typeof(Color)) return Formats.ParseColor(text);
        throw new InvalidOperationException($"No scalar converter for {declaredType}.");
    }
}
