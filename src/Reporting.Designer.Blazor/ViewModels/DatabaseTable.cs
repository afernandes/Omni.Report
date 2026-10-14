namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>One table or view discovered by the schema explorer.</summary>
public sealed record DatabaseTable(
    string Schema,
    string Name,
    bool IsView,
    IReadOnlyList<DatabaseColumn> Columns)
{
    public string QualifiedName => string.IsNullOrEmpty(Schema) ? Name : $"{Schema}.{Name}";
}
