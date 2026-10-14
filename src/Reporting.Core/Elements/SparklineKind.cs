using Reporting.Common;
using Reporting.Geometry;
using Reporting.Metadata;

namespace Reporting.Elements;

/// <summary>Visual form of a <see cref="SparklineElement"/> — a line, column, or filled-area mini-chart.</summary>
public enum SparklineKind
{
    /// <summary>A polyline through the points.</summary>
    Line,

    /// <summary>One column per point.</summary>
    Column,

    /// <summary>A line with the area beneath it filled.</summary>
    Area,
}
