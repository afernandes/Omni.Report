namespace Reporting.DataSources;

/// <param name="Name">Column name as the source reports it. Expressions address the field by this.</param>
/// <param name="Type">CLR type of the values. Provider-native types with no CLR equivalent surface as
/// <see cref="object"/>.</param>
public sealed record ReportField(string Name, Type Type);
