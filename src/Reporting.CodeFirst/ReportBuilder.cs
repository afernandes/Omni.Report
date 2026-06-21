using System.Linq.Expressions;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Data;
using Reporting.DataSources;
using Reporting.DataSources.Enumerable;
using Reporting.Parameters;

namespace Reporting.CodeFirst;

/// <summary>Entry point of the fluent code-first API. Chains description of a report and
/// produces a <see cref="Report"/> bound to an internal data source registry.</summary>
public static class ReportBuilder
{
    public static ReportBuilderRoot Create(string name) => new(name);
}

/// <summary>The fluent root returned by <see cref="ReportBuilder.Create"/>.</summary>
public sealed class ReportBuilderRoot
{
    private readonly string _name;
    private readonly PageSetupBuilder _pageSetup = new();
    private readonly ParameterListBuilder _parameters = new();
    private readonly DataSourceRegistry _dataSources = new();
    private readonly List<DataSourceDefinition> _dataSourceDefinitions = [];
    private readonly List<GroupBuilder> _groups = [];
    private readonly Dictionary<string, string> _metadata = [];
    private readonly List<Reporting.Parameters.ReportVariable> _variables = [];

    private BandContent? _reportHeader;
    private BandContent? _pageHeader;
    private BandContent? _detail;
    private BandContent? _pageFooter;
    private BandContent? _reportFooter;
    private readonly List<SubDetailBuilder> _subDetails = [];
    private string? _detailNoRows;
    private string? _detailFilter;
    private readonly List<Reporting.Data.SortDescriptor> _detailSorts = [];
    private PageBreak _detailPageBreak;

    internal ReportBuilderRoot(string name)
    {
        _name = name;
    }

    public ReportBuilderRoot Page(Action<PageSetupBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(_pageSetup);
        return this;
    }

    public ReportBuilderRoot Parameters(Action<ParameterListBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(_parameters);
        return this;
    }

    /// <summary>Declares a report-level computed variable (RDL <c>&lt;Variables&gt;</c>) — evaluated once
    /// per report (default) and referenced in expressions as <c>Variables.&lt;Name&gt;</c>.</summary>
    public ReportBuilderRoot Variable(string name, string expression,
        Reporting.Parameters.VariableScope scope = Reporting.Parameters.VariableScope.Report)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _variables.Add(new Reporting.Parameters.ReportVariable(name, expression, scope));
        return this;
    }

    /// <summary>Registers a typed in-memory data source and exposes its top-level fields
    /// to band expressions as <c>Fields.&lt;PropertyName&gt;</c>.</summary>
    public ReportBuilderRoot DataSource<T>(string name, IEnumerable<T> items)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(items);
        var ds = new EnumerableDataSource<T>(name, items);
        _dataSources.Register(ds);
        _dataSourceDefinitions.Add(new DataSourceDefinition(name,
            Fields: new EquatableArray<DataField>(ds.Schema.Fields.Select(f => new DataField(f.Name, f.Type)))));
        return this;
    }

    /// <summary>Registers an arbitrary <see cref="IReportDataSource"/> — e.g. database-backed,
    /// streaming, or remote. Use this for ADO.NET sources (PostgreSQL, SQL Server, SQLite),
    /// HTTP APIs, or any custom provider.</summary>
    /// <remarks>
    /// <para>When the source's <see cref="IReportDataSource.Schema"/> is empty at register time
    /// (typical for lazy-schema DB sources where the query hasn't run yet), pass
    /// <paramref name="fields"/> explicitly so designer field-binding and validation work. If
    /// omitted and the schema is empty, the report's <c>Fields.*</c> tokens won't be statically
    /// validated — they'll still resolve at render time, just without design-time hints.</para>
    /// </remarks>
    public ReportBuilderRoot DataSource(string name, IReportDataSource source,
        IEnumerable<DataField>? fields = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(source);
        _dataSources.Register(source);
        var declared = fields ?? source.Schema.Fields.Select(f => new DataField(f.Name, f.Type));
        _dataSourceDefinitions.Add(new DataSourceDefinition(name,
            Fields: new EquatableArray<DataField>(declared)));
        return this;
    }

    public ReportBuilderRoot Metadata(string key, string value)
    {
        _metadata[key] = value;
        return this;
    }

    public ReportBuilderRoot ReportHeader(Action<BandContent> configure)
        => Configure(ref _reportHeader, configure);

    public ReportBuilderRoot PageHeader(Action<BandContent> configure)
        => Configure(ref _pageHeader, configure);

    public ReportBuilderRoot Detail(Action<BandContent> configure)
        => Configure(ref _detail, configure);

    /// <summary>RDL <c>&lt;NoRows&gt;</c>: message rendered centered in the Detail position
    /// when the bound data source produces zero rows (after filter).</summary>
    public ReportBuilderRoot DetailNoRows(string message) { _detailNoRows = message; return this; }

    /// <summary>RDL <c>&lt;Filters&gt;</c> at the Detail data region: boolean expression
    /// evaluated per row; non-matching rows are skipped.</summary>
    public ReportBuilderRoot DetailFilter(string expression) { _detailFilter = expression; return this; }

    /// <summary>RDL <c>&lt;SortExpressions&gt;</c> at the Detail data region: stable composite
    /// sort applied to iteration rows.</summary>
    public ReportBuilderRoot DetailSortBy(string expression,
        Reporting.Data.SortDirection direction = Reporting.Data.SortDirection.Ascending)
    {
        _detailSorts.Add(new Reporting.Data.SortDescriptor(expression, direction));
        return this;
    }

    /// <summary>RDL <c>&lt;PageBreak&gt;</c> rule on the Detail data region.</summary>
    public ReportBuilderRoot DetailPageBreak(PageBreak rule) { _detailPageBreak = rule; return this; }

    /// <summary>RDL <c>&lt;CalculatedField&gt;</c>: adds a virtual field whose value is
    /// computed per row from the expression. Exposed at runtime as <c>Fields.{name}</c>.
    /// Attached to the most recently registered data source.</summary>
    public ReportBuilderRoot CalculatedField(string name, string expression, Type? resultType = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        if (_dataSourceDefinitions.Count == 0)
        {
            throw new InvalidOperationException(
                "CalculatedField requires a registered data source — call .DataSource(...) first.");
        }
        var last = _dataSourceDefinitions[^1];
        var updated = last with
        {
            CalculatedFields = new EquatableArray<Reporting.Data.CalculatedField>(
                last.CalculatedFields.Concat([new Reporting.Data.CalculatedField(name, expression, resultType)])),
        };
        _dataSourceDefinitions[^1] = updated;
        return this;
    }

    /// <summary>Sets a filter on the most recently registered data source. Mirrors RDL
    /// <c>&lt;Filters&gt;</c> on a <c>&lt;DataSet&gt;</c>.</summary>
    public ReportBuilderRoot DataSourceFilter(string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        if (_dataSourceDefinitions.Count == 0) throw new InvalidOperationException("Call .DataSource first.");
        _dataSourceDefinitions[^1] = _dataSourceDefinitions[^1] with { FilterExpression = expression };
        return this;
    }

    /// <summary>Adds a sort to the most recently registered data source.</summary>
    public ReportBuilderRoot DataSourceSortBy(string expression,
        Reporting.Data.SortDirection direction = Reporting.Data.SortDirection.Ascending)
    {
        if (_dataSourceDefinitions.Count == 0) throw new InvalidOperationException("Call .DataSource first.");
        var ds = _dataSourceDefinitions[^1];
        _dataSourceDefinitions[^1] = ds with
        {
            SortExpressions = new EquatableArray<Reporting.Data.SortDescriptor>(
                ds.SortExpressions.Concat([new Reporting.Data.SortDescriptor(expression, direction)])),
        };
        return this;
    }

    /// <summary>Declares a sub-detail band — fires once per child row of
    /// <paramref name="dataMember"/> (a relation name on the primary source, or a registered
    /// data source name). Mirrors DevExpress XtraReports <c>DetailReportBand</c> / FastReport
    /// sub-band: parent Detail still fires once per parent, then each declared sub-detail
    /// loops its own child source.</summary>
    /// <example>
    /// <code>
    /// .DataSource("Clientes", clientes)
    /// .DataSource("Pedidos", pedidos)
    /// .Relation("PedidosDoCliente", parent: "Clientes", parentField: "Id",
    ///                                child: "Pedidos",  childField: "ClienteId")
    /// .Detail(d => d.Text("{Fields.Nome}").At(0, 0).Size(80, 6))
    /// .SubDetail("PedidosDoCliente", sub => sub
    ///     .Header(h => h.Label("Pedidos:"))
    ///     .Detail(s => s.Height(5)
    ///         .Text("  - {Fields.produto}").At(10, 0).Size(80, 5))
    ///     .Footer(f => f.Text("Subtotal: {Sum(Fields.total)}")))
    /// </code>
    /// </example>
    public ReportBuilderRoot SubDetail(string dataMember, Action<SubDetailBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataMember);
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new SubDetailBuilder(dataMember, name: dataMember);
        configure(builder);
        _subDetails.Add(builder);
        return this;
    }

    /// <summary>Same as <see cref="SubDetail(string, Action{SubDetailBuilder})"/> but with an
    /// explicit logical name (useful when multiple sub-bands share the same data member).</summary>
    public ReportBuilderRoot SubDetail(string name, string dataMember, Action<SubDetailBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataMember);
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new SubDetailBuilder(dataMember, name);
        configure(builder);
        _subDetails.Add(builder);
        return this;
    }

    /// <summary>Declares a master-detail relation between two registered data sources. The
    /// paginator + designer use it to filter children automatically when a sub-band references
    /// the relation by name.</summary>
    public ReportBuilderRoot Relation(string name, string parent, string parentField, string child, string childField)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(parent);
        ArgumentException.ThrowIfNullOrWhiteSpace(parentField);
        ArgumentException.ThrowIfNullOrWhiteSpace(child);
        ArgumentException.ThrowIfNullOrWhiteSpace(childField);
        // Attach to the parent's DataSourceDefinition so it round-trips through .repx.
        for (int i = 0; i < _dataSourceDefinitions.Count; i++)
        {
            var ds = _dataSourceDefinitions[i];
            if (!string.Equals(ds.Name, parent, StringComparison.Ordinal)) continue;
            var relations = ds.Relations.Concat([
                new DataRelation(name, parent, parentField, child, childField)
            ]);
            _dataSourceDefinitions[i] = ds with { Relations = new EquatableArray<DataRelation>(relations) };
            return this;
        }
        throw new InvalidOperationException(
            $"Cannot create relation '{name}': data source '{parent}' has not been registered yet. " +
            "Call .DataSource(...) for both parent and child before declaring the relation.");
    }

    public ReportBuilderRoot PageFooter(Action<BandContent> configure)
        => Configure(ref _pageFooter, configure);

    public ReportBuilderRoot ReportFooter(Action<BandContent> configure)
        => Configure(ref _reportFooter, configure);

    /// <summary>Adds a group with a string expression as the key.</summary>
    public ReportBuilderRoot Group(string name, string groupExpression, Action<GroupBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(groupExpression);
        ArgumentNullException.ThrowIfNull(configure);
        var group = new GroupBuilder(name, groupExpression);
        configure(group);
        _groups.Add(group);
        return this;
    }

    /// <summary>Adds a group whose key is derived from a typed lambda
    /// (<c>v =&gt; v.Cliente.Nome</c>). Converts the lambda to a <c>Fields.&lt;path&gt;</c> expression.</summary>
    public ReportBuilderRoot Group<T>(string name, Expression<Func<T, object>> selector, Action<GroupBuilder> configure)
        => Group(name, FieldPathBuilder.From(selector), configure);

    public Report Build()
    {
        // The Detail at the report level may also have been described inside a group.
        var detailFromGroup = _groups
            .Select(g => g.BuildDetail())
            .FirstOrDefault(d => d is not null);
        // Materialize sub-details once so they can be embedded into either the report-level
        // Detail or the group-level Detail (rare, but the API allows both).
        var subDetails = _subDetails.Count == 0
            ? EquatableArray<SubDetailBand>.Empty
            : new EquatableArray<SubDetailBand>(_subDetails.Select(b => b.Build()));

        var sorts = new EquatableArray<Reporting.Data.SortDescriptor>(_detailSorts);
        var detail = _detail is null
            ? (detailFromGroup is null
                ? DetailBand.Empty with
                {
                    NoRowsMessage = _detailNoRows,
                    FilterExpression = _detailFilter,
                    SortExpressions = sorts,
                    PageBreak = _detailPageBreak,
                }
                : detailFromGroup with
                {
                    SubDetails = subDetails,
                    NoRowsMessage = _detailNoRows,
                    FilterExpression = _detailFilter,
                    SortExpressions = sorts,
                    PageBreak = _detailPageBreak,
                })
            : new DetailBand(
                _detail.BandHeight, _detail.BuildElements(),
                CanGrow: _detail.DetailCanGrow, CanShrink: _detail.DetailCanShrink,
                VisibleExpression: _detail.VisibleExpression,
                SubDetails: subDetails,
                NoRowsMessage: _detailNoRows,
                FilterExpression: _detailFilter,
                SortExpressions: sorts,
                PageBreak: _detailPageBreak);

        var definition = new ReportDefinition(_name, _pageSetup.Build(), detail)
        {
            Parameters = _parameters.Build(),
            DataSources = new EquatableArray<DataSourceDefinition>(_dataSourceDefinitions),
            Groups = new EquatableArray<GroupBand>(_groups.Select(g => g.BuildGroupBand())),
            ReportHeader = _reportHeader is null ? null : GroupBuilder.BuildReportBand(_reportHeader, BandKind.ReportHeader),
            PageHeader = _pageHeader is null ? null : GroupBuilder.BuildReportBand(_pageHeader, BandKind.PageHeader),
            PageFooter = _pageFooter is null ? null : GroupBuilder.BuildReportBand(_pageFooter, BandKind.PageFooter),
            ReportFooter = _reportFooter is null ? null : GroupBuilder.BuildReportBand(_reportFooter, BandKind.ReportFooter),
            Metadata = new EquatableDictionary<string, string>(_metadata),
            Variables = new EquatableArray<Reporting.Parameters.ReportVariable>(_variables),
        };
        return new Report(definition, _dataSources);
    }

    private ReportBuilderRoot Configure(ref BandContent? slot, Action<BandContent> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var content = new BandContent();
        configure(content);
        slot = content;
        return this;
    }
}
