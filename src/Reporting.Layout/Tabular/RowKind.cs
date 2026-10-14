using System.Globalization;
using System.Text.RegularExpressions;
using Reporting.Geometry;
using Reporting.Layout.Primitives;

namespace Reporting.Layout.Tabular;

/// <summary>Semantic classification assigned to each row by the grid quantizer.</summary>
public enum RowKind
{
    /// <summary>The first row, typically column titles.</summary>
    Header,
    /// <summary>A regular data row.</summary>
    Detail,
    /// <summary>A single-column wide row that looks like a group title (no numbers, &gt; 3 chars).</summary>
    GroupHeader,
    /// <summary>A row containing "subtotal" / "sum" keywords, not the last row.</summary>
    Subtotal,
    /// <summary>A row containing "total" keywords or the last "subtotal" row.</summary>
    Total,
}
