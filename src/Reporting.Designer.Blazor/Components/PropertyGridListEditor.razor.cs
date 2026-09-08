using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Reporting;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Paper;
using Reporting.Styling;
using Reporting.Designer.Blazor;
using Reporting.Designer.Blazor.Components;
using Reporting.Designer.Blazor.Icons;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Output.Pdf;
using Reporting.Output.Excel;
using Reporting.Printing;
using System.Collections;
using System.Globalization;
using Reporting.Designer.Blazor.Services;
namespace Reporting.Designer.Blazor.Components;

public partial class PropertyGridListEditor
{
    protected override ElementViewModel? EditedElement => Element;
    [Parameter, EditorRequired] public ElementViewModel Element { get; set; } = default!;
    [Parameter, EditorRequired] public PropertyGridDescriptor Descriptor { get; set; } = default!;

    private readonly List<PropertyGridListRow> _rows = new();
    private ElementViewModel? _seededElement;
    private string? _seededPath;
    private object? _seededValue;

    private Type ItemType => Descriptor.Type.GetGenericArguments()[0];
    private IReadOnlyList<PropertyGridDescriptor> ItemFields => PropertyGridDescriptors.For(ItemType);

    // Seed local rows once per (element, descriptor) — NOT on our own commits (same Element reference) —
    // so add/remove never re-derives identity-less items and re-binds an input to a shifted row.
    protected override void OnParametersSet()
    {
        if (ReferenceEquals(_seededElement, Element) && _seededPath == Descriptor.Path && Equals(_seededValue, Descriptor.Get(Element.ToElement())))
        {
            return;
        }
        _seededElement = Element;
        _seededPath = Descriptor.Path;
        _seededValue = Descriptor.Get(Element.ToElement());
        _rows.Clear();
        if (Descriptor.Get(Element.ToElement()) is IEnumerable items)
        {
            foreach (var it in items)
            {
                _rows.Add(new PropertyGridListRow { Item = it });
            }
        }
    }

    private static Type EnumType(PropertyGridDescriptor d) => Nullable.GetUnderlyingType(d.Type) ?? d.Type;

    // Coerce the input string to the field's numeric type before the setter — PropertyInfo.SetValue throws
    // on a string→double/int, so passing the raw string would break editing a numeric list-item field.
    private static object ConvertNumber(PropertyGridDescriptor d, object? value)
    {
        var parsed = double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture),
            NumberStyles.Any, CultureInfo.InvariantCulture, out var x) ? x : 0;
        return Convert.ChangeType(parsed, Nullable.GetUnderlyingType(d.Type) ?? d.Type, CultureInfo.InvariantCulture);
    }

    private void EditField(PropertyGridListRow row, PropertyGridDescriptor field, object? value)
    {
        row.Item = field.Set(row.Item, value);
        Commit();
    }

    private void RemoveRow(PropertyGridListRow row)
    {
        _rows.Remove(row);
        Commit();
    }

    private void AddRow()
    {
        _rows.Add(new PropertyGridListRow { Item = CreateDefaultItem(ItemType) });
        Commit();
    }

    /// <summary>Rebuilds the immutable EquatableArray&lt;T&gt; from the working rows and pushes it back
    /// through the metadata setter (which re-hydrates the element, opaque-advanced included).</summary>
    private void Commit()
    {
        var typed = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(ItemType))!;
        foreach (var r in _rows)
        {
            typed.Add(r.Item);
        }
        var newArray = Activator.CreateInstance(Descriptor.Type, typed)!;
        Element.ApplyMetaSet(Descriptor, newArray);
        _seededValue = Descriptor.Get(Element.ToElement());
    }

    /// <summary>Creates a fresh list item by invoking its primary constructor with defaulted args
    /// (empty string / default struct / null) — the user then fills the fields in.</summary>
    private static object CreateDefaultItem(Type itemType)
    {
        var ctor = itemType.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
        var args = ctor.GetParameters().Select(p =>
            p.ParameterType == typeof(string) ? string.Empty
            : p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType)
            : null).ToArray();
        return ctor.Invoke(args);
    }

}
