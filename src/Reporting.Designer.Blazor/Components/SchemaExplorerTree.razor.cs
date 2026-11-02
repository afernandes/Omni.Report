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
using Reporting.Printing;
namespace Reporting.Designer.Blazor.Components;

public partial class SchemaExplorerTree
{
    [Parameter] public IReadOnlyList<DatabaseTable> Tables { get; set; } = Array.Empty<DatabaseTable>();
    [Parameter] public bool Busy { get; set; }
    [Parameter] public string? Error { get; set; }
    [Parameter] public EventCallback OnRefresh { get; set; }
    [Parameter] public EventCallback<DatabaseTable> OnInsertTable { get; set; }
    [Parameter] public EventCallback<(DatabaseTable Table, DatabaseColumn Column)> OnInsertColumn { get; set; }

    private readonly HashSet<DatabaseTable> _expanded = new();
    private string _filter = string.Empty;
    private List<DatabaseTable> _tables = new();
    private bool _busy;
    private string? _error;

    protected override void OnParametersSet()
    {
        _busy = Busy;
        _error = Error;
        _tables = Tables
            .Where(t => string.IsNullOrEmpty(_filter)
                     || t.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase)
                     || t.Columns.Any(c => c.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private void Toggle(DatabaseTable t)
    {
        if (!_expanded.Add(t))
        {
            _expanded.Remove(t);
        }
    }

    private static IEnumerable<IGrouping<string, DatabaseTable>> GroupBySchema(IEnumerable<DatabaseTable> tables)
        => tables.GroupBy(t => t.Schema ?? string.Empty)
                 .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

    private static string TypeIndicator(string dataType)
    {
        var lower = dataType.ToLowerInvariant();
        if (lower.Contains("int") || lower.Contains("number") || lower.Contains("decimal")
            || lower.Contains("real") || lower.Contains("float") || lower.Contains("double"))
        {
            return "#";
        }
        if (lower.Contains("date") || lower.Contains("time"))
        {
            return "D";
        }
        if (lower.Contains("bool"))
        {
            return "B";
        }
        return "T";
    }
}
