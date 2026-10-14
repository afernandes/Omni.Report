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
