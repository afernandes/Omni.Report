using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using Reporting.DataSources.Json;
using Reporting.DataSources.Xml;

namespace Reporting.DataSources.WebService;

/// <summary>
/// Streams report records from a REST endpoint. Issues a single HTTP request, parses
/// the response body as JSON or XML, and yields rows.
/// </summary>
/// <remarks>
/// <para>This is the modern replacement for the RDL/My-FyiReporting
/// <c>WebServiceDataReader</c>, which is SOAP/WSDL-based. SOAP services live behind
/// the <c>Method=POST + BodyContentType=text/xml</c> path; the actual JSON-or-XML
/// response is then dispatched to the matching streaming parser.</para>
///
/// <para><b>Composition over inheritance</b> — the parsing logic lives in
/// <see cref="JsonDataSource"/> and <see cref="XmlDataSource"/>. We just download the
/// body once, write it into an in-memory string, and hand it to whichever inner source
/// matches the content type. This means every JSON/XML feature (root paths, namespaces,
/// schema inference, type coercion) is automatically available without duplication.</para>
/// </remarks>
public sealed class WebServiceDataSource : IReportDataSource
{
    private readonly WebServiceDataSourceOptions _opts;
    private readonly HttpClient? _httpClient;

    // Process-wide fallback when no client is supplied and no per-call timeout is set — reused across calls to
    // avoid socket exhaustion (a fresh `new HttpClient()` per request leaks sockets in TIME_WAIT under load).
    private static readonly HttpClient SharedHttp = new();
    private IReportRecordSchema _schema;

    public WebServiceDataSource(string name, WebServiceDataSourceOptions options, HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(options.Url);
        Name = name;
        _opts = options;
        _httpClient = httpClient;
        _schema = EmptySchema.Instance;
    }

    public string Name { get; }
    public IReportRecordSchema Schema => _schema;

    public async IAsyncEnumerable<IReportRecord> ReadAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var (body, contentType) = await SendAsync(cancellationToken).ConfigureAwait(false);
        var format = ResolveFormat(contentType);
        IReportDataSource inner = format switch
        {
            WebServiceResponseFormat.Json => new JsonDataSource(Name,
                new JsonDataSourceOptions
                {
                    InlineJson = body,
                    RootPath = _opts.JsonRootPath,
                    SchemaSampleSize = _opts.SchemaSampleSize,
                }),
            WebServiceResponseFormat.Xml => new XmlDataSource(Name,
                new XmlDataSourceOptions
                {
                    InlineXml = body,
                    RowsXPath = _opts.XmlRowsXPath ?? "//*",
                    SchemaSampleSize = _opts.SchemaSampleSize,
                }),
            _ => throw new InvalidOperationException(
                $"WebServiceDataSource '{Name}': cannot infer response format for content type '{contentType}'. " +
                "Set Format = Json or Xml on WebServiceDataSourceOptions to force a parser."),
        };

        await foreach (var record in inner.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            // Lift the inner schema up so callers reading `WebServiceDataSource.Schema`
            // mid-iteration see the resolved field list.
            _schema = inner.Schema;
            yield return record;
        }
        _schema = inner.Schema; // ensure final state too (zero-row case)
    }

    // ── HTTP plumbing ───────────────────────────────────────────────────────────

    /// <summary>Builds the <see cref="HttpRequestMessage"/>, sends it, and returns the
    /// body plus the response's Content-Type header. We materialise the body as a
    /// string up-front because both inner parsers want the whole document anyway.</summary>
    private async Task<(string Body, string? ContentType)> SendAsync(CancellationToken ct)
    {
        // Common path (no per-call timeout) reuses the shared client to avoid socket exhaustion; only when an
        // explicit Timeout is set do we spin up — and dispose — an own client, since a shared/injected client's
        // global Timeout must not be mutated per call.
        var ownClient = _httpClient is null && _opts.Timeout is not null;
        var client = _httpClient ?? (ownClient ? new HttpClient { Timeout = _opts.Timeout!.Value } : SharedHttp);
        try
        {
            using var req = new HttpRequestMessage(new HttpMethod(_opts.Method), _opts.Url);
            // Headers — split between request headers and content headers (HttpClient is
            // pedantic about this; e.g. Content-Type goes on the content, Authorization
            // goes on the request). We try the request side first; HttpClient throws if
            // the header is actually a content header, in which case we attach to the
            // body content below.
            var deferredContentHeaders = new List<KeyValuePair<string, string>>();
            if (_opts.Headers is not null)
            {
                foreach (var kv in _opts.Headers)
                {
                    if (!req.Headers.TryAddWithoutValidation(kv.Key, kv.Value))
                    {
                        deferredContentHeaders.Add(kv);
                    }
                }
            }
            if (!string.IsNullOrEmpty(_opts.BodyContent))
            {
                req.Content = new StringContent(_opts.BodyContent!, Encoding.UTF8);
                req.Content.Headers.ContentType = new MediaTypeHeaderValue(_opts.BodyContentType);
                foreach (var kv in deferredContentHeaders)
                {
                    req.Content.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
                }
            }

            using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var contentType = resp.Content.Headers.ContentType?.MediaType;
            return (body, contentType);
        }
        finally
        {
            if (ownClient) client.Dispose();
        }
    }

    /// <summary>Selects the response parser. <see cref="WebServiceResponseFormat.Auto"/>
    /// inspects the MIME type — JSON wins on the JSON family, XML on the XML family.</summary>
    private WebServiceResponseFormat ResolveFormat(string? contentType)
    {
        if (_opts.Format != WebServiceResponseFormat.Auto) return _opts.Format;
        if (string.IsNullOrEmpty(contentType)) return WebServiceResponseFormat.Auto;
        contentType = contentType.ToLowerInvariant();
        if (contentType.Contains("json")) return WebServiceResponseFormat.Json;
        if (contentType.Contains("xml")) return WebServiceResponseFormat.Xml;
        return WebServiceResponseFormat.Auto;
    }

    private sealed class EmptySchema : IReportRecordSchema
    {
        public static readonly EmptySchema Instance = new();
        public IReadOnlyList<ReportField> Fields { get; } = Array.Empty<ReportField>();
        public int IndexOf(string name) => -1;
    }
}
