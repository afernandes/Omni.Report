using System.Collections.ObjectModel;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Data;
using Reporting.Geometry;
using Reporting.Paper;
using Reporting.Styling;

namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>Top-level mutable wrapper around a <see cref="ReportDefinition"/>.</summary>
public sealed class ReportDefinitionViewModel : Notifying
{
    public ReportDefinitionViewModel(string name)
    {
        _name = name;
        Bands = new ObservableCollection<BandViewModel>();
        // Seed a minimal set of bands; users can add/remove later via the band strip.
        AddBand(new BandViewModel(DesignerBandKind.PageHeader, Unit.FromMm(8)));
        AddBand(new BandViewModel(DesignerBandKind.Detail,     Unit.FromMm(8)));
        AddBand(new BandViewModel(DesignerBandKind.PageFooter, Unit.FromMm(8)));
    }

    private string _name;
    public string Name { get => _name; set => Set(ref _name, value); }

    private PageSetup _pageSetup = PageSetup.A4Portrait;
    public PageSetup PageSetup { get => _pageSetup; set => Set(ref _pageSetup, value); }

    // Report-level tables preserved opaquely across load→save (no dedicated Designer editor yet), so opening and
    // re-saving a report never silently drops its Metadata (e.g. Language) or its named styles.
    private EquatableDictionary<string, string> _metadata = EquatableDictionary<string, string>.Empty;
    private EquatableDictionary<string, Style> _namedStyles = EquatableDictionary<string, Style>.Empty;

    /// <summary>Names of the report's named styles (sorted), for the PropertyGrid's <c>BasedOn</c> picker.</summary>
    public IReadOnlyList<string> NamedStyleNames => _namedStyles.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();

    public ObservableCollection<BandViewModel> Bands { get; }

    public BandViewModel AddBand(BandViewModel band)
    {
        ArgumentNullException.ThrowIfNull(band);
        band.Changed += RaiseChanged;
        Bands.Add(band);
        RaiseChanged();
        return band;
    }

    public void RemoveBand(BandViewModel band)
    {
        ArgumentNullException.ThrowIfNull(band);
        band.Changed -= RaiseChanged;
        Bands.Remove(band);
        RaiseChanged();
    }

    public BandViewModel? FindBand(DesignerBandKind kind)
        => Bands.FirstOrDefault(b => b.Kind == kind);

    /// <summary>Materialize the mutable VM tree to an immutable <see cref="ReportDefinition"/>.</summary>
    /// <param name="dataSources">Optional designer-side data sources to embed as
    /// <see cref="DataSourceDefinition"/>s in the report. When passed, <c>.repx</c> round-trip
    /// preserves connection / SQL / parameter metadata via <see cref="DesignerDataSource.ToDefinition"/>.</param>
    /// <param name="relations">Optional master-detail relationships between data sources.</param>
    /// <param name="parameters">Optional report parameters to persist on the definition (prompt,
    /// type, default, required, multi-value) so a <c>.repx</c> round-trips them.</param>
    /// <param name="variables">Optional report-level computed variables (RDL <c>&lt;Variables&gt;</c>).</param>
    public ReportDefinition Build(
        IEnumerable<DesignerDataSource>? dataSources = null,
        IEnumerable<DesignerRelation>? relations = null,
        IEnumerable<DesignerParameter>? parameters = null,
        IEnumerable<DesignerVariable>? variables = null)
    {
        // Build the Detail band, then attach any SubDetail bands that appear in the strip
        // immediately AFTER it (and before the first GroupFooter / PageFooter). This mirrors
        // the visual order the user sees in the canvas and matches the paginator's emit order.
        var baseDetail = FindBand(DesignerBandKind.Detail)?.BuildDetailBand() ?? DetailBand.Empty;
        var detailIdx = Bands.ToList().FindIndex(b => b.Kind == DesignerBandKind.Detail);
        var subDetails = new List<SubDetailBand>();
        if (detailIdx >= 0)
        {
            for (int i = detailIdx + 1; i < Bands.Count; i++)
            {
                var b = Bands[i];
                if (b.Kind == DesignerBandKind.SubDetail)
                {
                    subDetails.Add(b.BuildSubDetailBand());
                    continue;
                }
                // Stop collecting at the first non-SubDetail band — sub-bands sit between
                // Detail and the trailing footers.
                if (b.Kind is DesignerBandKind.Detail or DesignerBandKind.SubDetail) continue;
                break;
            }
        }
        var detail = subDetails.Count == 0
            ? baseDetail
            : baseDetail with { SubDetails = new EquatableArray<SubDetailBand>(subDetails) };

        // Groups: pair consecutive GH/GF bands. A GH without a matching GF (or vice-versa)
        // still becomes a GroupBand with the missing side as null.
        var groups = BuildGroups();

        // Build base DataSourceDefinitions, then attach each DesignerRelation to its
        // ParentSource's Relations array (the Core model groups relations per parent source).
        var dsList = dataSources?.Select(d => d.ToDefinition()).ToList() ?? new List<DataSourceDefinition>();
        if (relations is not null)
        {
            var relByParent = relations.GroupBy(r => r.ParentSource, StringComparer.Ordinal);
            for (int i = 0; i < dsList.Count; i++)
            {
                var ds = dsList[i];
                var rels = relByParent.FirstOrDefault(g => string.Equals(g.Key, ds.Name, StringComparison.Ordinal));
                if (rels is null) continue;
                var coreRels = rels.Select(r => new DataRelation(
                    r.Name, r.ParentSource, r.ParentField, r.ChildSource, r.ChildField));
                dsList[i] = ds with { Relations = new EquatableArray<DataRelation>(coreRels) };
            }
        }
        var dsArray = new EquatableArray<DataSourceDefinition>(dsList);

        var paramArray = parameters is null
            ? EquatableArray<Reporting.Parameters.ReportParameter>.Empty
            : new EquatableArray<Reporting.Parameters.ReportParameter>(
                parameters.Select(p => p.ToReportParameter()).ToArray());

        return new ReportDefinition(Name, PageSetup, detail)
        {
            Parameters = paramArray,
            ReportHeader = FindBand(DesignerBandKind.ReportHeader)?.BuildReportBand(BandKind.ReportHeader),
            PageHeader   = FindBand(DesignerBandKind.PageHeader)?.BuildReportBand(BandKind.PageHeader),
            Groups       = groups,
            PageFooter   = FindBand(DesignerBandKind.PageFooter)?.BuildReportBand(BandKind.PageFooter),
            ReportFooter = FindBand(DesignerBandKind.ReportFooter)?.BuildReportBand(BandKind.ReportFooter),
            DataSources  = dsArray,
            Variables    = variables is null
                ? EquatableArray<Reporting.Parameters.ReportVariable>.Empty
                : new EquatableArray<Reporting.Parameters.ReportVariable>(variables.Select(v => v.ToVariable()).ToArray()),
            Metadata     = _metadata,
            NamedStyles  = _namedStyles,
        };
    }

    /// <summary>Extracts the master-detail relations recorded in the report's
    /// <see cref="DataSourceDefinition.Relations"/> arrays back into flat
    /// <see cref="DesignerRelation"/> view-models.</summary>
    public static IReadOnlyList<DesignerRelation> ExtractRelations(ReportDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var list = new List<DesignerRelation>();
        foreach (var ds in definition.DataSources)
        {
            foreach (var r in ds.Relations)
            {
                list.Add(new DesignerRelation(r.Name, r.ParentSource, r.ParentField, r.ChildSource, r.ChildField));
            }
        }
        return list;
    }

    private EquatableArray<GroupBand> BuildGroups()
    {
        var groups = new List<GroupBand>();
        // Walk bands in declared order; whenever we see a GroupHeader, look ahead for
        // the next GroupFooter (until another GH or Detail/PF/RF) and pair them.
        BandViewModel? pendingHeader = null;
        foreach (var b in Bands)
        {
            if (b.Kind == DesignerBandKind.GroupHeader)
            {
                if (pendingHeader is not null)
                {
                    groups.Add(MakeGroup(pendingHeader, null));
                }
                pendingHeader = b;
            }
            else if (b.Kind == DesignerBandKind.GroupFooter)
            {
                groups.Add(MakeGroup(pendingHeader, b));
                pendingHeader = null;
            }
        }
        if (pendingHeader is not null)
        {
            groups.Add(MakeGroup(pendingHeader, null));
        }
        return new EquatableArray<GroupBand>(groups);
    }

    private static GroupBand MakeGroup(BandViewModel? header, BandViewModel? footer)
    {
        var name = header?.GroupName ?? footer?.GroupName ?? "Group";
        var expr = header?.GroupExpression ?? footer?.GroupExpression ?? string.Empty;
        // RDL group-level fields are stored on the GroupHeader band's VM — that's where the
        // PropertyGrid presents them when the user selects a GH. Footer-only groups (rare)
        // fall back to header=null and use the footer's bare values.
        var settings = header ?? footer;
        var sorts = settings is null || settings.SortExpressions.Count == 0
            ? EquatableArray<Reporting.Data.SortDescriptor>.Empty
            : new EquatableArray<Reporting.Data.SortDescriptor>(
                settings.SortExpressions.Select(s => s.ToDescriptor()));
        var vars = settings is null || settings.Variables.Count == 0
            ? EquatableArray<Reporting.Parameters.ReportVariable>.Empty
            : new EquatableArray<Reporting.Parameters.ReportVariable>(
                settings.Variables.Select(v => v.ToVariable()));
        return new GroupBand(name, expr,
            Header: header?.BuildReportBand(BandKind.GroupHeader),
            Footer: footer?.BuildReportBand(BandKind.GroupFooter),
            PageBreak: settings?.PageBreak ?? Reporting.Bands.PageBreak.None,
            FilterExpression: string.IsNullOrWhiteSpace(settings?.FilterExpression) ? null : settings!.FilterExpression,
            SortExpressions: sorts,
            Variables: vars);
    }

    /// <summary>Inverse of <see cref="Build"/> — loads an existing definition into mutable state.</summary>
    public static ReportDefinitionViewModel FromDefinition(ReportDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var vm = new ReportDefinitionViewModel(definition.Name) { PageSetup = definition.PageSetup };
        vm._metadata = definition.Metadata;       // preserved opaquely (no editor yet) so save doesn't drop them
        vm._namedStyles = definition.NamedStyles;
        vm.Bands.Clear();
        if (definition.ReportHeader is { } rh)
        {
            vm.AddBand(BandViewModel.FromReportBand(rh, DesignerBandKind.ReportHeader));
        }
        if (definition.PageHeader is { } ph)
        {
            vm.AddBand(BandViewModel.FromReportBand(ph, DesignerBandKind.PageHeader));
        }
        // Group headers go ABOVE Detail; group footers go BELOW Detail. This keeps the band
        // sequence consistent with the natural layout the user sees in the canvas
        // (RH → PH → GH₁ → GH₂ → … → Detail → GF₂ → GF₁ → PF → RF) — outer group footers
        // are emitted in reverse order, matching the inverse stack at runtime.
        foreach (var grp in definition.Groups)
        {
            if (grp.Header is { } gh)
            {
                var gvm = BandViewModel.FromReportBand(gh, DesignerBandKind.GroupHeader);
                gvm.GroupName = grp.Name;
                gvm.GroupExpression = grp.GroupExpression;
                // Group-level RDL settings (PageBreak/Filter/Sort/Variables) live on the
                // GroupHeader VM so the PropertyGrid surfaces them when GH is selected.
                gvm.PageBreak = grp.PageBreak == Reporting.Bands.PageBreak.None
                    ? grp.EffectivePageBreak() // honour legacy NewPageBefore/After bools
                    : grp.PageBreak;
                gvm.FilterExpression = grp.FilterExpression;
                foreach (var s in grp.SortExpressions)
                {
                    gvm.SortExpressions.Add(SortDescriptorRule.From(s));
                }
                foreach (var v in grp.Variables)
                {
                    gvm.Variables.Add(GroupVariableRule.From(v));
                }
                vm.AddBand(gvm);
            }
        }
        vm.AddBand(BandViewModel.FromDetailBand(definition.Detail));
        // Sub-details land right after the main Detail, in declared order — matches both
        // the paginator's emit order and the natural visual hierarchy.
        foreach (var sub in definition.Detail.SubDetails)
        {
            vm.AddBand(BandViewModel.FromSubDetailBand(sub));
        }
        foreach (var grp in definition.Groups.Reverse())
        {
            if (grp.Footer is { } gf)
            {
                var gvm = BandViewModel.FromReportBand(gf, DesignerBandKind.GroupFooter);
                gvm.GroupName = grp.Name;
                gvm.GroupExpression = grp.GroupExpression;
                vm.AddBand(gvm);
            }
        }
        if (definition.PageFooter is { } pf)
        {
            vm.AddBand(BandViewModel.FromReportBand(pf, DesignerBandKind.PageFooter));
        }
        if (definition.ReportFooter is { } rf)
        {
            vm.AddBand(BandViewModel.FromReportBand(rf, DesignerBandKind.ReportFooter));
        }
        return vm;
    }
}
