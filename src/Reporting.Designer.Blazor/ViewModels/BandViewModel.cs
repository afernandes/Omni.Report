using System.Collections.ObjectModel;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;

namespace Reporting.Designer.Blazor.ViewModels;

public enum DesignerBandKind
{
    ReportHeader,
    PageHeader,
    GroupHeader,
    Detail,
    /// <summary>Sub-detail (DevExpress DetailReportBand / FastReport sub-band). Visually nested
    /// under the Detail, fires once per child row of <see cref="BandViewModel.DataMember"/>.</summary>
    SubDetail,
    GroupFooter,
    PageFooter,
    ReportFooter,
}

/// <summary>Mutable wrapper around a <see cref="Reporting.Data.SortDescriptor"/> — one row
/// in the PropertyGrid sort-expression list. Expression + direction toggle.</summary>
public sealed class SortDescriptorRule : Notifying
{
    private string _expression = string.Empty;
    public string Expression { get => _expression; set => Set(ref _expression, value); }

    private Reporting.Data.SortDirection _direction = Reporting.Data.SortDirection.Ascending;
    public Reporting.Data.SortDirection Direction { get => _direction; set => Set(ref _direction, value); }

    internal Reporting.Data.SortDescriptor ToDescriptor() => new(Expression, Direction);

    internal static SortDescriptorRule From(Reporting.Data.SortDescriptor d) =>
        new() { Expression = d.Expression, Direction = d.Direction };
}

/// <summary>Mutable wrapper around a group-scoped <see cref="Reporting.Parameters.ReportVariable"/>.
/// The scope is fixed to <see cref="Reporting.Parameters.VariableScope.Group"/> — group-level
/// variables are evaluated once per group instance.</summary>
public sealed class GroupVariableRule : Notifying
{
    private string _name = string.Empty;
    public string Name { get => _name; set => Set(ref _name, value); }

    private string _expression = string.Empty;
    public string Expression { get => _expression; set => Set(ref _expression, value); }

    internal Reporting.Parameters.ReportVariable ToVariable() =>
        new(Name, Expression, Reporting.Parameters.VariableScope.Group);

    internal static GroupVariableRule From(Reporting.Parameters.ReportVariable v) =>
        new() { Name = v.Name, Expression = v.Expression };
}

/// <summary>Mutable wrapper around a report band — owns a list of <see cref="ElementViewModel"/>.</summary>
public sealed class BandViewModel : Notifying
{
    public BandViewModel(DesignerBandKind kind, Unit height)
    {
        Kind = kind;
        _height = height;
    }

    public DesignerBandKind Kind { get; }

    private Unit _height;
    public Unit Height { get => _height; set => Set(ref _height, value); }

    /// <summary>For <see cref="DesignerBandKind.GroupHeader"/> / <see cref="DesignerBandKind.GroupFooter"/>: the
    /// name of the group (e.g. <c>"Cliente"</c>). Ignored for other band kinds.</summary>
    private string? _groupName;
    public string? GroupName { get => _groupName; set => Set(ref _groupName, value); }

    /// <summary>For group bands: the expression that produces the grouping key
    /// (e.g. <c>"Fields.Cliente.Nome"</c>). Ignored for other band kinds.</summary>
    private string? _groupExpression;
    public string? GroupExpression { get => _groupExpression; set => Set(ref _groupExpression, value); }

    /// <summary>For <see cref="DesignerBandKind.SubDetail"/>: the relation name (master-detail)
    /// or registered data source name this sub-band iterates. The paginator resolves the name
    /// against the parent's <c>DataSourceDefinition.Relations</c> first, then falls back to
    /// the global source registry.</summary>
    private string? _dataMember;
    public string? DataMember { get => _dataMember; set => Set(ref _dataMember, value); }

    /// <summary>For <see cref="DesignerBandKind.SubDetail"/>: when true, the sub-band still
    /// renders its header/footer even with zero matching children. Default false (Crystal /
    /// DevExpress behavior).</summary>
    private bool _printIfEmpty;
    public bool PrintIfEmpty { get => _printIfEmpty; set => Set(ref _printIfEmpty, value); }

    // ── RDL Phase 1 extensions ──────────────────────────────────────────────────

    private Reporting.Bands.PageBreak _pageBreak;
    /// <summary>RDL <c>&lt;PageBreak BreakLocation="…"&gt;</c>: unified page-break rule.
    /// Applies to every band kind (the paginator skips it for Detail-non-applicable kinds).</summary>
    public Reporting.Bands.PageBreak PageBreak { get => _pageBreak; set => Set(ref _pageBreak, value); }

    private bool _bandVisible = true;
    /// <summary>RDL <c>&lt;Visibility&gt;&lt;Hidden&gt;</c> (static): when false the whole band is suppressed.</summary>
    public bool BandVisible { get => _bandVisible; set => Set(ref _bandVisible, value); }

    private string? _bandVisibleExpr;
    /// <summary>RDL <c>&lt;Visibility&gt;</c> expression — when set, the band renders only when it
    /// evaluates to true (layered on top of <see cref="BandVisible"/>).</summary>
    public string? BandVisibleExpr { get => _bandVisibleExpr; set => Set(ref _bandVisibleExpr, value); }

    private string? _noRowsMessage;
    /// <summary>RDL <c>&lt;NoRows&gt;</c>: message shown when the bound data set produces
    /// zero rows. Only meaningful for <see cref="DesignerBandKind.Detail"/> and
    /// <see cref="DesignerBandKind.SubDetail"/>.</summary>
    public string? NoRowsMessage { get => _noRowsMessage; set => Set(ref _noRowsMessage, value); }

    private string? _dataSetName;
    /// <summary>Dataset that drives the Detail loop (RDL data region's dataset). When empty, the engine uses
    /// the request's primary source then the first declared source. Distinct from <see cref="DataMember"/>,
    /// which on a Sub-Detail is a relation-or-source name.</summary>
    public string? DataSetName { get => _dataSetName; set => Set(ref _dataSetName, value); }

    private string? _filterExpression;
    /// <summary>RDL <c>&lt;Filters&gt;</c> at this data region: rows whose expression evaluates
    /// false are skipped. Applies to Detail/SubDetail/Group bands.</summary>
    public string? FilterExpression { get => _filterExpression; set => Set(ref _filterExpression, value); }

    /// <summary>RDL <c>&lt;SortExpressions&gt;</c>: ordered list of sort keys applied to
    /// the data region's rows. The PropertyGrid sort editor binds to this collection.</summary>
    public ObservableCollection<SortDescriptorRule> SortExpressions { get; } = new();

    /// <summary>RDL <c>&lt;Variables&gt;</c> at group scope: re-evaluated once per group
    /// instance. Only meaningful for <see cref="DesignerBandKind.GroupHeader"/>.</summary>
    public ObservableCollection<GroupVariableRule> Variables { get; } = new();

    public ObservableCollection<ElementViewModel> Elements { get; } = [];

    public void AddElement(ElementViewModel element)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.Changed += RaiseChanged;
        Elements.Add(element);
        RaiseChanged();
    }

    public void RemoveElement(ElementViewModel element)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.Changed -= RaiseChanged;
        Elements.Remove(element);
        RaiseChanged();
    }

    internal ReportBand BuildReportBand(BandKind targetKind)
        => new(targetKind, Height,
            new EquatableArray<ReportElement>(Elements.Select(e => e.ToElement())),
            Visible: BandVisible,
            VisibleExpression: string.IsNullOrWhiteSpace(BandVisibleExpr) ? null : BandVisibleExpr,
            PageBreak: PageBreak);

    internal DetailBand BuildDetailBand()
    {
        var sorts = SortExpressions.Count == 0
            ? EquatableArray<Reporting.Data.SortDescriptor>.Empty
            : new EquatableArray<Reporting.Data.SortDescriptor>(
                SortExpressions.Select(s => s.ToDescriptor()));
        return new DetailBand(Height,
            new EquatableArray<ReportElement>(Elements.Select(e => e.ToElement())),
            Visible: BandVisible,
            VisibleExpression: string.IsNullOrWhiteSpace(BandVisibleExpr) ? null : BandVisibleExpr,
            NoRowsMessage: string.IsNullOrWhiteSpace(NoRowsMessage) ? null : NoRowsMessage,
            FilterExpression: string.IsNullOrWhiteSpace(FilterExpression) ? null : FilterExpression,
            SortExpressions: sorts,
            PageBreak: PageBreak,
            DataSetName: string.IsNullOrWhiteSpace(DataSetName) ? null : DataSetName);
    }

    /// <summary>Builds the SubDetailBand core record from this designer band. Header/Footer
    /// sub-trees aren't yet exposed on the flat band strip — they're left null for now and
    /// can be added in a follow-up iteration without breaking the format.</summary>
    internal SubDetailBand BuildSubDetailBand()
        => new(
            Name: !string.IsNullOrWhiteSpace(GroupName) ? GroupName : (DataMember ?? "Sub"),
            DataMember: DataMember ?? string.Empty,
            Height: Height,
            Elements: new EquatableArray<ReportElement>(Elements.Select(e => e.ToElement())),
            Header: null,
            Footer: null,
            Visible: BandVisible,
            VisibleExpression: string.IsNullOrWhiteSpace(BandVisibleExpr) ? null : BandVisibleExpr,
            PrintIfEmpty: PrintIfEmpty);

    /// <summary>Loads a <see cref="SubDetailBand"/> into a designer band of kind
    /// <see cref="DesignerBandKind.SubDetail"/>. Header/Footer trees are skipped for now —
    /// they round-trip via the core model but aren't yet editable on the band strip.</summary>
    internal static BandViewModel FromSubDetailBand(SubDetailBand band)
    {
        var vm = new BandViewModel(DesignerBandKind.SubDetail, band.Height)
        {
            DataMember = band.DataMember,
            GroupName = band.Name, // reuse GroupName as the sub-band's display name
            PrintIfEmpty = band.PrintIfEmpty,
            BandVisible = band.Visible,
            BandVisibleExpr = band.VisibleExpression,
        };
        foreach (var e in band.Elements)
        {
            vm.AddElement(ElementViewModel.FromElement(e));
        }
        return vm;
    }

    internal static BandViewModel FromReportBand(ReportBand band, DesignerBandKind kind)
    {
        var vm = new BandViewModel(kind, band.Height)
        {
            PageBreak = band.PageBreak,
            BandVisible = band.Visible,
            BandVisibleExpr = band.VisibleExpression,
        };
        foreach (var e in band.Elements)
        {
            vm.AddElement(ElementViewModel.FromElement(e));
        }
        return vm;
    }

    internal static BandViewModel FromDetailBand(DetailBand band)
    {
        var vm = new BandViewModel(DesignerBandKind.Detail, band.Height)
        {
            BandVisible = band.Visible,
            BandVisibleExpr = band.VisibleExpression,
            NoRowsMessage = band.NoRowsMessage,
            FilterExpression = band.FilterExpression,
            PageBreak = band.PageBreak,
            DataSetName = band.DataSetName,
        };
        foreach (var s in band.SortExpressions)
        {
            vm.SortExpressions.Add(SortDescriptorRule.From(s));
        }
        foreach (var e in band.Elements)
        {
            vm.AddElement(ElementViewModel.FromElement(e));
        }
        return vm;
    }

    public string DisplayLabel => Kind switch
    {
        DesignerBandKind.ReportHeader => "Report Header",
        DesignerBandKind.PageHeader   => "Page Header",
        DesignerBandKind.GroupHeader  => "Group Header",
        DesignerBandKind.Detail       => "Detail",
        DesignerBandKind.SubDetail    => string.IsNullOrEmpty(DataMember)
                                            ? "Sub-Detail"
                                            : $"↳ Sub-Detail · {DataMember}",
        DesignerBandKind.GroupFooter  => "Group Footer",
        DesignerBandKind.PageFooter   => "Page Footer",
        DesignerBandKind.ReportFooter => "Report Footer",
        _ => Kind.ToString(),
    };

    /// <summary>Two-letter monospace code shown in the band strip — matches the mock
    /// (RH/PH/GH/DT/SD/GF/PF/RF).</summary>
    public string ShortCode => Kind switch
    {
        DesignerBandKind.ReportHeader => "RH",
        DesignerBandKind.PageHeader   => "PH",
        DesignerBandKind.GroupHeader  => "GH",
        DesignerBandKind.Detail       => "DT",
        DesignerBandKind.SubDetail    => "SD",
        DesignerBandKind.GroupFooter  => "GF",
        DesignerBandKind.PageFooter   => "PF",
        DesignerBandKind.ReportFooter => "RF",
        _ => Kind.ToString()[..Math.Min(2, Kind.ToString().Length)].ToUpperInvariant(),
    };

    /// <summary>Tone class used by CSS to colour the strip (matches mock <c>t-rh / t-ph / ...</c>).</summary>
    public string ToneClass => Kind switch
    {
        DesignerBandKind.ReportHeader => "t-rh",
        DesignerBandKind.PageHeader   => "t-ph",
        DesignerBandKind.GroupHeader  => "t-gh",
        DesignerBandKind.Detail       => "t-d",
        DesignerBandKind.GroupFooter  => "t-gf",
        DesignerBandKind.PageFooter   => "t-pf",
        DesignerBandKind.ReportFooter => "t-rf",
        _ => string.Empty,
    };
}
