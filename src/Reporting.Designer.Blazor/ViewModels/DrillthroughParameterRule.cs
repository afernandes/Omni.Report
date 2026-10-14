using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Reflection;
using Reporting.Common;
using Reporting.Designer.Blazor.Services;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Metadata;
using Reporting.Styling;

namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>
/// Mutable wrapper around a <see cref="DrillthroughParameter"/>. Exposed as a row in the
/// Action editor's parameter table: Name + Value expression + Omit toggle.
/// </summary>
public sealed class DrillthroughParameterRule : Notifying
{
    private string _name = string.Empty;
    public string Name { get => _name; set => Set(ref _name, value); }

    private string _value = string.Empty;
    /// <summary>Expression evaluated in the SOURCE report's context; the result is passed
    /// to the target report under <see cref="Name"/>.</summary>
    public string Value { get => _value; set => Set(ref _value, value); }

    private bool _omit;
    /// <summary>When true, the parameter is not passed at all — matches RDL <c>&lt;Omit&gt;</c>.</summary>
    public bool Omit { get => _omit; set => Set(ref _omit, value); }

    internal DrillthroughParameter ToCoreParameter() => new(Name, Value, Omit);

    internal static DrillthroughParameterRule From(DrillthroughParameter p) =>
        new() { Name = p.Name, Value = p.Value, Omit = p.Omit };
}
