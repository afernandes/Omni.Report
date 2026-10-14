using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Reporting.DataSources.Json;

public sealed partial class JsonDataSource
{

    /// <summary>Sentinel schema returned before the first <c>ReadAsync</c> call.</summary>
    private sealed class EmptyJsonSchema : IReportRecordSchema
    {
        public static readonly EmptyJsonSchema Instance = new();
        public IReadOnlyList<ReportField> Fields { get; } = Array.Empty<ReportField>();
        public int IndexOf(string name) => -1;
    }
}
