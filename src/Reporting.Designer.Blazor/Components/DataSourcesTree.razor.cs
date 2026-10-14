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
using System.Collections.ObjectModel;
namespace Reporting.Designer.Blazor.Components;

public partial class DataSourcesTree
{
    [Parameter, EditorRequired] public IReadOnlyList<DesignerDataSource> DataSources { get; set; } = default!;
    [Parameter] public ObservableCollection<DesignerRelation>? Relations { get; set; }
    [Parameter] public EventCallback<DesignerDataSource> OnEdit { get; set; }

    /// <summary>Builds the entire visible tree as a flat ordered list. Each parent emits its
    /// source-row, its fields, and then recursively every child relation pair (badge + nested
    /// source-row + nested fields). The recursion is in plain C# — Razor only sees the
    /// flattened list, so rendering is deterministic.</summary>
    private List<TreeRow> BuildFlatTree()
    {
        var rows = new List<TreeRow>(DataSources.Count * 4);
        var childSourceNames = ComputeChildSourceNames();
        foreach (var ds in DataSources)
        {
            if (childSourceNames.Contains(ds.Name))
            {
                continue; // shown only as a nested child
            }
            EmitSource(rows, ds, depth: 0, viaLabel: null);
        }
        return rows;
    }

    private void EmitSource(List<TreeRow> rows, DesignerDataSource ds, int depth, string? viaLabel)
    {
        var depthClass = depth == 0 ? "depth-0" : "depth-1";
        var fieldDepthClass = depth == 0 ? "depth-1" : "depth-2";

        if (viaLabel is not null)
        {
            rows.Add(new RelationBadge(depthClass, viaLabel + " — editar na aba 'Relações' do data source", viaLabel));
        }

        rows.Add(new SourceRow(depthClass, ds));

        foreach (var f in ds.Fields)
        {
            rows.Add(new FieldRow(fieldDepthClass, ds, f, IsAmbiguous(f.Name)));
        }

        // Recurse into nested children — every relation where this source is the parent.
        if (Relations is null)
        {
            return;
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in Relations)
        {
            if (!string.Equals(r.ParentSource, ds.Name, StringComparison.Ordinal))
            {
                continue;
            }
            if (!seen.Add(r.ChildSource))
            {
                continue;
            }
            var child = DataSources.FirstOrDefault(d => string.Equals(d.Name, r.ChildSource, StringComparison.Ordinal));
            if (child is null)
            {
                continue;
            }
            var via = string.Equals(r.ParentField, r.ChildField, StringComparison.Ordinal)
                            ? $"via {r.ParentField}"
                            : $"via {r.ParentField} → {r.ChildField}";
            EmitSource(rows, child, depth + 1, via);
        }
    }

    /// <summary>Sources that appear as the child side of at least one relation — these are
    /// shown nested under their parent and skipped at root. Orphan child sources (relation
    /// references a deleted parent) fall back to root so the user can repair them.</summary>
    private HashSet<string> ComputeChildSourceNames()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (Relations is null)
        {
            return set;
        }
        foreach (var r in Relations)
        {
            if (DataSources.Any(d => string.Equals(d.Name, r.ParentSource, StringComparison.Ordinal)) &&
                DataSources.Any(d => string.Equals(d.Name, r.ChildSource, StringComparison.Ordinal)))
            {
                set.Add(r.ChildSource);
            }
        }
        return set;
    }

    /// <summary>True when the given field name appears in more than one registered data source.
    /// The drop handler reads <c>data-ambiguous</c>/<c>data-source-name</c> from the DOM and
    /// emits the qualified form <c>{Fields.SourceName.FieldName}</c> in that case.</summary>
    private bool IsAmbiguous(string fieldName)
    {
        int count = 0;
        foreach (var ds in DataSources)
        {
            foreach (var f in ds.Fields)
            {
                if (string.Equals(f.Name, fieldName, StringComparison.Ordinal))
                {
                    count++;
                    if (count > 1)
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }
}
