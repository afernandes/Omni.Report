using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;

namespace Reporting.DataSources.AdoNet;

public sealed partial class AdoNetDataSource
{

    private sealed class EmptySchema : IReportRecordSchema
    {
        public static readonly EmptySchema Instance = new();
        public IReadOnlyList<ReportField> Fields { get; } = Array.Empty<ReportField>();
        public int IndexOf(string name) => -1;
    }
}
