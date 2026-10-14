using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using Reporting.Common;
using Reporting.Data;

namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>One SQL parameter ↔ report-parameter binding row. Edited as a grid in the
/// Query tab of the data source dialog.</summary>
public sealed class DesignerSqlParameter : Notifying
{
    public DesignerSqlParameter(string sqlName, string? reportParameter = null, string? literal = null)
    {
        _sqlName = sqlName;
        _reportParameter = reportParameter;
        _literal = literal;
    }

    private string _sqlName;
    /// <summary>The placeholder as it appears in the SQL (e.g. <c>@dataInicio</c>,
    /// <c>$customer</c>, <c>:id</c>). Keep the provider's prefix.</summary>
    public string SqlName { get => _sqlName; set => Set(ref _sqlName, value); }

    private string? _reportParameter;
    /// <summary>Report parameter name whose runtime value feeds this SQL placeholder. When
    /// non-null, takes precedence over <see cref="Literal"/>.</summary>
    public string? ReportParameter { get => _reportParameter; set => Set(ref _reportParameter, value); }

    private string? _literal;
    /// <summary>Hard-coded literal — used at preview time, and at runtime when
    /// <see cref="ReportParameter"/> is null.</summary>
    public string? Literal { get => _literal; set => Set(ref _literal, value); }

    public DesignerSqlParameter Clone() => new(SqlName, ReportParameter, Literal);
}
