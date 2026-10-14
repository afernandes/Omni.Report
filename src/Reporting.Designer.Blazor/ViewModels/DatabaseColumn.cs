namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>One column inside a database table or view (schema explorer leaf).</summary>
public sealed record DatabaseColumn(string Name, string DataType, bool IsNullable, bool IsPrimaryKey = false);
