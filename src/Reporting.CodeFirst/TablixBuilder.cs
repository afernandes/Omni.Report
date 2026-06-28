using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Styling;

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
    private Func<Style, Style>? _cellStyle;
    private readonly List<ConditionalFormat> _cellConditionalFormats = [];
    private bool _rowSubtotals;
    private bool _columnSubtotals;
    private string? _subtotalLabel;
    private string? _grandTotalLabel;
    private string? _noRowsMessage;
    private bool _repeatColumnHeaders = true;
    private bool _keepTogether;

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
    /// <summary>The matrix body cell's value expression, optionally styled (ForeColor/Font/alignment honoured by the
    /// matrix renderer; e.g. <c>.Cell("Fields.Total", s =&gt; s with { HorizontalAlignment = HorizontalAlignment.Right })</c>).</summary>
    public TablixBuilder Cell(string valueExpression, Func<Style, Style>? style = null)
    {
        _cellExpression = valueExpression;
        _cellStyle = style;
        return this;
    }

    /// <summary>Adds a CONDITIONAL FORMAT to the matrix body cell: when <paramref name="condition"/> holds for a
    /// given intersection, <paramref name="style"/> overlays the base cell style. The condition sees that cell's
    /// aggregate as <c>Value</c> (or <c>Fields.Value</c>) — e.g.
    /// <c>.CellConditionalFormat("Value &lt; 0", Style.Default with { ForeColor = Color.Red })</c> paints the
    /// negative cells red, or a <c>BackColor</c> overlay makes a heat-map. Call more than once; formats apply in
    /// declaration order. Matrix mode only.</summary>
    public TablixBuilder CellConditionalFormat(string condition, Style style)
    {
        _cellConditionalFormats.Add(new ConditionalFormat(condition, style));
        return this;
    }

    /// <summary>Enables SSRS-style group totals: a subtotal row after each outer row-group block plus a
    /// grand total row at the bottom (each summing the body per column). Matrix mode only.</summary>
    public TablixBuilder RowSubtotals(bool enabled = true) { _rowSubtotals = enabled; return this; }

    /// <summary>Enables column-axis group totals: a subtotal column after each outer column-group block plus
    /// a grand total column at the right (each summing the body per row) — the mirror of
    /// <see cref="RowSubtotals"/>. Matrix mode only.</summary>
    public TablixBuilder ColumnSubtotals(bool enabled = true) { _columnSubtotals = enabled; return this; }

    /// <summary>Overrides the total labels. <paramref name="subtotal"/> uses <c>{0}</c> for the group value
    /// (default <c>"Total {0}"</c>); <paramref name="grandTotal"/> is the overall total (default
    /// <c>"Total geral"</c>). Pass null to keep a default. Applies to both row and column totals.</summary>
    public TablixBuilder TotalLabels(string? subtotal = null, string? grandTotal = null)
    {
        _subtotalLabel = subtotal;
        _grandTotalLabel = grandTotal;
        return this;
    }

    /// <summary>Message shown (centred, in place of the grid) when the bound dataset yields no rows — the
    /// RDL <c>NoRowsMessage</c>. Accepts a literal or an expression (<c>"=…"</c>). Applies to both modes.</summary>
    public TablixBuilder NoRowsMessage(string message)
    {
        _noRowsMessage = message;
        return this;
    }

    /// <summary>When the matrix is taller than the page it splits across pages by row; this controls whether the
    /// column header is reprinted at the top of each continuation page (default <c>true</c>, SSRS-style). Matrix mode.</summary>
    public TablixBuilder RepeatColumnHeaders(bool enabled = true) { _repeatColumnHeaders = enabled; return this; }

    /// <summary>Keeps the whole matrix together on one page instead of paginating it by row (it overflows if it
    /// doesn't fit). The opt-out of row-level pagination — default is to paginate. Matrix mode.</summary>
    public TablixBuilder KeepTogether(bool enabled = true) { _keepTogether = enabled; return this; }

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
                RowSubtotals = _rowSubtotals,
                ColumnSubtotals = _columnSubtotals,
                SubtotalLabel = _subtotalLabel,
                GrandTotalLabel = _grandTotalLabel,
                NoRowsMessage = _noRowsMessage,
                RepeatColumnHeaders = _repeatColumnHeaders,
                KeepTogether = _keepTogether,
                Cells = new EquatableArray<TablixCell>(
                [
                    new TablixCell(0, 0, new LabelElement { Text = _corner ?? string.Empty, Bounds = Rectangle.Empty }),
                    new TablixCell(1, 1, new TextBoxElement { Expression = _cellExpression ?? "0", Bounds = Rectangle.Empty, Style = _cellStyle?.Invoke(Style.Default) ?? Style.Default, ConditionalFormats = new EquatableArray<ConditionalFormat>(_cellConditionalFormats) }),
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
            NoRowsMessage = _noRowsMessage,
            RepeatColumnHeaders = _repeatColumnHeaders,
            KeepTogether = _keepTogether,
        };
    }
}
