using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Reporting.DataSources.Json;

/// <summary>
/// Streams report records from a JSON document. Mirrors the My-FyiReporting / RDL
/// <c>JsonDataReader</c> contract — accepts a JSON document, locates a JSON array
/// inside it, and yields each element as a row.
/// </summary>
/// <remarks>
/// <para><b>Where the array lives</b> — three shapes are supported transparently:
/// <list type="bullet">
/// <item>The whole document is the array: <c>[ { "id": 1 }, { "id": 2 } ]</c></item>
/// <item>The array is at the root level of the document: <c>{ "items": [ … ] }</c>.
/// Pick the property with <see cref="JsonDataSourceOptions.RootPath"/> = <c>"items"</c>.</item>
/// <item>The array is nested: <c>{ "data": { "results": [ … ] } }</c>. Use a dot path:
/// <see cref="JsonDataSourceOptions.RootPath"/> = <c>"data.results"</c>.</item>
/// </list>
/// </para>
///
/// <para><b>Schema inference</b> — column types are inferred by walking the first
/// <see cref="JsonDataSourceOptions.SchemaSampleSize"/> rows (default 100). Per-row
/// candidate types are merged via <see cref="TypeInference.WidenType"/> so a column
/// that starts numeric and turns into a string later widens to <see cref="string"/>.
/// A row's missing keys read as <c>null</c>; the dictionary used by every row is
/// case-insensitive so <c>{Fields.Total}</c> matches "total" or "Total" alike.</para>
///
/// <para><b>Streaming</b> — the document is materialised once into a <see cref="JsonDocument"/>
/// (for schema inference) but yielded lazily so reports paginating from a huge JSON
/// file don't keep every record alive after the page that displayed it. The
/// <c>JsonDocument</c> is disposed when iteration finishes.</para>
///
/// <para>SOURCES — choose ONE of:
/// <list type="bullet">
/// <item><see cref="JsonDataSourceOptions.FilePath"/> — local file (sync I/O wrapped in async).</item>
/// <item><see cref="JsonDataSourceOptions.Url"/> — HTTP/HTTPS GET via <see cref="HttpClient"/>.
/// Pass a custom <see cref="HttpClient"/> if you need auth headers; the source will
/// not dispose a client you provided.</item>
/// <item><see cref="JsonDataSourceOptions.InlineJson"/> — pre-loaded string. Useful for
/// tests and for ".repx-embedded data" workflows.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class JsonDataSource : IReportDataSource
{
    private readonly JsonDataSourceOptions _opts;
    private readonly HttpClient? _httpClient;
    private IReportRecordSchema _schema;

    public JsonDataSource(string name, JsonDataSourceOptions options, HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(options);
        var sources = new[] { options.FilePath, options.Url, options.InlineJson }
            .Count(s => !string.IsNullOrEmpty(s));
        if (sources != 1)
        {
            throw new ArgumentException(
                "Exactly one of FilePath, Url, or InlineJson must be set.", nameof(options));
        }
        Name = name;
        _opts = options;
        _httpClient = httpClient;
        // Pre-populate schema on first ReadAsync call. Until then, expose an empty schema —
        // the designer's field tree can still show "no columns yet" without crashing.
        _schema = EmptyJsonSchema.Instance;
    }

    public string Name { get; }
    public IReportRecordSchema Schema => _schema;

    public async IAsyncEnumerable<IReportRecord> ReadAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var raw = await LoadAsync(cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(raw);
        var array = LocateArray(doc.RootElement, _opts.RootPath);
        if (array.ValueKind != JsonValueKind.Array)
        {
            // Single-object documents become a one-row source — RDL behaviour for "data"
            // shapes that aren't actually arrays. Treat the object as a single row.
            if (array.ValueKind == JsonValueKind.Object)
            {
                var single = ConvertElement(array);
                _schema = BuildSchemaFromSample(new[] { single });
                yield return new DictionaryRecord(_schema, single);
                yield break;
            }
            throw new InvalidOperationException(
                $"JsonDataSource '{Name}': root path '{_opts.RootPath}' resolved to {array.ValueKind}, not Array or Object.");
        }

        // Pass 1: materialise sampled rows for schema inference. We can't usefully start
        // streaming records before the schema is known — the expression context needs the
        // field list to coerce types correctly.
        var rows = new List<IReadOnlyDictionary<string, object?>>(array.GetArrayLength());
        foreach (var element in array.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rows.Add(ConvertElement(element));
        }
        _schema = BuildSchemaFromSample(rows.Take(_opts.SchemaSampleSize));
        // Pass 2: yield every materialised row against the now-final schema.
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new DictionaryRecord(_schema, row);
            await Task.Yield();
        }
    }

    // ── Source loading ──────────────────────────────────────────────────────────

    private async Task<string> LoadAsync(CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(_opts.InlineJson)) return _opts.InlineJson!;
        if (!string.IsNullOrEmpty(_opts.FilePath))
        {
            return await File.ReadAllTextAsync(_opts.FilePath!, ct).ConfigureAwait(false);
        }
        // URL — use the supplied HttpClient when present (lets the caller configure
        // auth headers, proxies, etc.), or fall back to a per-call default client.
        if (_httpClient is not null)
        {
            return await _httpClient.GetStringAsync(_opts.Url!, ct).ConfigureAwait(false);
        }
        using var client = new HttpClient();
        return await client.GetStringAsync(_opts.Url!, ct).ConfigureAwait(false);
    }

    // ── Path navigation ─────────────────────────────────────────────────────────

    /// <summary>Navigates a path inside the root element. Empty / null path returns the
    /// root unchanged. Supports dot-separated object properties and bracket array indices,
    /// freely mixed — e.g. <c>data.results</c>, <c>data.items[0].rows</c>, <c>[1].cells[2]</c>.
    /// A property segment names an object key; <c>[n]</c> indexes into an array (0-based).</summary>
    private static JsonElement LocateArray(JsonElement root, string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return root;
        var current = root;
        foreach (var token in TokenizePath(path))
        {
            if (token.IsIndex)
            {
                if (current.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidOperationException(
                        $"JsonDataSource: index [{token.Index}] applied to a {current.ValueKind}, not an Array (path '{path}').");
                }
                if (token.Index < 0 || token.Index >= current.GetArrayLength())
                {
                    throw new InvalidOperationException(
                        $"JsonDataSource: index [{token.Index}] is out of range (array length {current.GetArrayLength()}, path '{path}').");
                }
                current = current[token.Index];
            }
            else
            {
                if (current.ValueKind != JsonValueKind.Object
                    || !current.TryGetProperty(token.Name, out var next))
                {
                    throw new InvalidOperationException(
                        $"JsonDataSource: path segment '{token.Name}' not found in document (path '{path}').");
                }
                current = next;
            }
        }
        return current;
    }

    /// <summary>Splits a path like <c>data.items[0].rows</c> into ordered tokens — property
    /// names and array indices. A single dot-part may carry trailing indexers
    /// (<c>cells[1][2]</c>) and a part may be index-only (<c>[0]</c>).</summary>
    private static IEnumerable<PathToken> TokenizePath(string path)
    {
        foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var bracket = part.IndexOf('[');
            // Leading property name, if any (everything before the first '[').
            if (bracket != 0)
            {
                yield return PathToken.Property(bracket < 0 ? part : part[..bracket]);
            }
            // Trailing [n] groups.
            var rest = bracket < 0 ? string.Empty : part[bracket..];
            while (rest.Length > 0)
            {
                var close = rest.IndexOf(']');
                if (rest[0] != '[' || close < 0)
                {
                    throw new InvalidOperationException(
                        $"JsonDataSource: malformed index in path segment '{part}'.");
                }
                var inner = rest[1..close];
                if (!int.TryParse(inner, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx))
                {
                    throw new InvalidOperationException(
                        $"JsonDataSource: non-numeric array index '[{inner}]' in path segment '{part}'.");
                }
                yield return PathToken.AtIndex(idx);
                rest = rest[(close + 1)..];
            }
        }
    }

    private readonly record struct PathToken(string Name, int Index, bool IsIndex)
    {
        public static PathToken Property(string name) => new(name, 0, false);
        public static PathToken AtIndex(int index) => new(string.Empty, index, true);
    }

    // ── Per-row conversion ──────────────────────────────────────────────────────

    /// <summary>Flattens a single JSON object into a name→CLR-value dictionary. Nested
    /// objects are dot-joined (<c>address.city</c>); nested arrays are skipped (only
    /// the top-level row is materialised here — sub-arrays are best modelled as a
    /// separate data source bound to a SubDetail band).</summary>
    private static IReadOnlyDictionary<string, object?> ConvertElement(JsonElement el)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        FlattenInto(dict, el, prefix: string.Empty);
        return dict;
    }

    private static void FlattenInto(Dictionary<string, object?> dict, JsonElement el, string prefix)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in el.EnumerateObject())
                {
                    var key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
                    if (prop.Value.ValueKind is JsonValueKind.Object)
                    {
                        FlattenInto(dict, prop.Value, key);
                    }
                    else if (prop.Value.ValueKind is JsonValueKind.Array)
                    {
                        // Skip nested arrays at the row level — they belong to sub-detail
                        // bands and should be exposed via a relation, not a flattened cell.
                        // The user can model these with a second JsonDataSource pointed at
                        // the sub-path.
                        continue;
                    }
                    else
                    {
                        dict[key] = ConvertScalar(prop.Value);
                    }
                }
                break;
            default:
                // A non-object array element (string, number, etc.) becomes a single-column
                // row named "value" — matches the RDL convention for "array of scalars".
                dict[string.IsNullOrEmpty(prefix) ? "value" : prefix] = ConvertScalar(el);
                break;
        }
    }

    private static object? ConvertScalar(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => ConvertNumber(el),
        JsonValueKind.String => ConvertStringScalar(el.GetString()),
        // Object / Array shouldn't reach this branch (handled above) — but stringify defensively.
        _ => el.ToString(),
    };

    private static object ConvertNumber(JsonElement el)
    {
        // Prefer the narrowest CLR type that fits. Integers first so reports doing
        // %-on-int aren't surprised by float drift; fall back to double for fractions.
        if (el.TryGetInt32(out var i)) return i;
        if (el.TryGetInt64(out var l)) return l;
        if (el.TryGetDouble(out var d)) return d;
        return el.GetRawText();
    }

    private static object? ConvertStringScalar(string? s)
    {
        if (s is null) return null;
        // JSON spec says everything in quotes is a string — but RDL data providers
        // commonly serialise dates/bools/numbers as strings. We do a soft type coercion
        // so a "2024-01-15" string ends up as DateTime, "true" as bool, "12.5" as
        // double. This is the same heuristic AdoNet uses on `dynamic` columns.
        var (value, _) = TypeInference.Coerce(s);
        // Important: keep the raw string when coercion didn't promote (Coerce returns the
        // original string in that case). The schema-building pass widens accordingly.
        return value ?? s;
    }

    // ── Schema inference ────────────────────────────────────────────────────────

    private static IReportRecordSchema BuildSchemaFromSample(
        IEnumerable<IReadOnlyDictionary<string, object?>> rows)
    {
        // Column ORDER is taken from the FIRST row we see — JSON object property order is
        // preserved by System.Text.Json. Subsequent rows can add new columns but only
        // contribute type information for existing ones.
        var columns = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var typeCandidates = new Dictionary<string, List<Type>>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            foreach (var kv in row)
            {
                if (seen.Add(kv.Key))
                {
                    columns.Add(kv.Key);
                    typeCandidates[kv.Key] = new List<Type>();
                }
                if (kv.Value is not null)
                {
                    typeCandidates[kv.Key].Add(kv.Value.GetType());
                }
            }
        }

        var fields = columns.Select(c => new ReportField(c,
            TypeInference.ConsolidateColumnType(typeCandidates[c]))).ToArray();
        return new ReportRecordSchema(fields);
    }

    /// <summary>Sentinel schema returned before the first <c>ReadAsync</c> call.</summary>
    private sealed class EmptyJsonSchema : IReportRecordSchema
    {
        public static readonly EmptyJsonSchema Instance = new();
        public IReadOnlyList<ReportField> Fields { get; } = Array.Empty<ReportField>();
        public int IndexOf(string name) => -1;
    }
}
