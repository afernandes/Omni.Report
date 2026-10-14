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

/// <summary>Mutable wrapper around a chart <see cref="ChartSeries"/> — one row in the chart
/// editor: name + category/value expressions + optional colour.</summary>
public sealed class ChartSeriesRule : Notifying
{
    private string _name = "Série 1";
    public string Name { get => _name; set => Set(ref _name, value); }

    private string _categoryExpression = "Fields.Categoria";
    public string CategoryExpression { get => _categoryExpression; set => Set(ref _categoryExpression, value); }

    private string _valueExpression = "Fields.Valor";
    public string ValueExpression { get => _valueExpression; set => Set(ref _valueExpression, value); }

    private Color? _color;
    public Color? Color { get => _color; set => Set(ref _color, value); }

    /// <summary>Series colour as a <c>#RRGGBB</c> hex string, for the PropertyGrid colour picker.
    /// Null model colour surfaces a sensible default; an unparseable value is ignored.</summary>
    public string ColorHex
    {
        get => _color?.ToHex() ?? "#4F46E5";
        set { try { Color = Reporting.Styling.Color.FromHex(value); } catch (FormatException) { } }
    }

    private string _sizeExpression = string.Empty;
    /// <summary>Bubble marker size expression — used when the chart kind is <c>Bubble</c>.</summary>
    public string SizeExpression { get => _sizeExpression; set => Set(ref _sizeExpression, value); }

    private string _highExpression = string.Empty;
    /// <summary>Stock high expression — used when the chart kind is <c>Stock</c>.</summary>
    public string HighExpression { get => _highExpression; set => Set(ref _highExpression, value); }

    private string _lowExpression = string.Empty;
    /// <summary>Stock low expression — used when the chart kind is <c>Stock</c>.</summary>
    public string LowExpression { get => _lowExpression; set => Set(ref _lowExpression, value); }

    internal ChartSeries ToSeries() => new(Name, CategoryExpression, ValueExpression, Color,
        SizeExpression: string.IsNullOrWhiteSpace(SizeExpression) ? null : SizeExpression,
        HighExpression: string.IsNullOrWhiteSpace(HighExpression) ? null : HighExpression,
        LowExpression: string.IsNullOrWhiteSpace(LowExpression) ? null : LowExpression);

    internal static ChartSeriesRule From(ChartSeries s) => new()
    {
        Name = s.Name,
        CategoryExpression = s.CategoryExpression,
        ValueExpression = s.ValueExpression,
        Color = s.Color,
        SizeExpression = s.SizeExpression ?? string.Empty,
        HighExpression = s.HighExpression ?? string.Empty,
        LowExpression = s.LowExpression ?? string.Empty,
    };
}
