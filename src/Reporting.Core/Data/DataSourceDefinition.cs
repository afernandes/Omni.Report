using Reporting.Common;

namespace Reporting.Data;

/// <summary>Declarative reference to a data source resolved at runtime by name.</summary>
/// <remarks>
/// The actual <c>IReportDataSource</c> implementation lives in <c>Reporting.DataSources</c>.
/// The Core model only stores the metadata needed to wire the source to the layout engine.
///
/// <para>RDL-compatibility additions:
/// <list type="bullet">
/// <item><see cref="CalculatedFields"/> — virtual fields whose values are computed per row
/// from an expression. Appear as <c>Fields.{Name}</c> at evaluation time.</item>
/// <item><see cref="FilterExpression"/> — boolean expression applied at the data source
/// level, before any region consumes rows. Mirrors RDL <c>&lt;Filters&gt;</c> on the
/// <c>&lt;DataSet&gt;</c>.</item>
/// <item><see cref="SortExpressions"/> — global sort applied before any region sees the
/// rows. Mirrors RDL <c>&lt;SortExpressions&gt;</c> on the <c>&lt;DataSet&gt;</c>.</item>
/// </list>
/// </para>
/// </remarks>
public sealed record DataSourceDefinition(
    string Name,
    string? DataMember = null,
    EquatableArray<DataField> Fields = default,
    EquatableArray<DataRelation> Relations = default,
    EquatableDictionary<string, string> Parameters = default,
    EquatableArray<CalculatedField> CalculatedFields = default,
    string? FilterExpression = null,
    EquatableArray<SortDescriptor> SortExpressions = default);

public sealed record DataField(string Name, Type? FieldType = null, string? DisplayName = null);

/// <summary>Master-detail relationship between two data sources.</summary>
public sealed record DataRelation(
    string Name,
    string ParentSource,
    string ParentField,
    string ChildSource,
    string ChildField);
