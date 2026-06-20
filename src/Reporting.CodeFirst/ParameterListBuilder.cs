using Reporting.Common;
using Reporting.Parameters;

namespace Reporting.CodeFirst;

/// <summary>Fluent builder for the report's parameter list.</summary>
public sealed class ParameterListBuilder
{
    private readonly List<ReportParameter> _parameters = [];

    public ParameterListBuilder Add<T>(string name, string? prompt = null, T? defaultValue = default, bool required = true,
        ParameterAvailableValues? availableValues = null)
    {
        _parameters.Add(new ReportParameter(name, typeof(T), prompt, defaultValue, AllowMultiple: false, required, availableValues));
        return this;
    }

    public ParameterListBuilder Add(ReportParameter parameter)
    {
        _parameters.Add(parameter);
        return this;
    }

    internal EquatableArray<ReportParameter> Build() => new(_parameters);
}
