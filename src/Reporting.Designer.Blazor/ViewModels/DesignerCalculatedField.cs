using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using Reporting.Common;
using Reporting.Data;

namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>RDL <c>&lt;CalculatedField&gt;</c>: a virtual field whose value is computed
/// per row from an expression. Round-trips through <see cref="DataSourceDefinition.CalculatedFields"/>.</summary>
public sealed class DesignerCalculatedField : Notifying
{
    public DesignerCalculatedField(string name, string expression, DesignerFieldType resultType = DesignerFieldType.Text)
    {
        _name = name;
        _expression = expression;
        _resultType = resultType;
    }

    private string _name;
    public string Name { get => _name; set => Set(ref _name, value); }

    private string _expression;
    /// <summary>Raw expression (no template braces) — evaluated per row. Can reference
    /// other Fields, Parameters, aggregates, and earlier CalculatedFields.</summary>
    public string Expression { get => _expression; set => Set(ref _expression, value); }

    private DesignerFieldType _resultType;
    /// <summary>Logical type hint for the computed value. Drives coercion at evaluation
    /// time when the expression returns a string but the column is supposed to be
    /// numeric / date / etc.</summary>
    public DesignerFieldType ResultType { get => _resultType; set => Set(ref _resultType, value); }
}
