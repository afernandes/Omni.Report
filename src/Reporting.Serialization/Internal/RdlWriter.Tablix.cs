using System.Globalization;
using System.Xml.Linq;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;

namespace Reporting.Serialization.Internal;

/// <summary>
/// The <c>&lt;Tablix&gt;</c> half of <see cref="RdlWriter"/>: the matrix projection, the two group
/// hierarchies, and the reconstruction of a flat table from PageHeader + Detail bands.
///
/// <para>Split out of the 1.400-line main file purely for navigability — <c>partial</c> means this is the
/// same class, so nothing about behaviour or accessibility changes. Tablix earns its own file for the same
/// reason it does on the importer side: it is the largest cohesive block and the one most often edited.</para>
/// </summary>
internal static partial class RdlWriter
{
    // A matrix TablixElement carries RowGroups/ColumnGroups + a corner cell (0,0) and a body value cell (1,1).
    // The importer reads exactly those (the corner via <TablixCorner>, the body via the first <Textbox> of
    // <TablixBody>, the groups via the two hierarchies, subtotals via an empty <Group/> sibling). WriteCommon
    // adds the Tablix's Name/Style/Top/Left/Width/Height afterwards (the importer's TablixBounds prefers the
    // literal <Width>/<Height> when present, so bounds round-trip).
    private static XElement WriteTablix(TablixElement tx, List<string> warnings)
    {
        var tablix = new XElement(Rdl + "Tablix");

        var corner = FindCell(tx, 0, 0);
        if (corner?.Content is not null)
        {
            tablix.Add(new XElement(Rdl + "TablixCorner",
                new XElement(Rdl + "TablixCornerRows",
                    new XElement(Rdl + "TablixCornerRow",
                        new XElement(Rdl + "TablixCornerCell", CellContents(corner.Content))))));
        }

        // Body: a single column/row carrying the (1,1) value cell. The importer reads only the first <Textbox>
        // of <TablixBody>; the column/row sizes are structural (bounds come from the literal <Width>/<Height>).
        var body = FindCell(tx, 1, 1);
        var colW = Size(tx.Bounds.Width.Mils > 0 ? tx.Bounds.Width : Unit.FromMm(25));
        var rowH = Size(tx.Bounds.Height.Mils > 0 ? tx.Bounds.Height : Unit.FromMm(6));
        tablix.Add(new XElement(Rdl + "TablixBody",
            new XElement(Rdl + "TablixColumns",
                new XElement(Rdl + "TablixColumn", new XElement(Rdl + "Width", colW))),
            new XElement(Rdl + "TablixRows",
                new XElement(Rdl + "TablixRow",
                    new XElement(Rdl + "Height", rowH),
                    new XElement(Rdl + "TablixCells",
                        new XElement(Rdl + "TablixCell", CellContents(body?.Content)))))));

        tablix.Add(WriteTablixHierarchy("TablixColumnHierarchy", tx.ColumnGroups, tx.ColumnSubtotals, warnings));
        tablix.Add(WriteTablixHierarchy("TablixRowHierarchy", tx.RowGroups, tx.RowSubtotals, warnings));

        if (tx.DataSetName is not null)
        {
            tablix.Add(new XElement(Rdl + "DataSetName", tx.DataSetName));
        }
        if (!string.IsNullOrEmpty(tx.NoRowsMessage))
        {
            // NoRowsMessage is a plain caption in the model (the importer stores it via Convert, never marked
            // as an expression); emit it literally — running it through ValueOf would prepend '=' and re-import
            // would mangle any '&'/'Like' as Concat/Like.
            tablix.Add(new XElement(Rdl + "NoRowsMessage", tx.NoRowsMessage));
        }
        // SubtotalLabel/GrandTotalLabel are preserved losslessly via WriteCustomProperties (called by WriteCommon).
        return tablix;
    }

    // A <CellContents><Textbox> for a Tablix cell: a Label → literal value; a TextBox → "=expr"; plus the
    // content's own <Style> (the importer reads the body cell's style via ReadStyle).
    private static XElement CellContents(ReportElement? content)
    {
        var textbox = content switch
        {
            LabelElement l => Textbox(TextRunValue(l.Text ?? string.Empty)),
            TextBoxElement t => Textbox(TextRunValue(ValueOf(t.Expression))),
            _ => Textbox(TextRunValue(string.Empty)),
        };
        // A cell's <Textbox> also requires @Name (XSD). Use the content's name, else synthesize (importer strips it).
        textbox.SetAttributeValue("Name", content is { Name: { Length: > 0 } cellName }
            ? cellName
            : SyntheticNamePrefix + (content?.Id ?? Guid.NewGuid().ToString("n")));
        var style = content is null ? null : StyleElement(content.Style);
        if (style is not null)
        {
            textbox.Add(style);
        }
        return new XElement(Rdl + "CellContents", textbox);
    }

    // <TablixRowHierarchy>/<TablixColumnHierarchy> from a group list (outer→inner, nested <TablixMembers>).
    // No groups → a single static anchor member. Subtotals → an empty <Group/> sibling at the outer level
    // (exactly what RdlImporter.HasSubtotalMember detects).
    private static XElement WriteTablixHierarchy(string element, EquatableArray<TablixGroup> groups, bool subtotals,
        List<string> warnings)
    {
        var members = new XElement(Rdl + "TablixMembers");
        if (groups.Count == 0)
        {
            members.Add(new XElement(Rdl + "TablixMember")); // static anchor (no dynamic group)
            return new XElement(Rdl + element, members);
        }
        XElement level = members;
        for (var gi = 0; gi < groups.Count; gi++)
        {
            var g = groups[gi];
            XElement member;
            if (string.IsNullOrEmpty(g.GroupExpression))
            {
                // A keyless group is not a dynamic RDL group — an empty <GroupExpression> would be dropped by
                // the importer (and could flip the whole element to the flat-table shape). Emit a static member
                // and warn (the group identity/sort can't round-trip through RDL).
                warnings.Add($"TablixGroup '{g.Name}': sem GroupExpression — exportado como membro estático (não round-trippa como grupo dinâmico).");
                member = new XElement(Rdl + "TablixMember");
            }
            else
            {
                var group = new XElement(Rdl + "Group", new XAttribute("Name", g.Name),
                    new XElement(Rdl + "GroupExpressions",
                        new XElement(Rdl + "GroupExpression", ValueOf(g.GroupExpression))));
                member = new XElement(Rdl + "TablixMember", group);
                if (!string.IsNullOrEmpty(g.SortExpression))
                {
                    var sort = new XElement(Rdl + "SortExpression", new XElement(Rdl + "Value", ValueOf(g.SortExpression)));
                    if (g.SortDescending)
                    {
                        sort.Add(new XElement(Rdl + "Direction", "Descending"));
                    }
                    member.Add(new XElement(Rdl + "SortExpressions", sort));
                }
            }
            level.Add(member);
            // Only nest a child <TablixMembers> when there's an inner group; an empty one is XSD-invalid
            // (TablixMembers requires ≥1 TablixMember).
            if (gi < groups.Count - 1)
            {
                var inner = new XElement(Rdl + "TablixMembers"); // nest the next (inner) group beneath this member
                member.Add(inner);
                level = inner;
            }
        }
        if (subtotals)
        {
            members.Add(new XElement(Rdl + "TablixMember", new XElement(Rdl + "Group"))); // empty = total
        }
        return new XElement(Rdl + element, members);
    }

    private static TablixCell? FindCell(TablixElement tx, int row, int col)
    {
        foreach (var c in tx.Cells)
        {
            if (c.RowIndex == row && c.ColumnIndex == col)
            {
                return c;
            }
        }
        return null;
    }

    // True when the bands can be re-folded into a flat <Tablix> losslessly via the column-boundary grid. The
    // detail/header cells must not OVERLAP and must have positive width (those have no column-grid meaning);
    // a page header, if present, must be a column-header row — all Labels (graphics like a Line/Image mark
    // genuine page chrome, which is preserved as a <PageHeader> instead). The union-boundary grid handles any
    // alignment (ColSpan, leading/trailing/interior gaps, differing extents), so no extent match is required.
    private static bool IsReconstructableFlatTable(ReportBand? pageHeader, DetailBand detail)
    {
        var detailEls = detail.Elements.OrderBy(e => e.Bounds.X.Mils).ToList();
        if (HasOverlapOrZeroWidth(detailEls))
        {
            return false;
        }
        if (pageHeader is null || pageHeader.Elements.Count == 0)
        {
            return true; // detail-only flat table
        }
        var headerEls = pageHeader.Elements.OrderBy(e => e.Bounds.X.Mils).ToList();
        return headerEls.All(e => e is LabelElement) && !HasOverlapOrZeroWidth(headerEls);
    }

    // True if any element (in X-sorted order) has non-positive width or starts before the previous one ends —
    // either makes the elements something other than a clean left-to-right column grid.
    private static bool HasOverlapOrZeroWidth(List<ReportElement> els)
    {
        for (var i = 0; i < els.Count; i++)
        {
            if (els[i].Bounds.Width.Mils <= 0)
            {
                return true;
            }
            if (i > 0 && els[i].Bounds.X.Mils < els[i - 1].Bounds.X.Mils + els[i - 1].Bounds.Width.Mils)
            {
                return true;
            }
        }
        return false;
    }

    // ── Flat-table Tablix — inverse of RdlImporter.TryFlatTablixBands ───────────────
    // The importer decomposes a single flat <Tablix> (no dynamic groups) into a repeating PageHeader band
    // (column-header Labels) + a data-bound DetailBand (one TextBox per column). This rebuilds that one flat
    // <Tablix>. The column grid is the sorted distinct X boundaries (start AND end) of every header+detail
    // element, so a cell spanning several columns becomes a <ColSpan> and an uncovered column an empty cell —
    // ColSpan-merged headers and gap layouts round-trip exactly. The caller suppresses the <PageHeader> section.
    private static XElement WriteFlatTablix(ReportBand? pageHeader, DetailBand detail, List<string> warnings)
    {
        var detailEls = detail.Elements.OrderBy(e => e.Bounds.X.Mils).ToList();
        var headerEls = (pageHeader?.Elements ?? EquatableArray<ReportElement>.Empty)
            .OrderBy(e => e.Bounds.X.Mils).ToList();
        var hasHeader = headerEls.Count > 0;

        // Cluster the X boundaries: independent mm→mil rounding can place the "same" physical boundary 1 mil
        // apart (e.g. 20mm+20mm = 1574 mils vs 40mm = 1575), which would emit a spurious sliver column. Collapse
        // boundaries within EdgeTolerance to one. (Imported tables share exact edges, so nothing merges there.)
        var raw = new List<int>();
        foreach (var e in detailEls.Concat(headerEls))
        {
            raw.Add(e.Bounds.X.Mils);
            raw.Add(e.Bounds.X.Mils + e.Bounds.Width.Mils);
        }
        raw.Sort();
        var edgeList = new List<int>();
        foreach (var v in raw)
        {
            if (edgeList.Count == 0 || v - edgeList[^1] > EdgeTolerance)
            {
                edgeList.Add(v);
            }
        }
        var columnCount = Math.Max(edgeList.Count - 1, 0);

        var columns = new XElement(Rdl + "TablixColumns");
        for (var i = 0; i < columnCount; i++)
        {
            columns.Add(new XElement(Rdl + "TablixColumn",
                new XElement(Rdl + "Width", Size(new Unit(edgeList[i + 1] - edgeList[i])))));
        }
        var rows = new XElement(Rdl + "TablixRows");
        if (hasHeader)
        {
            rows.Add(FlatRow(pageHeader!.Height, headerEls, edgeList));
        }
        rows.Add(FlatRow(detail.Height, detailEls, edgeList));

        var tablix = new XElement(Rdl + "Tablix", new XElement(Rdl + "TablixBody", columns, rows));

        var colMembers = new XElement(Rdl + "TablixMembers");
        for (var i = 0; i < columnCount; i++)
        {
            colMembers.Add(new XElement(Rdl + "TablixMember")); // a static member per column
        }
        tablix.Add(new XElement(Rdl + "TablixColumnHierarchy", colMembers));

        var rowMembers = new XElement(Rdl + "TablixMembers");
        if (hasHeader)
        {
            rowMembers.Add(new XElement(Rdl + "TablixMember")); // static header row
        }
        // A <Group> WITHOUT a <GroupExpression> marks the detail row (El(m,"Group") is not null), and stays
        // out of the dynamic-group count so TryFlatTablixBands still fires.
        rowMembers.Add(new XElement(Rdl + "TablixMember",
            new XElement(Rdl + "Group", new XAttribute("Name", "Details"))));
        tablix.Add(new XElement(Rdl + "TablixRowHierarchy", rowMembers));

        tablix.Add(new XElement(Rdl + "DataSetName", detail.DataSetName));
        if (!string.IsNullOrEmpty(detail.NoRowsMessage))
        {
            tablix.Add(new XElement(Rdl + "NoRowsMessage", detail.NoRowsMessage)); // literal caption
        }
        if (detail.CanGrow || detail.CanShrink || detail.VisibleExpression is not null)
        {
            warnings.Add("Flat-table: CanGrow/CanShrink/VisibleExpression do Detail não são re-emitidos no <Tablix> (hint de render perdido).");
        }
        var pageBreak = WritePageBreak(detail.PageBreak);
        if (pageBreak is not null)
        {
            tablix.Add(pageBreak);
        }

        // Bounds span the whole grid so the import's scale = edges-width / Σcolumn-widths = 1 and the
        // reconstructed column edges land exactly on the original element X positions.
        var left = edgeList.Count > 0 ? new Unit(edgeList[0]) : Unit.Zero;
        var width = edgeList.Count > 0 ? new Unit(edgeList[^1] - edgeList[0]) : Unit.Zero;
        var height = (hasHeader ? pageHeader!.Height : Unit.Zero) + detail.Height;
        tablix.Add(new XElement(Rdl + "Top", Size(Unit.Zero)),
            new XElement(Rdl + "Left", Size(left)),
            new XElement(Rdl + "Width", Size(width)),
            new XElement(Rdl + "Height", Size(height)));
        return tablix;
    }

    // One <TablixRow> over the column grid: each element occupies the columns from its left edge to its right
    // edge (emitted with <ColSpan> when it covers more than one); a column no element starts on becomes an
    // empty placeholder cell. This is the inverse of the importer's edge/ColSpan walk (which drops empty cells
    // but still advances the column index).
    private static XElement FlatRow(Unit height, List<ReportElement> els, List<int> edges)
    {
        var tablixCells = new XElement(Rdl + "TablixCells");
        var columnCount = Math.Max(edges.Count - 1, 0);
        var col = 0;
        var elIdx = 0;
        while (col < columnCount)
        {
            if (elIdx < els.Count && ColumnOf(edges, els[elIdx].Bounds.X.Mils) == col)
            {
                var el = els[elIdx++];
                var endCol = ColumnOf(edges, el.Bounds.X.Mils + el.Bounds.Width.Mils);
                var span = Math.Max(endCol - col, 1);
                var cell = new XElement(Rdl + "TablixCell", CellContents(el));
                if (span > 1)
                {
                    cell.Add(new XElement(Rdl + "ColSpan", span.ToString(CultureInfo.InvariantCulture)));
                }
                tablixCells.Add(cell);
                col += span;
            }
            else
            {
                tablixCells.Add(new XElement(Rdl + "TablixCell", CellContents(null))); // gap → empty cell
                col++;
            }
        }
        return new XElement(Rdl + "TablixRow", new XElement(Rdl + "Height", Size(height)), tablixCells);
    }

    // Index of the clustered grid edge nearest to a mils value (within EdgeTolerance), so an element whose edge
    // was snapped during clustering still maps to its column. -1 if none (degenerate input).
    private static int ColumnOf(List<int> edges, int mils)
    {
        for (var i = 0; i < edges.Count; i++)
        {
            if (Math.Abs(edges[i] - mils) <= EdgeTolerance)
            {
                return i;
            }
        }
        return -1;
    }

    private const int EdgeTolerance = 2; // mils (≈0.05mm) — collapses mm→mil rounding noise between bands

}
