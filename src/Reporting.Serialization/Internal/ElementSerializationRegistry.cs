using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Styling;

namespace Reporting.Serialization.Internal;

/// <summary>
/// Convention-based serialization for <see cref="ReportElement"/> subtypes that are NOT hand-wired in the
/// format switches. A new element gets repx + repjson round-trip for free: its tag is derived from the type
/// name (minus the <c>Element</c> suffix) and its declared properties are serialized recursively. The
/// hand-wired elements never reach this path — their explicit switch arms take precedence; this only backs
/// the <c>_ =&gt;</c> fallthrough.
///
/// Supported member shapes (recursive): scalar leaves (<c>string</c>/<c>bool</c>/<c>int</c>/<c>long</c>/
/// <c>double</c>/enum/<see cref="Unit"/>/<see cref="Color"/> and nullable forms), nested records (init or
/// positional — built via constructor-parameter matching), and <see cref="EquatableArray{T}"/> collections
/// (T scalar or record). A NESTED <see cref="ReportElement"/> (e.g. a cell holding an element) is the one
/// remaining shape not yet covered and keeps its type on the hand-wired path. See
/// docs/serialization-auto-wiring-design.md.
/// </summary>
internal static class ElementSerializationRegistry
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly PropertyInfo BoundsProp = typeof(ReportElement).GetProperty(nameof(ReportElement.Bounds))!;
    private const string ItemTag = "Item"; // repx wrapper for a collection element

    /// <summary>Wire names owned by the base envelope. A subtype declaring a property with one of these
    /// names would clash on the wire, so such a type is excluded from the generic path.</summary>
    private static readonly HashSet<string> EnvelopeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Id", "Name", "Bounds", "Visible", "VisibleExpression", "Style", "ConditionalFormats",
        "PropertyExpressions", "Bookmark", "DocumentMapLabel", "Action", "ToggleItemId", "InitiallyHidden",
    };

    /// <summary>One element-specific property, with its wire names (PascalCase for repx, camelCase for
    /// repjson), its value on a default instance (for sparse omission), and whether it is required.</summary>
    internal sealed class Member
    {
        public required PropertyInfo Prop { get; init; }
        public required string XmlName { get; init; }
        public required string JsonName { get; init; }
        public required object? Default { get; init; }
        public required bool Required { get; init; }
    }

    private sealed record Schema(string Tag, IReadOnlyList<Member> Members);

    // null = type is not auto-serializable (hand-wired, an unsupported member shape, or an envelope clash).
    private static readonly ConcurrentDictionary<Type, Schema?> _schemas = new();
    private static readonly Lazy<Dictionary<string, Type>> _tagToType = new(BuildTagMap);

    // ── Tag dispatch ─────────────────────────────────────────────────────────────

    /// <summary>The convention tag for an auto-serializable element, else throws (mirrors the original
    /// "unsupported element type" failure of the format switches).</summary>
    public static string TagFor(ReportElement element)
        => SchemaFor(element.GetType())?.Tag
           ?? throw new InvalidOperationException($"Unsupported element type: {element.GetType().Name}");

    public static bool TryGetType(string tag, out Type type) => _tagToType.Value.TryGetValue(tag, out type!);

    // ── Write ────────────────────────────────────────────────────────────────────

    /// <summary>The element-specific member nodes for repx, sparse (a non-required member equal to its
    /// default is omitted, read back as that default; <c>null</c> is always omitted).</summary>
    public static IEnumerable<XElement> WriteXml(ReportElement element)
    {
        foreach (var (m, value) in EmittedMembers(element))
        {
            var node = new XElement(m.XmlName);
            WriteValueXml(node, value, m.Prop.PropertyType);
            yield return node;
        }
    }

    /// <summary>The element-specific members for repjson, same sparse policy, with native JSON nodes.</summary>
    public static IEnumerable<KeyValuePair<string, JsonNode>> WriteJson(ReportElement element)
    {
        foreach (var (m, value) in EmittedMembers(element))
        {
            yield return new(m.JsonName, WriteValueJson(value, m.Prop.PropertyType));
        }
    }

    private static IEnumerable<(Member Member, object Value)> EmittedMembers(ReportElement element)
    {
        var schema = SchemaFor(element.GetType())
            ?? throw new InvalidOperationException($"Unsupported element type: {element.GetType().Name}");
        foreach (var m in schema.Members)
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
            yield return (m, value);
        }
    }

    // ── Read ─────────────────────────────────────────────────────────────────────

    /// <summary>Materialise an element from its repx node: construct it, apply the bounds, then read each
    /// member sub-tree (absent → keep the record default). A missing required member throws.</summary>
    public static ReportElement ReadXml(Type type, Rectangle bounds, XElement source)
        => ReadElement(type, bounds, m => source.Element(m.XmlName) is { } child ? () => ReadValueXml(child, m.Prop.PropertyType) : null);

    /// <summary>Materialise an element from its repjson object.</summary>
    public static ReportElement ReadJson(Type type, Rectangle bounds, JsonObject source)
        => ReadElement(type, bounds, m => source[m.JsonName] is { } node ? () => ReadValueJson(node, m.Prop.PropertyType) : null);

    private static ReportElement ReadElement(Type type, Rectangle bounds, Func<Member, Func<object>?> reader)
    {
        var schema = SchemaFor(type)
            ?? throw new FormatException($"No generic serialization schema for '{type.Name}'.");
        var element = (ReportElement)Activator.CreateInstance(type)!;
        BoundsProp.SetValue(element, bounds);
        foreach (var m in schema.Members)
        {
            var read = reader(m);
            if (read is null)
            {
                if (m.Required)
                {
                    throw new FormatException($"Required '{m.XmlName}' missing for element <{schema.Tag}>.");
                }
                continue; // keep the record default
            }
            m.Prop.SetValue(element, read());
        }
        return element;
    }

    // ── Recursive value (de)serialization ────────────────────────────────────────

    private static void WriteValueXml(XElement target, object value, Type type)
    {
        if (IsScalar(type))
        {
            target.Add(ToText(value, type));
        }
        else if (IsEquatableArray(type, out var elem))
        {
            foreach (var item in (IEnumerable)value)
            {
                if (item is null)
                {
                    continue;
                }
                var itemNode = new XElement(ItemTag);
                WriteValueXml(itemNode, item, elem);
                target.Add(itemNode);
            }
        }
        else // nested record
        {
            foreach (var p in RecordProps(type))
            {
                var pv = p.GetValue(value);
                if (pv is null)
                {
                    continue;
                }
                var child = new XElement(p.Name);
                WriteValueXml(child, pv, p.PropertyType);
                target.Add(child);
            }
        }
    }

    private static object ReadValueXml(XElement source, Type type)
    {
        if (IsScalar(type))
        {
            return FromText(source.Value, type);
        }
        if (IsEquatableArray(type, out var elem))
        {
            var items = source.Elements(ItemTag).Select(i => ReadValueXml(i, elem)).ToList();
            return BuildEquatableArray(elem, items);
        }
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in RecordProps(type))
        {
            if (source.Element(p.Name) is { } child)
            {
                values[p.Name] = ReadValueXml(child, p.PropertyType);
            }
        }
        return ConstructRecord(type, values);
    }

    private static JsonNode WriteValueJson(object value, Type type)
    {
        if (IsScalar(type))
        {
            return ToJson(value, type, ToText(value, type));
        }
        if (IsEquatableArray(type, out var elem))
        {
            var arr = new JsonArray();
            foreach (var item in (IEnumerable)value)
            {
                if (item is not null)
                {
                    arr.Add(WriteValueJson(item, elem));
                }
            }
            return arr;
        }
        var o = new JsonObject();
        foreach (var p in RecordProps(type))
        {
            var pv = p.GetValue(value);
            if (pv is not null)
            {
                o[char.ToLowerInvariant(p.Name[0]) + p.Name[1..]] = WriteValueJson(pv, p.PropertyType);
            }
        }
        return o;
    }

    private static object ReadValueJson(JsonNode node, Type type)
    {
        if (IsScalar(type))
        {
            return FromText(node.ToString(), type);
        }
        if (IsEquatableArray(type, out var elem))
        {
            var items = node.AsArray().Where(n => n is not null).Select(n => ReadValueJson(n!, elem)).ToList();
            return BuildEquatableArray(elem, items);
        }
        var obj = node.AsObject();
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in RecordProps(type))
        {
            if (obj[char.ToLowerInvariant(p.Name[0]) + p.Name[1..]] is { } child)
            {
                values[p.Name] = ReadValueJson(child, p.PropertyType);
            }
        }
        return ConstructRecord(type, values);
    }

    // ── Record construction (init or positional) ─────────────────────────────────

    private static object ConstructRecord(Type type, IReadOnlyDictionary<string, object?> values)
    {
        var ctor = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
        var args = ctor.GetParameters()
            .Select(p => values.TryGetValue(p.Name!, out var v) ? v
                       : p.HasDefaultValue ? p.DefaultValue
                       : DefaultOf(p.ParameterType))
            .ToArray();
        var obj = ctor.Invoke(args)!;
        var ctorNames = ctor.GetParameters().Select(p => p.Name!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var p in RecordProps(type))
        {
            if (!ctorNames.Contains(p.Name) && values.TryGetValue(p.Name, out var v))
            {
                p.SetValue(obj, v);
            }
        }
        return obj;
    }

    private static object BuildEquatableArray(Type elem, IReadOnlyList<object> items)
    {
        var typed = Array.CreateInstance(elem, items.Count);
        for (int i = 0; i < items.Count; i++)
        {
            typed.SetValue(items[i], i);
        }
        return Activator.CreateInstance(typeof(EquatableArray<>).MakeGenericType(elem), new object[] { typed })!;
    }

    private static object? DefaultOf(Type t) => t.IsValueType ? Activator.CreateInstance(t) : null;

    private static IEnumerable<PropertyInfo> RecordProps(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite);

    // ── Schema discovery ─────────────────────────────────────────────────────────

    private static Schema? SchemaFor(Type type) => _schemas.GetOrAdd(type, BuildSchema);

    private static Schema? BuildSchema(Type type)
    {
        if (!type.IsSubclassOf(typeof(ReportElement)) || type.IsAbstract || type.BaseType != typeof(ReportElement))
        {
            return null; // not a direct, concrete ReportElement subtype (DeclaredOnly would drop intermediate props)
        }
        if (type.GetConstructor(Type.EmptyTypes) is null)
        {
            return null; // the element is built via the parameterless ctor (positional elements: a later increment)
        }
        var defaultInstance = Activator.CreateInstance(type)!;
        var members = new List<Member>();
        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (!p.CanRead || !p.CanWrite)
            {
                continue; // computed/get-only — not round-tripped
            }
            if (EnvelopeNames.Contains(p.Name) || !IsSerializable(p.PropertyType, new HashSet<Type>()))
            {
                return null; // clash with the envelope, or an unsupported member shape → defer to the hand-wired path
            }
            members.Add(new Member
            {
                Prop = p,
                XmlName = p.Name,
                JsonName = char.ToLowerInvariant(p.Name[0]) + p.Name[1..],
                Default = p.GetValue(defaultInstance),
                Required = p.IsDefined(typeof(RequiredMemberAttribute), inherit: false),
            });
        }
        return new Schema(ConventionTag(type), members);
    }

    // A member type the recursive (de)serializer can handle: a scalar, an EquatableArray of one, or a
    // constructible record whose own members are all serializable. A nested ReportElement is intentionally
    // rejected (kept on the hand-wired path for now).
    private static bool IsSerializable(Type t, HashSet<Type> visiting)
    {
        if (IsScalar(t))
        {
            return true;
        }
        if (IsEquatableArray(t, out var elem))
        {
            return IsSerializable(elem, visiting);
        }
        if (typeof(ReportElement).IsAssignableFrom(t) || t.IsAbstract || t.IsInterface || t.IsArray || t.IsPrimitive)
        {
            return false;
        }
        // Any other collection shape (dictionaries, lists, …) is unsupported and must NOT slip through the
        // record branch: e.g. EquatableDictionary exposes only get-only members, so RecordProps is empty and
        // All(...) would be vacuously true — silently dropping the data. Reject it outright.
        if (typeof(IEnumerable).IsAssignableFrom(t))
        {
            return false;
        }
        if (!visiting.Add(t))
        {
            return true; // recursive type — assume serializable to break the cycle
        }
        try
        {
            // A round-trippable record needs a public ctor AND at least one writable property whose own type
            // is serializable. The Count > 0 guard is what stops a zero-writable-property type passing vacuously.
            var props = RecordProps(t).ToList();
            return t.GetConstructors().Length > 0 && props.Count > 0
                && props.All(p => IsSerializable(p.PropertyType, visiting));
        }
        finally
        {
            visiting.Remove(t);
        }
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
                    throw new InvalidOperationException(
                        $"Serialization tag '{schema.Tag}' is claimed by both '{map[schema.Tag].Name}' and " +
                        $"'{t.Name}'. Rename one element type so their tags differ.");
                }
            }
        }
        return map;
    }

    // ── Scalar leaf conversion ───────────────────────────────────────────────────

    private static bool IsScalar(Type t)
    {
        var u = Nullable.GetUnderlyingType(t) ?? t;
        return u.IsEnum
            || u == typeof(string) || u == typeof(bool) || u == typeof(int) || u == typeof(long)
            || u == typeof(double) || u == typeof(Unit) || u == typeof(Color);
    }

    private static bool IsEquatableArray(Type t, out Type elem)
    {
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(EquatableArray<>))
        {
            elem = t.GetGenericArguments()[0];
            return true;
        }
        elem = typeof(object);
        return false;
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
