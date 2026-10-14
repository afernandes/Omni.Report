using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using Reporting.Common;
using Reporting.Data;

namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>One master→detail relationship between two data sources.
/// At paginate time, the detail band iterates child rows filtered by the current parent
/// row's field value (joining on equality).</summary>
public sealed class DesignerRelation : Notifying
{
    public DesignerRelation(string name, string parentSource, string parentField, string childSource, string childField)
    {
        _name = name;
        _parentSource = parentSource;
        _parentField = parentField;
        _childSource = childSource;
        _childField = childField;
    }

    private string _name;
    public string Name { get => _name; set => Set(ref _name, value); }

    private string _parentSource;
    public string ParentSource { get => _parentSource; set => Set(ref _parentSource, value); }

    private string _parentField;
    public string ParentField { get => _parentField; set => Set(ref _parentField, value); }

    private string _childSource;
    public string ChildSource { get => _childSource; set => Set(ref _childSource, value); }

    private string _childField;
    public string ChildField { get => _childField; set => Set(ref _childField, value); }

    public DesignerRelation Clone() => new(Name, ParentSource, ParentField, ChildSource, ChildField);
}
