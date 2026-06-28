using Reporting.Common;
using Reporting.Parameters;

namespace Reporting.CodeFirst;

/// <summary>Fluent builder for the report's parameter list.</summary>
public sealed class ParameterListBuilder
{
    private readonly List<ReportParameter> _parameters = [];

    /// <summary>Defines a single-valued report parameter of type <typeparamref name="T"/> and returns the builder for chaining.</summary>
    /// <param name="name">Unique parameter name used to reference it in expressions and queries.</param>
    /// <param name="prompt">Label shown to the user when prompting for a value; defaults to the name when omitted.</param>
    /// <param name="defaultValue">Value used when none is supplied at run time.</param>
    /// <param name="required">When true, a non-null value must be provided before the report can run.</param>
    /// <param name="availableValues">Optional set of valid/selectable values to constrain user input.</param>
    /// <param name="nullable">When true, the parameter may hold a null value.</param>
    /// <param name="allowBlank">When true, an empty string is accepted for text parameters.</param>
    /// <param name="hidden">When true, the parameter is not shown in the prompt UI.</param>
    public ParameterListBuilder Add<T>(string name, string? prompt = null, T? defaultValue = default, bool required = true,
        ParameterAvailableValues? availableValues = null, bool nullable = false, bool allowBlank = false, bool hidden = false)
    {
        _parameters.Add(new ReportParameter(name, typeof(T), prompt, defaultValue, AllowMultiple: false, required, availableValues,
            Nullable: nullable, AllowBlank: allowBlank, Hidden: hidden));
        return this;
    }

    /// <summary>Appends a pre-built <see cref="ReportParameter"/> to the list and returns the builder for chaining.</summary>
    public ParameterListBuilder Add(ReportParameter parameter)
    {
        _parameters.Add(parameter);
        return this;
    }

    internal EquatableArray<ReportParameter> Build() => new(_parameters);
}
