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
using Reporting.Designer.Blazor.Services;
namespace Reporting.Designer.Blazor.Components;

public partial class PropertyGridDictEditor
{
    protected override ElementViewModel? EditedElement => Element;
    [Parameter, EditorRequired] public ElementViewModel Element { get; set; } = default!;
    [Parameter, EditorRequired] public PropertyGridDescriptor Descriptor { get; set; } = default!;

    private readonly List<PropertyGridDictRow> _rows = new();
    private ElementViewModel? _seededElement;
    private string? _seededPath;
    private object? _seededValue;

    // Seed the local rows once per (element, descriptor) — NOT on our own commits, which keep the same
    // Element reference. That keeps the visible order stable and independent of the dict's hash order.
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
        var entries = Descriptor.Get(Element.ToElement()) as IEnumerable<KeyValuePair<string, string>>;
        if (entries is not null)
        {
            foreach (var kv in entries)
            {
                _rows.Add(new PropertyGridDictRow { Key = kv.Key, Value = kv.Value });
            }
        }
    }

    private void EditKey(PropertyGridDictRow row, string key)
    {
        row.Key = key;
        Commit();
    }

    private void EditValue(PropertyGridDictRow row, string val)
    {
        row.Value = val;
        Commit();
    }

    private void AddRow()
    {
        var key = "param";
        var n = 1;
        while (_rows.Any(r => r.Key == key))
        {
            key = $"param{++n}";
        }
        _rows.Add(new PropertyGridDictRow { Key = key });
        Commit();
    }

    private void RemoveRow(PropertyGridDictRow row)
    {
        _rows.Remove(row);
        Commit();
    }

    private void Commit()
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var r in _rows)
        {
            if (!string.IsNullOrEmpty(r.Key))
            {
                dict[r.Key] = r.Value; // empty keys stay as rows but aren't persisted; last wins on a transient collision
            }
        }
        Element.ApplyMetaSet(Descriptor, new EquatableDictionary<string, string>(dict));
        _seededValue = Descriptor.Get(Element.ToElement());
    }

}
