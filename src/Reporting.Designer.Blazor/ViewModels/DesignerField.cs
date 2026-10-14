using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using Reporting.Common;
using Reporting.Data;

namespace Reporting.Designer.Blazor.ViewModels;

public sealed class DesignerField : Notifying
{
    public DesignerField(string name, DesignerFieldType type, string? sample = null)
    {
        _name = name;
        _type = type;
        _sample = sample;
    }

    private string _name;
    public string Name { get => _name; set => Set(ref _name, value); }

    private DesignerFieldType _type;
    public DesignerFieldType Type { get => _type; set => Set(ref _type, value); }

    private string? _sample;
    public string? Sample { get => _sample; set => Set(ref _sample, value); }

    public string TypeIndicator => Type switch
    {
        DesignerFieldType.Text => "T",
        DesignerFieldType.Number => "#",
        DesignerFieldType.Date => "D",
        DesignerFieldType.Bool => "B",
        DesignerFieldType.Money => "$",
        _ => "?",
    };

    public string TypeCssClass => Type switch
    {
        DesignerFieldType.Number => "t-num",
        DesignerFieldType.Date => "t-date",
        DesignerFieldType.Bool => "t-bool",
        DesignerFieldType.Money => "t-money",
        _ => "t-text",
    };

    public DesignerField Clone() => new(Name, Type, Sample);
}
