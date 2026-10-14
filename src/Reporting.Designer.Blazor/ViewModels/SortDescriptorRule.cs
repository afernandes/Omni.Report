using System.Collections.ObjectModel;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;

namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>Mutable wrapper around a <see cref="Reporting.Data.SortDescriptor"/> — one row
/// in the PropertyGrid sort-expression list. Expression + direction toggle.</summary>
public sealed class SortDescriptorRule : Notifying
{
    private string _expression = string.Empty;
    public string Expression { get => _expression; set => Set(ref _expression, value); }

    private Reporting.Data.SortDirection _direction = Reporting.Data.SortDirection.Ascending;
    public Reporting.Data.SortDirection Direction { get => _direction; set => Set(ref _direction, value); }

    internal Reporting.Data.SortDescriptor ToDescriptor() => new(Expression, Direction);

    internal static SortDescriptorRule From(Reporting.Data.SortDescriptor d) =>
        new() { Expression = d.Expression, Direction = d.Direction };
}
