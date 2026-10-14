using Reporting.Common;
using Reporting.Geometry;
using Reporting.Metadata;

namespace Reporting.Elements;

/// <summary>A single body cell of a Tablix. <paramref name="ColumnSpan"/>/<paramref name="RowSpan"/> merge the
/// cell across adjacent columns/rows (RDL ColSpan/RowSpan); 1 = a normal 1×1 cell. A merged cell occupies the
/// covered columns/rows, which the renderer then skips.</summary>
public sealed record TablixCell(int RowIndex, int ColumnIndex, ReportElement? Content,
    int ColumnSpan = 1, int RowSpan = 1);
