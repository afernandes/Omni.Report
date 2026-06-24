using Reporting.Bands;
using Reporting.Common;
using Reporting.Data;
using Reporting.Paper;
using Reporting.Parameters;
using Reporting.Styling;

namespace Reporting;

/// <summary>The complete, immutable definition of a report.</summary>
/// <remarks>
/// <see cref="ReportDefinition"/> is the canonical AST consumed by the layout engine
/// and produced by the code-first API, the designer, and the serializers. It is fully
/// immutable; structural equality is guaranteed by the record + <see cref="EquatableArray{T}"/>
/// machinery, so two definitions built from the same inputs are <c>Equals</c>.
/// </remarks>
public sealed record ReportDefinition(
    string Name,
    PageSetup PageSetup,
    DetailBand Detail)
{
    public string SchemaVersion { get; init; } = "1.0";

    public EquatableArray<ReportParameter> Parameters { get; init; } = EquatableArray<ReportParameter>.Empty;

    public EquatableArray<DataSourceDefinition> DataSources { get; init; } = EquatableArray<DataSourceDefinition>.Empty;

    public EquatableArray<ReportVariable> Variables { get; init; } = EquatableArray<ReportVariable>.Empty;

    public ReportBand? ReportHeader { get; init; }
    public ReportBand? PageHeader { get; init; }
    public EquatableArray<GroupBand> Groups { get; init; } = EquatableArray<GroupBand>.Empty;
    public ReportBand? PageFooter { get; init; }
    public ReportBand? ReportFooter { get; init; }

    public EquatableDictionary<string, string> Metadata { get; init; } = EquatableDictionary<string, string>.Empty;

    /// <summary>Reusable named styles (SSRS <c>Style[@Name]</c>): a table of styles an element's
    /// <see cref="Style.BasedOn"/> can inherit from. Resolved at render — the named style is the base, the
    /// element's inline <see cref="Style"/> overlays it. A named style may itself have a <see cref="Style.BasedOn"/>
    /// (chained, cycle-guarded at resolution).</summary>
    public EquatableDictionary<string, Style> NamedStyles { get; init; } = EquatableDictionary<string, Style>.Empty;

    /// <summary>Creates a minimal valid definition: A4 portrait, empty detail band.</summary>
    public static ReportDefinition Empty(string name)
        => new(name, PageSetup.A4Portrait, DetailBand.Empty);
}
