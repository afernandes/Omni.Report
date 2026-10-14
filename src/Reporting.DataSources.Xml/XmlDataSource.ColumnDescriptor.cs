using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.XPath;

namespace Reporting.DataSources.Xml;

public sealed partial class XmlDataSource
{

    /// <summary>Discovered (or explicit) column metadata used by BuildRow to select values
    /// correctly. For attributes <see cref="IsAttribute"/> is true and namespace is the
    /// attribute namespace (usually empty); for elements it's the element's namespace URI
    /// so <see cref="XPathNavigator.SelectChildren(string, string)"/> finds it again
    /// without needing a prefix in the XPath.</summary>
    private readonly partial record struct ColumnDescriptor(string Name, string NamespaceUri, bool IsAttribute);
}
