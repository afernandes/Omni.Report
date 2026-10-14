using Reporting.Common;
using Reporting.Data;
using Reporting.Elements;
using Reporting.Geometry;

namespace Reporting.Bands;

/// <summary>Identifies the role a band plays in the report layout (header, footer, group, or detail).</summary>
public enum BandKind
{
    /// <summary>Rendered once, before everything else, at the very start of the report.</summary>
    ReportHeader,

    /// <summary>Rendered at the top of every page.</summary>
    PageHeader,

    /// <summary>Rendered before the first row of each group instance.</summary>
    GroupHeader,

    /// <summary>Rendered once per data row — the body of the report.</summary>
    Detail,

    /// <summary>Rendered after the last row of each group instance.</summary>
    GroupFooter,

    /// <summary>Rendered at the bottom of every page.</summary>
    PageFooter,

    /// <summary>Rendered once, after everything else, at the very end of the report.</summary>
    ReportFooter,
}
