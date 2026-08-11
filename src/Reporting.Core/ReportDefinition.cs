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
    /// <summary>Version of the report format this definition conforms to. Written to and read from the
    /// serialized file so a future reader can migrate an older document.</summary>
    public string SchemaVersion { get; init; } = "1.0";

    /// <summary>Parameters prompted for (or supplied) before the report runs.</summary>
    public EquatableArray<ReportParameter> Parameters { get; init; } = EquatableArray<ReportParameter>.Empty;

    /// <summary>Declared data sources. The detail band binds to one of them.</summary>
    public EquatableArray<DataSourceDefinition> DataSources { get; init; } = EquatableArray<DataSourceDefinition>.Empty;

    /// <summary>Report-level variables — expressions evaluated once and reusable anywhere.</summary>
    public EquatableArray<ReportVariable> Variables { get; init; } = EquatableArray<ReportVariable>.Empty;

    /// <summary>Band rendered once at the very start. Null means none.</summary>
    public ReportBand? ReportHeader { get; init; }

    /// <summary>Band rendered at the top of every page. Null means none.</summary>
    public ReportBand? PageHeader { get; init; }

    /// <summary>Grouping levels wrapped around the detail band, outermost first.</summary>
    public EquatableArray<GroupBand> Groups { get; init; } = EquatableArray<GroupBand>.Empty;

    /// <summary>Band rendered at the bottom of every page. Null means none.</summary>
    public ReportBand? PageFooter { get; init; }

    /// <summary>Band rendered once at the very end. Null means none.</summary>
    public ReportBand? ReportFooter { get; init; }

    /// <summary>Free-form key/value bag. Carries RDL report-level fields that have no first-class property
    /// (<c>Language</c>, <c>Description</c>, <c>Author</c>, …) so they survive a round-trip.</summary>
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
