using Reporting.Common;

namespace Reporting.Parameters;

/// <summary>Strongly typed report parameter — its <see cref="ValueType"/> is the CLR type
/// the runtime coerces the prompted input to before binding.</summary>
public sealed record ReportParameter(
    string Name,
    Type ValueType,
    string? Prompt = null,
    object? DefaultValue = null,
    bool AllowMultiple = false,
    bool Required = true,
    ParameterAvailableValues? AvailableValues = null,
    bool Nullable = false,
    bool AllowBlank = false,
    bool Hidden = false)
{
    /// <summary>An expression default (SSRS <c>=Today()</c>, <c>=DateAdd(...)</c>, <c>=Parameters!Other.Value</c>)
    /// in OmniReport syntax, evaluated at run start to seed the value when no literal <see cref="DefaultValue"/>
    /// and no prompted value are supplied. Mutually exclusive with <see cref="DefaultValue"/>.</summary>
    public string? DefaultValueExpression { get; init; }
}
