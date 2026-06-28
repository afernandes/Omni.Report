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

    /// <summary>Creates a builder for a group identified by <paramref name="name"/> and broken
    /// by <paramref name="groupExpression"/> (a new group instance starts whenever the
    /// expression's value changes).</summary>
    /// <param name="name">Unique name of the group.</param>
    /// <param name="groupExpression">Expression whose value defines the group boundaries.</param>
    public GroupBuilder(string name, string groupExpression)
    {
        _name = name;
        _groupExpression = groupExpression;
    }

    /// <summary>Whether the whole group instance is kept on a single page when possible.</summary>
    public bool KeepTogether { get; private set; }
    /// <summary>Whether each group instance starts on a new page.</summary>
    public bool NewPageBefore { get; private set; }
    /// <summary>Whether a page break is forced after each group instance.</summary>
    public bool NewPageAfter { get; private set; }
    /// <summary>Whether the group header is reprinted at the top of every page the group spans.</summary>
    public bool RepeatHeaderOnNewPage { get; private set; }
    /// <summary>Unified RDL page-break rule for the group; overrides the legacy
    /// <see cref="NewPageBefore"/>/<see cref="NewPageAfter"/> bools when non-None.</summary>
    public PageBreak GroupPageBreak { get; private set; }
    /// <summary>Filter expression that restricts which group instances are emitted, or null.</summary>
    public string? FilterExpression { get; private set; }
    private readonly List<Reporting.Data.SortDescriptor> _sorts = [];
    private readonly List<Reporting.Parameters.ReportVariable> _variables = [];

    /// <summary>Configures the group header band, rendered once before the group's rows.</summary>
    /// <returns>The same builder, for chaining.</returns>
    public GroupBuilder Header(Action<BandContent> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var content = new BandContent();
        configure(content);
        _header = content;
        return this;
    }

    /// <summary>Configures the report-level detail band described inside this group (OmniReport
    /// models a single shared detail per report).</summary>
    /// <returns>The same builder, for chaining.</returns>
    public GroupBuilder Detail(Action<BandContent> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var content = new BandContent();
        configure(content);
        _detail = content;
        return this;
    }

    /// <summary>Configures the group footer band, rendered once after the group's rows.</summary>
    /// <returns>The same builder, for chaining.</returns>
    public GroupBuilder Footer(Action<BandContent> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var content = new BandContent();
        configure(content);
        _footer = content;
        return this;
    }

    /// <summary>Keeps the whole group instance on a single page when it fits.</summary>
    /// <returns>The same builder, for chaining.</returns>
    public GroupBuilder KeepGroupTogether(bool value = true) { KeepTogether = value; return this; }
    /// <summary>Forces each group instance to begin on a new page.</summary>
    /// <returns>The same builder, for chaining.</returns>
    public GroupBuilder StartOnNewPage(bool value = true) { NewPageBefore = value; return this; }
    /// <summary>Forces a page break after each group instance.</summary>
    /// <returns>The same builder, for chaining.</returns>
    public GroupBuilder EndOnNewPage(bool value = true) { NewPageAfter = value; return this; }
    /// <summary>Reprints the group header at the top of every page the group spans.</summary>
    /// <returns>The same builder, for chaining.</returns>
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
