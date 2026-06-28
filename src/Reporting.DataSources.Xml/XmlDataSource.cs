using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.XPath;

namespace Reporting.DataSources.Xml;

/// <summary>
/// Streams report records from an XML document. Mirrors the RDL/My-FyiReporting
/// <c>XmlDataReader</c>: pick a <see cref="XmlDataSourceOptions.RowsXPath"/>, and for
/// each matching node produce a row whose columns come from attributes, child
/// elements, or explicit per-column XPaths.
/// </summary>
/// <remarks>
/// <para><b>Discovery</b> — when the user doesn't provide
/// <see cref="XmlDataSourceOptions.ColumnXPaths"/>, the column set is inferred from the
/// FIRST row node. The discovery mode (<see cref="XmlColumnDiscovery.Attributes"/>,
/// <see cref="XmlColumnDiscovery.Elements"/>, <see cref="XmlColumnDiscovery.Both"/>)
/// chooses which children to harvest. Subsequent rows can have a SUBSET of those
/// columns — missing columns read as <c>null</c>.</para>
///
/// <para><b>Namespaces</b> — XML in the wild loves namespaces (Atom, RSS 2.0 with
/// content:encoded, SOAP). Register prefixes via <see cref="XmlDataSourceOptions.Namespaces"/>
/// and reference them in XPath expressions: <c>{"atom", "http://www.w3.org/2005/Atom"}</c>
/// + <c>RowsXPath = "//atom:entry"</c>.</para>
///
/// <para><b>Type inference</b> — XML values are always strings on the wire. We pass
/// each value through <see cref="TypeInference.Coerce"/> so that <c>&lt;total&gt;12.50&lt;/total&gt;</c>
/// becomes a <see cref="double"/> at runtime — making expressions like
/// <c>Sum(Fields.total)</c> "just work".</para>
/// </remarks>
public sealed class XmlDataSource : IReportDataSource
{
    private readonly XmlDataSourceOptions _opts;
    private readonly HttpClient? _httpClient;

    // Process-wide fallback when the caller doesn't supply an HttpClient — reused across calls to avoid socket
    // exhaustion (a fresh `new HttpClient()` per request leaks sockets in TIME_WAIT).
    private static readonly HttpClient SharedHttp = new();
    private IReportRecordSchema _schema;

    public XmlDataSource(string name, XmlDataSourceOptions options, HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(options);
        var sources = new[] { options.FilePath, options.Url, options.InlineXml }
            .Count(s => !string.IsNullOrEmpty(s));
        if (sources != 1)
        {
            throw new ArgumentException(
                "Exactly one of FilePath, Url, or InlineXml must be set.", nameof(options));
        }
        Name = name;
        _opts = options;
        _httpClient = httpClient;
        _schema = EmptyXmlSchema.Instance;
    }

    public string Name { get; }
    public IReportRecordSchema Schema => _schema;

    public async IAsyncEnumerable<IReportRecord> ReadAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var raw = await LoadAsync(cancellationToken).ConfigureAwait(false);
        var doc = new XPathDocument(new StringReader(raw));
        var nav = doc.CreateNavigator();
        var nsmgr = BuildNamespaceManager(nav);

        // Iterate row nodes lazily — XPathNodeIterator is forward-only and doesn't
        // materialise the whole node set up-front.
        var rowIter = nav.Select(_opts.RowsXPath, nsmgr);

        // Pass 1: materialise rows into dictionaries (need to know column set before
        // we can stream typed values). For very large XML this could be optimised by
        // building the schema from the FIRST row and streaming the rest, but practical
        // XML reports are small enough that two-pass is the simpler correct choice.
        var rows = new List<IReadOnlyDictionary<string, object?>>();
        IReadOnlyList<ColumnDescriptor>? columnOrder = null;
        IReadOnlyDictionary<string, string>? explicitXpaths = _opts.ColumnXPaths;

        while (rowIter.MoveNext())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rowNav = rowIter.Current!;
            // First row drives column discovery if no explicit XPaths were provided.
            if (columnOrder is null)
            {
                if (explicitXpaths is null || explicitXpaths.Count == 0)
                {
                    columnOrder = DiscoverColumns(rowNav);
                }
                else
                {
                    columnOrder = explicitXpaths.Keys.Select(k => new ColumnDescriptor(k, string.Empty, IsAttribute: false)).ToArray();
                }
            }
            rows.Add(BuildRow(rowNav, nsmgr, columnOrder, explicitXpaths));
        }
        columnOrder ??= Array.Empty<ColumnDescriptor>();

        var columnNames = columnOrder.Select(c => c.Name).ToArray();
        _schema = BuildSchemaFromSample(columnNames, rows.Take(_opts.SchemaSampleSize));

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new DictionaryRecord(_schema, row);
            await Task.Yield();
        }
    }

    /// <summary>Discovered (or explicit) column metadata used by BuildRow to select values
    /// correctly. For attributes <see cref="IsAttribute"/> is true and namespace is the
    /// attribute namespace (usually empty); for elements it's the element's namespace URI
    /// so <see cref="XPathNavigator.SelectChildren(string, string)"/> finds it again
    /// without needing a prefix in the XPath.</summary>
    private readonly record struct ColumnDescriptor(string Name, string NamespaceUri, bool IsAttribute);

    // ── Source loading ──────────────────────────────────────────────────────────

    private async Task<string> LoadAsync(CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(_opts.InlineXml)) return _opts.InlineXml!;
        if (!string.IsNullOrEmpty(_opts.FilePath))
        {
            return await File.ReadAllTextAsync(_opts.FilePath!, ct).ConfigureAwait(false);
        }
        // Use the supplied client when present, otherwise the process-wide shared client (a per-call
        // `new HttpClient()` causes socket exhaustion under load).
        var client = _httpClient ?? SharedHttp;
        return await client.GetStringAsync(_opts.Url!, ct).ConfigureAwait(false);
    }

    // ── Namespace setup ─────────────────────────────────────────────────────────

    /// <summary>Builds an <see cref="XmlNamespaceManager"/> that includes (a) the
    /// namespaces declared on the document root (so default namespaces work transparently),
    /// and (b) any user-supplied prefix → URI overrides.</summary>
    private XmlNamespaceManager BuildNamespaceManager(XPathNavigator root)
    {
        var nsmgr = new XmlNamespaceManager(root.NameTable);
        // Auto-register namespaces visible at the root.
        foreach (var kv in root.GetNamespacesInScope(XmlNamespaceScope.All))
        {
            if (string.IsNullOrEmpty(kv.Key)) continue; // skip default; XPath has no syntax for it
            nsmgr.AddNamespace(kv.Key, kv.Value);
        }
        if (_opts.Namespaces is not null)
        {
            foreach (var kv in _opts.Namespaces)
            {
                nsmgr.AddNamespace(kv.Key, kv.Value);
            }
        }
        return nsmgr;
    }

    // ── Column discovery from the first row ─────────────────────────────────────

    private IReadOnlyList<ColumnDescriptor> DiscoverColumns(XPathNavigator rowNav)
    {
        var cols = new List<ColumnDescriptor>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        if (_opts.Discovery is XmlColumnDiscovery.Attributes or XmlColumnDiscovery.Both)
        {
            var attrIter = rowNav.Clone();
            if (attrIter.MoveToFirstAttribute())
            {
                do
                {
                    if (seen.Add(attrIter.LocalName))
                    {
                        cols.Add(new ColumnDescriptor(attrIter.LocalName, attrIter.NamespaceURI, IsAttribute: true));
                    }
                } while (attrIter.MoveToNextAttribute());
            }
        }
        if (_opts.Discovery is XmlColumnDiscovery.Elements or XmlColumnDiscovery.Both)
        {
            var childIter = rowNav.SelectChildren(XPathNodeType.Element);
            while (childIter.MoveNext())
            {
                var c = childIter.Current!;
                if (seen.Add(c.LocalName))
                {
                    // Capture the element's namespace URI so the row-level lookup uses
                    // SelectChildren(localName, namespaceUri) — works both for default
                    // namespaces (where no prefix is available) and for custom ones.
                    cols.Add(new ColumnDescriptor(c.LocalName, c.NamespaceURI, IsAttribute: false));
                }
            }
        }
        return cols;
    }

    // ── Per-row materialisation ─────────────────────────────────────────────────

    private static IReadOnlyDictionary<string, object?> BuildRow(
        XPathNavigator rowNav,
        XmlNamespaceManager nsmgr,
        IReadOnlyList<ColumnDescriptor> columnOrder,
        IReadOnlyDictionary<string, string>? explicitXpaths)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var col in columnOrder)
        {
            string? raw;
            if (explicitXpaths is not null && explicitXpaths.TryGetValue(col.Name, out var xpath))
            {
                raw = rowNav.Evaluate(xpath, nsmgr)?.ToString();
            }
            else if (col.IsAttribute)
            {
                var attr = rowNav.GetAttribute(col.Name, col.NamespaceUri);
                raw = string.IsNullOrEmpty(attr) ? null : attr;
            }
            else
            {
                // Element lookup respects the namespace URI directly — this is the
                // namespace-safe equivalent of SelectSingleNode("localName") that works
                // when the document uses a default namespace (e.g. Atom feeds).
                var child = rowNav.SelectChildren(col.Name, col.NamespaceUri);
                raw = child.MoveNext() ? child.Current!.Value : null;
            }
            var (value, _) = TypeInference.Coerce(raw);
            dict[col.Name] = value;
        }
        return dict;
    }

    // ── Schema inference ────────────────────────────────────────────────────────

    private static IReportRecordSchema BuildSchemaFromSample(
        IReadOnlyList<string> columnOrder,
        IEnumerable<IReadOnlyDictionary<string, object?>> rows)
    {
        var typeCandidates = columnOrder.ToDictionary(c => c, _ => new List<Type>(),
            StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            foreach (var col in columnOrder)
            {
                if (row.TryGetValue(col, out var v) && v is not null)
                {
                    typeCandidates[col].Add(v.GetType());
                }
            }
        }
        var fields = columnOrder.Select(c =>
            new ReportField(c, TypeInference.ConsolidateColumnType(typeCandidates[c]))).ToArray();
        return new ReportRecordSchema(fields);
    }

    private sealed class EmptyXmlSchema : IReportRecordSchema
    {
        public static readonly EmptyXmlSchema Instance = new();
        public IReadOnlyList<ReportField> Fields { get; } = Array.Empty<ReportField>();
        public int IndexOf(string name) => -1;
    }
}
