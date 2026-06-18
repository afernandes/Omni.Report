namespace Reporting.DataSources.Xml;

/// <summary>Auto-discovery strategy for XML column extraction. Mirrors My-FyiReporting's
/// <c>type=attributes|elements|both</c> connection-string option.</summary>
public enum XmlColumnDiscovery
{
    /// <summary>Each XML attribute on the row node becomes a column. Default for
    /// attribute-heavy documents like RSS.</summary>
    Attributes,

    /// <summary>Each direct child element becomes a column; the element's text value
    /// becomes the cell value. Default for "object literal" XML.</summary>
    Elements,

    /// <summary>Attributes AND direct child elements both become columns. Attributes
    /// take precedence when a name collision occurs.</summary>
    Both,

    /// <summary>Columns come exclusively from <see cref="XmlDataSourceOptions.ColumnXPaths"/>.
    /// Use when neither attributes nor child-elements map cleanly (e.g. computed
    /// columns via <c>sum(./items/item/@price)</c>).</summary>
    Explicit,
}

/// <summary>Configuration for an <see cref="XmlDataSource"/>. Exactly one of
/// <see cref="FilePath"/>, <see cref="Url"/>, <see cref="InlineXml"/> must be set.</summary>
public sealed class XmlDataSourceOptions
{
    /// <summary>Local file path to an XML document.</summary>
    public string? FilePath { get; init; }

    /// <summary>HTTP/HTTPS URL of an XML document.</summary>
    public string? Url { get; init; }

    /// <summary>Pre-loaded XML string.</summary>
    public string? InlineXml { get; init; }

    /// <summary>XPath that selects the row nodes. Required.
    /// Example: <c>/rss/channel/item</c> for RSS, <c>/Orders/Order</c> for a typical
    /// order document. Default <c>//</c> picks every element (rarely what you want).</summary>
    public string RowsXPath { get; init; } = "//";

    /// <summary>How columns are discovered relative to each row node when
    /// <see cref="ColumnXPaths"/> is empty.</summary>
    public XmlColumnDiscovery Discovery { get; init; } = XmlColumnDiscovery.Elements;

    /// <summary>Explicit per-column XPath expressions. Keys are column names; values
    /// are XPaths evaluated relative to the row node. When non-empty, this overrides
    /// <see cref="Discovery"/>.</summary>
    public IReadOnlyDictionary<string, string>? ColumnXPaths { get; init; }

    /// <summary>Custom prefix → namespace URI mappings registered with the XPath
    /// engine. Use this when the source XML declares default or custom namespaces
    /// that XPath needs to address (e.g. <c>{"a", "http://www.w3.org/2005/Atom"}</c>).</summary>
    public IReadOnlyDictionary<string, string>? Namespaces { get; init; }

    /// <summary>Number of leading rows scanned for schema inference. Default 100.</summary>
    public int SchemaSampleSize { get; init; } = 100;
}
