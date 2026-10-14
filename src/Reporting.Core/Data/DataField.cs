using Reporting.Common;

namespace Reporting.Data;

/// <summary>Describes a single field exposed by a data source, with an optional CLR type and display name.</summary>
public sealed record DataField(string Name, Type? FieldType = null, string? DisplayName = null);
