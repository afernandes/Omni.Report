using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using Reporting.Common;
using Reporting.Data;

namespace Reporting.Designer.Blazor.ViewModels;

public sealed partial class DesignerDataSource
{

    // ─── .repx round-trip ───────────────────────────────────────────────────

    /// <summary>Reserved metadata keys persisted inside <see cref="DataSourceDefinition.Parameters"/>.
    /// Underscore prefix distinguishes them from user-supplied SQL parameter names
    /// (which use <c>@</c>/<c>$</c>/<c>:</c> prefixes).</summary>
    internal static class RepxKeys
    {
        public const string Kind = "_kind";
        public const string Conn = "_connection";
        public const string Sql = "_sql";
        public const string StoredProc = "_storedProc";
        public const string Timeout = "_timeout";
        public const string SqlParamPrefix = "param:";   // ↦ "param:@dataInicio" → "ReportParameter|literalValue"
    }
}
