namespace Reporting.DataSources.WebService;

/// <summary>Configuration for a <see cref="WebServiceDataSource"/>. Mirrors the contract
/// of a typical REST API consumer — URL + method + optional headers + optional body.</summary>
/// <remarks>
/// <para><b>Replaces RDL/My-FyiReporting WSDL</b> — modern back-ends speak REST/JSON
/// rather than SOAP. This provider issues a single HTTP request, parses the response
/// body either as JSON (delegating to <see cref="Json.JsonDataSource"/>) or as XML
/// (delegating to <see cref="Xml.XmlDataSource"/>) based on Content-Type negotiation.</para>
///
/// <para>For SOAP, write the request body manually as XML and set <c>Method = "POST"</c>,
/// <c>BodyContentType = "text/xml"</c>, <c>BodyContent = "&lt;?xml…&gt;…"</c>. The response
/// is still parsed as XML via XPath.</para>
/// </remarks>
public sealed class WebServiceDataSourceOptions
{
    /// <summary>Full URL of the endpoint to call.</summary>
    public required string Url { get; init; }

    /// <summary>HTTP method (GET/POST/PUT/DELETE/PATCH). Default GET.</summary>
    public string Method { get; init; } = "GET";

    /// <summary>HTTP request headers to add. Common entries:
    /// <c>{"Authorization", "Bearer …"}</c>, <c>{"Accept", "application/json"}</c>.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>Optional request body (for POST/PUT/PATCH). Sent as-is with the
    /// content type from <see cref="BodyContentType"/>.</summary>
    public string? BodyContent { get; init; }

    /// <summary>Content type of <see cref="BodyContent"/>. Defaults to
    /// <c>application/json</c>.</summary>
    public string BodyContentType { get; init; } = "application/json";

    /// <summary>Forces the response parser. <see cref="WebServiceResponseFormat.Auto"/>
    /// (default) sniffs <c>Content-Type</c>: <c>application/json</c> or <c>text/json</c>
    /// → JSON; <c>application/xml</c> / <c>text/xml</c> → XML. Set this explicitly when
    /// the server lies about the content type.</summary>
    public WebServiceResponseFormat Format { get; init; } = WebServiceResponseFormat.Auto;

    /// <summary>Dot-path to the row array inside a JSON response. Same semantics as
    /// <see cref="Json.JsonDataSourceOptions.RootPath"/>. Ignored for XML responses.</summary>
    public string? JsonRootPath { get; init; }

    /// <summary>XPath that selects row nodes from an XML response. Same semantics as
    /// <see cref="Xml.XmlDataSourceOptions.RowsXPath"/>. Ignored for JSON responses.</summary>
    public string? XmlRowsXPath { get; init; }

    /// <summary>Number of leading rows scanned for schema inference. Default 100.</summary>
    public int SchemaSampleSize { get; init; } = 100;

    /// <summary>How long the request may take before the underlying HttpClient throws.
    /// Null = use the HttpClient's default. The caller can pass a custom HttpClient
    /// to set this globally; this option is a convenience that wraps the per-request
    /// timeout via <see cref="HttpRequestMessage"/> (not currently implemented — relies
    /// on the HttpClient setting).</summary>
    public TimeSpan? Timeout { get; init; }
}

/// <summary>Response-format selector for <see cref="WebServiceDataSourceOptions.Format"/>.</summary>
public enum WebServiceResponseFormat
{
    /// <summary>Sniff <c>Content-Type</c> header.</summary>
    Auto,
    Json,
    Xml,
}
