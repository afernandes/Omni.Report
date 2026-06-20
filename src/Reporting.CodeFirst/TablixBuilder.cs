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

    private readonly List<(string Expr, string? Sort, bool Desc)> _rowGroups = [];
    private readonly List<(string Expr, string? Sort, bool Desc)> _columnGroups = [];
    private string? _corner;
    private string? _cellExpression;

    /// <summary>Turns the Tablix into a matrix/crosstab: groups data rows by this expression down
    /// the left axis. Call more than once to <b>nest</b> row groups (outer→inner). Pair with
    /// <see cref="ColumnGroup"/> and <see cref="Cell"/>. Pass <paramref name="sortExpression"/> to order
    /// the group instances (e.g. <c>"Fields.total"</c>) ascending, or set <paramref name="descending"/>.</summary>
    public TablixBuilder RowGroup(string expression, string? sortExpression = null, bool descending = false)
    {
        _rowGroups.Add((expression, sortExpression, descending));
        return this;
    }

    /// <summary>Groups data rows by this expression across the top axis of a matrix. Call more than
    /// once to <b>nest</b> column groups (outer→inner). Optionally order the group instances by
    /// <paramref name="sortExpression"/> (<paramref name="descending"/> to reverse).</summary>
    public TablixBuilder ColumnGroup(string expression, string? sortExpression = null, bool descending = false)
    {
        _columnGroups.Add((expression, sortExpression, descending));
        return this;
    }

    /// <summary>Top-left corner label of a matrix (optional).</summary>
    public TablixBuilder Corner(string label) { _corner = label; return this; }

    /// <summary>The matrix body value expression — SUMmed over each (row × column) intersection.</summary>
    public TablixBuilder Cell(string valueExpression) { _cellExpression = valueExpression; return this; }

    internal TablixElement Build()
    {
        // Matrix mode: one or more row groups + column groups (nested outer→inner) + a body value
        // form a crosstab. The renderer reads every RowGroups/ColumnGroups level and the (1,1) body
        // cell template.
        if (_rowGroups.Count > 0 && _columnGroups.Count > 0)
        {
            return new TablixElement
            {
                Bounds = Rectangle.Empty,
                DataSetName = _dataSet,
                RowGroups = new EquatableArray<TablixGroup>(
                    _rowGroups.Select((g, i) => new TablixGroup($"Rows{i}", g.Expr, g.Sort, g.Desc)).ToArray()),
                ColumnGroups = new EquatableArray<TablixGroup>(
                    _columnGroups.Select((g, i) => new TablixGroup($"Cols{i}", g.Expr, g.Sort, g.Desc)).ToArray()),
                Cells = new EquatableArray<TablixCell>(
                [
                    new TablixCell(0, 0, new LabelElement { Text = _corner ?? string.Empty, Bounds = Rectangle.Empty }),
                    new TablixCell(1, 1, new TextBoxElement { Expression = _cellExpression ?? "0", Bounds = Rectangle.Empty }),
                ]),
            };
        }

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
