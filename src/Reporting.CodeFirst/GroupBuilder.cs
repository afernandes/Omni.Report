using Reporting.Bands;
using Reporting.Common;
using Reporting.Elements;

namespace Reporting.CodeFirst;

/// <summary>Fluent builder for a <see cref="GroupBand"/>. Captures the group's header, footer
/// and (because OmniReport models a single shared detail per report) the report-level detail
/// described inside the group.</summary>
public sealed class GroupBuilder
{
    private readonly string _name;
    private readonly string _groupExpression;
    private BandContent? _header;
    private BandContent? _footer;
    private BandContent? _detail;

    public GroupBuilder(string name, string groupExpression)
    {
        _name = name;
        _groupExpression = groupExpression;
    }

    public bool KeepTogether { get; private set; }
    public bool NewPageBefore { get; private set; }
    public bool NewPageAfter { get; private set; }
    public bool RepeatHeaderOnNewPage { get; private set; }
    public PageBreak GroupPageBreak { get; private set; }
    public string? FilterExpression { get; private set; }
    private readonly List<Reporting.Data.SortDescriptor> _sorts = [];
    private readonly List<Reporting.Parameters.ReportVariable> _variables = [];

    public GroupBuilder Header(Action<BandContent> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var content = new BandContent();
        configure(content);
        _header = content;
        return this;
    }

    public GroupBuilder Detail(Action<BandContent> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var content = new BandContent();
        configure(content);
        _detail = content;
        return this;
    }

    public GroupBuilder Footer(Action<BandContent> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var content = new BandContent();
        configure(content);
        _footer = content;
        return this;
    }

    public GroupBuilder KeepGroupTogether(bool value = true) { KeepTogether = value; return this; }
    public GroupBuilder StartOnNewPage(bool value = true) { NewPageBefore = value; return this; }
    public GroupBuilder EndOnNewPage(bool value = true) { NewPageAfter = value; return this; }
    public GroupBuilder RepeatHeader(bool value = true) { RepeatHeaderOnNewPage = value; return this; }

    /// <summary>RDL <c>&lt;PageBreak BreakLocation="..."&gt;</c> — unified break control for
    /// the group. Overrides the legacy <see cref="StartOnNewPage"/>/<see cref="EndOnNewPage"/>
    /// bools when set to a non-None value.</summary>
    public GroupBuilder PageBreak(Reporting.Bands.PageBreak rule) { GroupPageBreak = rule; return this; }

    /// <summary>RDL <c>&lt;Filters&gt;</c>: only group instances whose rows pass the predicate
    /// emit a header/detail/footer pair. Filter is evaluated per group instance.</summary>
    public GroupBuilder Filter(string expression) { FilterExpression = expression; return this; }

    /// <summary>RDL <c>&lt;SortExpressions&gt;</c>: stable-sort group instances by one or more
    /// expressions before emission.</summary>
    public GroupBuilder SortBy(string expression, Reporting.Data.SortDirection direction = Reporting.Data.SortDirection.Ascending)
    {
        _sorts.Add(new Reporting.Data.SortDescriptor(expression, direction));
        return this;
    }

    /// <summary>RDL <c>&lt;Variables&gt;</c> at group scope: declares variables that are
    /// re-evaluated once per group instance and accessible via <c>Variables.{Name}</c>.</summary>
    public GroupBuilder Variable(string name, string expression)
    {
        _variables.Add(new Reporting.Parameters.ReportVariable(name, expression, Reporting.Parameters.VariableScope.Group));
        return this;
    }

    internal GroupBand BuildGroupBand()
        => new(_name, _groupExpression,
            Header: _header is null ? null : BuildReportBand(_header, BandKind.GroupHeader),
            Footer: _footer is null ? null : BuildReportBand(_footer, BandKind.GroupFooter),
            KeepTogether: KeepTogether,
            NewPageBefore: NewPageBefore,
            NewPageAfter: NewPageAfter,
            RepeatHeaderOnNewPage: RepeatHeaderOnNewPage,
            PageBreak: GroupPageBreak,
            FilterExpression: FilterExpression,
            SortExpressions: new EquatableArray<Reporting.Data.SortDescriptor>(_sorts),
            Variables: new EquatableArray<Reporting.Parameters.ReportVariable>(_variables));

    internal DetailBand? BuildDetail()
    {
        if (_detail is null)
        {
            return null;
        }
        var elements = _detail.BuildElements();
        return new DetailBand(_detail.BandHeight, elements, CanGrow: _detail.DetailCanGrow, CanShrink: _detail.DetailCanShrink, VisibleExpression: _detail.VisibleExpression);
    }

    internal static ReportBand BuildReportBand(BandContent content, BandKind kind)
    {
        var elements = content.BuildElements();
        return new ReportBand(kind, content.BandHeight, elements,
            Visible: true, VisibleExpression: content.VisibleExpression,
            PrintOnFirstPage: content.PrintOnFirstPage, PrintOnLastPage: content.PrintOnLastPage,
            PageBreak: content.PageBreakRule);
    }
}
