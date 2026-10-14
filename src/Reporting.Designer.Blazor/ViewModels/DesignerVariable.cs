using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using Reporting.Common;
using Reporting.Data;

namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>A report-level computed variable in the designer — name + expression + scope, persisted as a
/// core <see cref="Reporting.Parameters.ReportVariable"/> (RDL <c>&lt;Variables&gt;</c>).</summary>
public sealed class DesignerVariable : Notifying
{
    public DesignerVariable(string name, string expression = "", Reporting.Parameters.VariableScope scope = Reporting.Parameters.VariableScope.Report)
    {
        _name = name;
        _expression = expression;
        _scope = scope;
    }

    private string _name;
    public string Name { get => _name; set => Set(ref _name, value); }

    private string _expression;
    /// <summary>Expression evaluated to produce the variable's value (e.g. <c>Sum(Fields.Total)</c>).</summary>
    public string Expression { get => _expression; set => Set(ref _expression, value); }

    private Reporting.Parameters.VariableScope _scope;
    /// <summary>When the variable is evaluated: per row, once per report, or once per group.</summary>
    public Reporting.Parameters.VariableScope Scope { get => _scope; set => Set(ref _scope, value); }

    internal Reporting.Parameters.ReportVariable ToVariable()
        => new(Name, Expression, Scope);

    internal static DesignerVariable From(Reporting.Parameters.ReportVariable v)
        => new(v.Name, v.Expression, v.Scope);
}
