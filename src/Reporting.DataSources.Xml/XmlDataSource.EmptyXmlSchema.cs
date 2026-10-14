using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.XPath;

namespace Reporting.DataSources.Xml;

public sealed partial class XmlDataSource
{

    private sealed class EmptyXmlSchema : IReportRecordSchema
    {
        public static readonly EmptyXmlSchema Instance = new();
        public IReadOnlyList<ReportField> Fields { get; } = Array.Empty<ReportField>();
        public int IndexOf(string name) => -1;
    }
}
