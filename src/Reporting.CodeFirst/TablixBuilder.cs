using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;

namespace Reporting.CodeFirst;

/// <summary>
/// Fluent builder for a banded <see cref="TablixElement"/> table. Each <see cref="Column"/>
/// adds a header label (cell row 0) and a per-row detail cell (cell row 1); the renderer draws
/// the header once and repeats the detail row for every record of the bound data source.
/// </summary>
public sealed class TablixBuilder
{
    private readonly List<(string Header, string Expression, double Width)> _columns = [];
    private string? _dataSet;

    /// <summary>Binds the table to a named data source. Defaults to the report's primary source.</summary>
    public TablixBuilder DataSet(string name)
    {
        _dataSet = name;
        return this;
    }

    /// <summary>Adds a column with a header label and a per-row detail expression
    /// (raw like <c>"Fields.produto"</c> or a template like <c>"{Fields.total:C}"</c>).
    /// <paramref name="width"/> is an optional relative weight: leave it <c>0</c> for an equal
    /// share, or pass e.g. <c>2</c> to make this column twice as wide as a <c>1</c>-weight column.
    /// Weights only take effect when at least one column sets one.</summary>
    public TablixBuilder Column(string header, string expression, double width = 0)
    {
        _columns.Add((header, expression, width));
        return this;
    }

    internal TablixElement Build()
    {
        var cells = new List<TablixCell>(_columns.Count * 2);
        for (int c = 0; c < _columns.Count; c++)
        {
            cells.Add(new TablixCell(0, c, new LabelElement
            {
                Text = _columns[c].Header,
                Bounds = Rectangle.Empty,
            }));
            cells.Add(new TablixCell(1, c, new TextBoxElement
            {
                Expression = _columns[c].Expression,
                Bounds = Rectangle.Empty,
            }));
        }
        // Only emit ColumnWidths when at least one column carries an explicit weight — keeps the
        // common equal-width table free of redundant metadata.
        var widths = _columns.Any(col => col.Width > 0)
            ? new EquatableArray<double>(_columns.Select(col => col.Width).ToArray())
            : EquatableArray<double>.Empty;
        return new TablixElement
        {
            Bounds = Rectangle.Empty,
            DataSetName = _dataSet,
            Cells = new EquatableArray<TablixCell>(cells),
            ColumnWidths = widths,
        };
    }
}
