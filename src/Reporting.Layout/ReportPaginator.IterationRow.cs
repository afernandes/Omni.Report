using Reporting.Elements;
using System.Text.RegularExpressions;
using Reporting.Bands;
using Reporting.Common;
using Reporting.DataSources;
using Reporting.Expressions;
using Reporting.Geometry;
using Reporting.Layout.Internal;
using Reporting.Layout.Primitives;
using Reporting.Rendering;

namespace Reporting.Layout;

public sealed partial class ReportPaginator
{

    /// <summary>One row of the Detail iteration. <see cref="Fields"/> is the "live" row (sets
    /// the unqualified <c>Fields.X</c> resolution); <see cref="SourceRows"/> carries the
    /// per-source current rows used for qualified <c>Fields.SourceName.X</c> references —
    /// in master-detail iteration this includes both the parent and the child rows.</summary>
    private sealed record IterationRow(
        IReadOnlyList<KeyValuePair<string, object?>> Fields,
        IReadOnlyDictionary<string, IReadOnlyList<KeyValuePair<string, object?>>>? SourceRows = null,
        object?[]? GroupInstances = null);
}
