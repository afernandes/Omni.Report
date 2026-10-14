using System.Collections.ObjectModel;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;

namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>Mutable wrapper around a group-scoped <see cref="Reporting.Parameters.ReportVariable"/>.
/// The scope is fixed to <see cref="Reporting.Parameters.VariableScope.Group"/> — group-level
/// variables are evaluated once per group instance.</summary>
public sealed class GroupVariableRule : Notifying
{
    private string _name = string.Empty;
    public string Name { get => _name; set => Set(ref _name, value); }

    private string _expression = string.Empty;
    public string Expression { get => _expression; set => Set(ref _expression, value); }

    internal Reporting.Parameters.ReportVariable ToVariable() =>
        new(Name, Expression, Reporting.Parameters.VariableScope.Group);

    internal static GroupVariableRule From(Reporting.Parameters.ReportVariable v) =>
        new() { Name = v.Name, Expression = v.Expression };
}
