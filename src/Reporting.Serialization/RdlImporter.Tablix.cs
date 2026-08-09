using System.Xml.Linq;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Data;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Serialization.Internal;
using Reporting.Styling;

namespace Reporting.Serialization;

/// <summary>
/// The <c>&lt;Tablix&gt;</c> half of <see cref="RdlImporter"/>: the matrix/crosstab path, the flat-table
/// decomposition into PageHeader + Detail bands, and their helpers.
///
/// <para>Split out of the 1.700-line main file purely for navigability — <c>partial</c> means this is the
/// same class, so nothing about behaviour or accessibility changes. Tablix earns its own file: it is the
/// largest cohesive block in the importer and the one most often edited (PR #212 lost pagination flags here,
/// #217 lost the drill-down).</para>
/// </summary>
public sealed partial class RdlImporter
{
    // Maps an RDL <Tablix> to OmniReport's TablixElement. First cut: the matrix/crosstab path — dynamic
    // row + column group hierarchies + the body value cell + corner. Static-column tables and per-cell
    // spans are follow-ups; a warning is recorded (never silent) when the Tablix isn't a clean matrix.
    private ReportElement TablixItem(XElement item, Rectangle bounds)
    {
        var name = item.Attribute("Name")?.Value ?? "Tablix";
        var rowGroups = ReadTablixGroups(El(El(item, "TablixRowHierarchy"), "TablixMembers"), "Rows");
        var colGroups = ReadTablixGroups(El(El(item, "TablixColumnHierarchy"), "TablixMembers"), "Cols");

        // Pure flat table (RDL Table/List): no DYNAMIC group on either axis (static columns + a Details row).
        // The model/renderer already support this shape (Cells row 0 = header, row 1 = detail); map the grid.
        if (rowGroups.Count == 0 && colGroups.Count == 0)
        {
            return FlatTableTablix(item, bounds, name);
        }

        var cells = new List<TablixCell>();
        var cornerRaw = TextboxValue(FirstTextbox(El(item, "TablixCorner")));
        if (!string.IsNullOrEmpty(cornerRaw))
        {
            cells.Add(new TablixCell(0, 0, new LabelElement { Text = cornerRaw, Bounds = Rectangle.Empty }));
        }
        var bodyTextbox = FirstTextbox(El(item, "TablixBody"));
        var bodyRaw = TextboxValue(bodyTextbox);
        if (!string.IsNullOrEmpty(bodyRaw))
        {
            // Carry the body textbox's <Style> so the matrix value cell keeps its RDL Format (currency,
            // percent, …) instead of falling back to the renderer's N2 default.
            cells.Add(new TablixCell(1, 1, new TextBoxElement
            {
                Expression = RdlExpression.Convert(bodyRaw),
                Bounds = Rectangle.Empty,
                Style = bodyTextbox is null ? Style.Default : ReadStyle(bodyTextbox) ?? Style.Default,
            }));
        }

        if (rowGroups.Count == 0 || colGroups.Count == 0)
        {
            // Pure flat tables are handled above; reaching here means exactly one axis is dynamic and the
            // other static — a table+matrix hybrid, which is a follow-up.
            _warnings.Add($"Tablix '{name}': importado parcialmente — híbrido tabela+matrix (um eixo com grupo dinâmico e outro estático) é follow-up.");
        }

        return new TablixElement
        {
            Bounds = bounds,
            DataSetName = Val(item, "DataSetName"),
            NoRowsMessage = NoRowsOf(item),
            RowGroups = new EquatableArray<TablixGroup>(rowGroups),
            ColumnGroups = new EquatableArray<TablixGroup>(colGroups),
            Cells = new EquatableArray<TablixCell>(cells),
            RowSubtotals = HasSubtotalMember(El(El(item, "TablixRowHierarchy"), "TablixMembers")),
            ColumnSubtotals = HasSubtotalMember(El(El(item, "TablixColumnHierarchy"), "TablixMembers")),
        };
    }

    // SSRS emits a group total as an EMPTY <Group/> member — no GroupExpression AND no Name — sitting as a
    // SIBLING of the dynamic group member at the same nesting level. A level that holds BOTH a dynamic group
    // and such a total member signals a subtotal on that axis. Detection is deliberately CONSERVATIVE: a named
    // static group (<Group Name="Details"/> = detail rows) and a label member (no <Group> at all) are NOT
    // totals, so they never produce a false positive (worst case is a missed total, which imports cleanly).
    private static bool HasSubtotalMember(XElement? members)
    {
        if (members is null)
        {
            return false;
        }
        bool anyDynamic = false, anyTotal = false;
        foreach (var m in members.Elements().Where(e => e.Name.LocalName == "TablixMember"))
        {
            var group = El(m, "Group");
            bool isDynamic = !string.IsNullOrEmpty(Val(El(group, "GroupExpressions"), "GroupExpression"));
            if (isDynamic)
            {
                anyDynamic = true;
            }
            else if (group is not null && string.IsNullOrEmpty(group.Attribute("Name")?.Value))
            {
                anyTotal = true; // empty <Group/> with no Name = the static total member
            }
            if (HasSubtotalMember(El(m, "TablixMembers"))) // nested levels (inner group totals)
            {
                return true;
            }
        }
        return anyDynamic && anyTotal;
    }

    // RDL <NoRowsMessage> on a data region (literal or =expression) → the message shown for an empty dataset.
    private static string? NoRowsOf(XElement item)
        => Val(item, "NoRowsMessage") is { Length: > 0 } m ? RdlExpression.Convert(m) : null;

    // Imports an RDL flat Table/List (static columns + a Details row) into the TablixElement table shape the
    // renderer already understands: Cells (0,c) = header Label, (1,c) = detail TextBox, RowGroups/ColumnGroups
    // empty, ColumnWidths = the RDL column widths (relative weights). Header vs detail rows are classified by
    // the row hierarchy (the member with a <Group> is the Details/detail row), falling back to position.
    // Scope limits (acceptable for the common 1-header-1-detail table): a Details member nested under a static
    // parent isn't classified (positional fallback covers the 2-row case); a trailing column empty in BOTH
    // rows is dropped (it carries no value); multiple detail/header rows collapse to one each.
    private ReportElement FlatTableTablix(XElement item, Rectangle bounds, string name)
    {
        var body = El(item, "TablixBody");
        var bodyRows = (El(body, "TablixRows")?.Elements().Where(e => e.Name.LocalName == "TablixRow")
            ?? Enumerable.Empty<XElement>()).ToList();
        var widths = (El(body, "TablixColumns")?.Elements().Where(e => e.Name.LocalName == "TablixColumn")
            ?? Enumerable.Empty<XElement>())
            .Select(c => ParseSize(Val(c, "Width"))?.ToMm() ?? 0.0).ToList();

        // Classify: the row-hierarchy member with a <Group> (Details, even without GroupExpression) is the
        // detail row; the static member before it is the header. No hierarchy → positional (last = detail).
        var rowMembers = (El(El(item, "TablixRowHierarchy"), "TablixMembers")?.Elements()
            .Where(e => e.Name.LocalName == "TablixMember") ?? Enumerable.Empty<XElement>()).ToList();
        int detailIdx = rowMembers.FindIndex(m => El(m, "Group") is not null);
        XElement? headerRow, detailRow;
        if (detailIdx >= 0 && detailIdx < bodyRows.Count)
        {
            detailRow = bodyRows[detailIdx];
            headerRow = detailIdx > 0 ? bodyRows[detailIdx - 1] : null;
        }
        else
        {
            detailRow = bodyRows.Count >= 1 ? bodyRows[^1] : null;
            headerRow = bodyRows.Count >= 2 ? bodyRows[0] : null;
        }

        var cells = new List<TablixCell>();
        if (headerRow is not null)
        {
            int col = 0; // RDL <ColSpan> pushes the next cell spanColumns over, not 1.
            foreach (var hcell in RowCells(headerRow))
            {
                int span = ColSpanOf(hcell);
                if (TextboxValue(FirstTextbox(hcell)) is { Length: > 0 } v)
                {
                    cells.Add(new TablixCell(0, col, new LabelElement { Text = v, Bounds = Rectangle.Empty }, ColumnSpan: span));
                }
                col += span;
            }
        }
        if (detailRow is not null)
        {
            int col = 0;
            foreach (var dcell in RowCells(detailRow))
            {
                int span = ColSpanOf(dcell);
                var tb = FirstTextbox(dcell);
                if (TextboxValue(tb) is { Length: > 0 } v)
                {
                    cells.Add(new TablixCell(1, col, new TextBoxElement
                    {
                        Expression = RdlExpression.Convert(v),
                        Bounds = Rectangle.Empty,
                        Style = tb is null ? Style.Default : ReadStyle(tb) ?? Style.Default,
                    }, ColumnSpan: span));
                }
                col += span;
            }
        }
        if (cells.Count == 0)
        {
            _warnings.Add($"Tablix '{name}': tabela sem células de texto reconhecíveis — importada vazia.");
        }

        return new TablixElement
        {
            Bounds = bounds,
            DataSetName = Val(item, "DataSetName"),
            NoRowsMessage = NoRowsOf(item),
            Cells = new EquatableArray<TablixCell>(cells),
            // RDL widths are absolute; the renderer treats ColumnWidths as relative weights, preserving ratios.
            ColumnWidths = widths.Count >= 2 && widths.Any(w => w > 0)
                ? new EquatableArray<double>(widths)
                : EquatableArray<double>.Empty,
        };
    }

    // First-cut Tablix→bands: when the Body is EXACTLY one flat Tablix (no dynamic row/col groups) and the
    // page has no <PageHeader>, decompose it into a repeating column-header band (PageHeader) + a DetailBand
    // with one positioned TextBox per column, instead of one monolithic TablixElement inside the ReportHeader.
    // The detail band paginates row-by-row and the header repeats per page — like a native banded report.
    // Bounds are absolute per column (RDL column widths are absolute, anchored at the Tablix's Left). Anything
    // that doesn't match (extra Body items, dynamic groups, no columns, no detail row, an existing PageHeader)
    // returns false so the caller keeps the existing TablixElement path (with its own warning).
    private bool TryFlatTablixBands(XElement? body, bool pageHasHeader, out ReportBand? headerBand, out DetailBand? detail)
    {
        headerBand = null;
        detail = null;
        var items = El(body, "ReportItems")?.Elements().ToList() ?? new List<XElement>();
        if (pageHasHeader || items.Count != 1 || items[0].Name.LocalName != "Tablix")
        {
            return false;
        }
        var tablix = items[0];
        if (ReadTablixGroups(El(El(tablix, "TablixRowHierarchy"), "TablixMembers"), "Rows").Count != 0
            || ReadTablixGroups(El(El(tablix, "TablixColumnHierarchy"), "TablixMembers"), "Cols").Count != 0)
        {
            return false;
        }

        var tbBounds = TablixBounds(tablix, Unit.Zero, Unit.Zero);
        var tablixBody = El(tablix, "TablixBody");
        var bodyRows = (El(tablixBody, "TablixRows")?.Elements().Where(e => e.Name.LocalName == "TablixRow")
            ?? Enumerable.Empty<XElement>()).ToList();
        var colWidths = (El(tablixBody, "TablixColumns")?.Elements().Where(e => e.Name.LocalName == "TablixColumn")
            ?? Enumerable.Empty<XElement>())
            .Select(c => ParseSize(Val(c, "Width")) ?? Unit.Zero).ToList();
        if (colWidths.Count == 0)
        {
            return false; // no column geometry → can't place cells; keep the TablixElement path
        }

        // Classify header/detail rows the same way FlatTableTablix does (the member with a <Group> = detail).
        var rowMembers = (El(El(tablix, "TablixRowHierarchy"), "TablixMembers")?.Elements()
            .Where(e => e.Name.LocalName == "TablixMember") ?? Enumerable.Empty<XElement>()).ToList();
        int detailIdx = rowMembers.FindIndex(m => El(m, "Group") is not null);
        XElement? headerRow, detailRow;
        if (detailIdx >= 0 && detailIdx < bodyRows.Count)
        {
            detailRow = bodyRows[detailIdx];
            headerRow = detailIdx > 0 ? bodyRows[detailIdx - 1] : null;
        }
        else
        {
            detailRow = bodyRows.Count >= 1 ? bodyRows[^1] : null;
            headerRow = bodyRows.Count >= 2 ? bodyRows[0] : null;
        }
        if (detailRow is null)
        {
            return false;
        }

        // Column X edges, anchored at the Tablix's Left. The columns are scaled to fill the Tablix's declared
        // width exactly — the RDL widths act as relative weights, matching how the old TablixElement render
        // (ComputeColumnEdges) fit them into the element's bounds. This keeps the table inside its rectangle
        // (no overflow past the page) regardless of the absolute width sum.
        double widthSumMm = colWidths.Sum(w => w.ToMm());
        double scale = widthSumMm > 0 ? tbBounds.Width.ToMm() / widthSumMm : 1.0;
        var edges = new Unit[colWidths.Count + 1];
        edges[0] = tbBounds.X;
        double accMm = 0;
        for (int c = 0; c < colWidths.Count; c++)
        {
            accMm += colWidths[c].ToMm() * scale;
            edges[c + 1] = tbBounds.X + Unit.FromMm(accMm);
        }
        // Width spanning columns [col, col+span) from the precomputed edges (honours RDL ColSpan).
        Unit SpanW(int col, int span) => edges[col + span] - edges[col];

        if (headerRow is not null)
        {
            var hHeight = ParseSize(Val(headerRow, "Height")) ?? Unit.FromMm(6);
            var hels = new List<ReportElement>();
            int col = 0;
            foreach (var hcell in RowCells(headerRow))
            {
                if (col >= colWidths.Count)
                {
                    break;
                }
                int span = Math.Clamp(ColSpanOf(hcell), 1, colWidths.Count - col);
                var tb = FirstTextbox(hcell);
                if (TextboxValue(tb) is { Length: > 0 } v)
                {
                    hels.Add(new LabelElement
                    {
                        Text = v,
                        Bounds = new Rectangle(edges[col], Unit.Zero, SpanW(col, span), hHeight),
                        Style = tb is null ? Style.Default : ReadStyle(tb) ?? Style.Default,
                    });
                }
                col += span;
            }
            if (hels.Count > 0)
            {
                headerBand = new ReportBand(BandKind.PageHeader, hHeight, new EquatableArray<ReportElement>(hels));
            }
        }

        var dHeight = ParseSize(Val(detailRow, "Height")) ?? Unit.FromMm(6);
        var dels = new List<ReportElement>();
        int dcol = 0;
        foreach (var dcell in RowCells(detailRow))
        {
            if (dcol >= colWidths.Count)
            {
                break;
            }
            int span = Math.Clamp(ColSpanOf(dcell), 1, colWidths.Count - dcol);
            var tb = FirstTextbox(dcell);
            if (TextboxValue(tb) is { Length: > 0 } v)
            {
                dels.Add(new TextBoxElement
                {
                    Expression = RdlExpression.Convert(v),
                    Bounds = new Rectangle(edges[dcol], Unit.Zero, SpanW(dcol, span), dHeight),
                    Style = tb is null ? Style.Default : ReadStyle(tb) ?? Style.Default,
                });
            }
            dcol += span;
        }
        if (dels.Count == 0)
        {
            return false; // nothing renderable in the detail → keep the TablixElement path (which warns)
        }
        detail = new DetailBand(dHeight, new EquatableArray<ReportElement>(dels))
        {
            DataSetName = Val(tablix, "DataSetName"),
            NoRowsMessage = NoRowsOf(tablix),
            PageBreak = ReadPageBreak(tablix),
        };
        return true;
    }

    // RDL <TablixCell><ColSpan> (optional, default 1) — how many columns the cell covers. RowSpan is implicit
    // in RDL (covered cells are omitted from later rows) and not inferred here.
    private static int ColSpanOf(XElement cell)
        => int.TryParse(Val(cell, "ColSpan"), out var n) && n > 1 ? n : 1;

    private static List<XElement> RowCells(XElement tablixRow)
        => (El(tablixRow, "TablixCells")?.Elements().Where(e => e.Name.LocalName == "TablixCell")
            ?? Enumerable.Empty<XElement>()).ToList();

    // Walks a TablixMembers tree (outer→inner), collecting each member that carries a <Group> with a
    // <GroupExpression> into a TablixGroup (with the member's optional first SortExpression). Static
    // members (no Group) are skipped. Nested <TablixMembers> become deeper group levels.
    private static List<TablixGroup> ReadTablixGroups(XElement? members, string prefix)
    {
        var list = new List<TablixGroup>();
        Walk(members);
        return list;

        void Walk(XElement? ms)
        {
            foreach (var m in ms?.Elements().Where(e => e.Name.LocalName == "TablixMember") ?? Enumerable.Empty<XElement>())
            {
                var group = El(m, "Group");
                var expr = Val(El(group, "GroupExpressions"), "GroupExpression");
                if (group is not null && !string.IsNullOrEmpty(expr))
                {
                    var sortEl = El(El(m, "SortExpressions"), "SortExpression");
                    var sortRaw = Val(sortEl, "Value");
                    var sort = string.IsNullOrEmpty(sortRaw) ? null : RdlExpression.Convert(sortRaw);
                    var desc = string.Equals(Val(sortEl, "Direction"), "Descending", StringComparison.OrdinalIgnoreCase);
                    list.Add(new TablixGroup($"{prefix}{list.Count}", RdlExpression.Convert(expr), sort, desc));
                }
                Walk(El(m, "TablixMembers")); // nested member → deeper group level
            }
        }
    }

    private static XElement? FirstTextbox(XElement? scope)
        => scope?.Descendants().FirstOrDefault(e => e.Name.LocalName == "Textbox");
}
