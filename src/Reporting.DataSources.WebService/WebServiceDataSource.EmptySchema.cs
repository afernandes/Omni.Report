using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using Reporting.DataSources.Json;
using Reporting.DataSources.Xml;

namespace Reporting.DataSources.WebService;

public sealed partial class WebServiceDataSource
{

    private sealed class EmptySchema : IReportRecordSchema
    {
        public static readonly EmptySchema Instance = new();
        public IReadOnlyList<ReportField> Fields { get; } = Array.Empty<ReportField>();
        public int IndexOf(string name) => -1;
    }
}
