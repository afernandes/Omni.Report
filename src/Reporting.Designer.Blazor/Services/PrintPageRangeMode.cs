using Reporting.Geometry;
using Reporting.Paper;

namespace Reporting.Designer.Blazor.Services;

/// <summary>How the user wants page numbers narrowed for printing.</summary>
public enum PrintPageRangeMode
{
    /// <summary>Every page in the rendered report.</summary>
    All,
    /// <summary>Just the page the viewer is currently showing.</summary>
    Current,
    /// <summary>An ad-hoc text range like "1-3,5,7-9".</summary>
    Range,
}
