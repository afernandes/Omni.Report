using Reporting.Bands;

namespace Reporting.CodeFirst;

/// <summary>Fluent builder for a sub-detail band — fires once per child row of the bound
/// <see cref="DataMember"/> (a relation name or a registered data source).</summary>
/// <remarks>
/// <para>Use via <see cref="ReportBuilderRoot.SubDetail(string, Action{SubDetailBuilder})"/>.
/// The header runs once before the first child, the detail body runs per child row, and the
/// footer runs once after the last child.</para>
/// </remarks>
public sealed class SubDetailBuilder
{
    private readonly string _name;
    private readonly string _dataMember;
    private BandContent? _header;
    private BandContent? _detail;
    private BandContent? _footer;
    private string? _visibleExpression;
    private bool _printIfEmpty;

    internal SubDetailBuilder(string dataMember, string name)
    {
        _dataMember = dataMember;
        _name = name;
    }

    /// <summary>The unique name of this sub-detail band.</summary>
    public string Name => _name;
    /// <summary>The bound data member — a relation name or a registered data source — whose child
    /// rows drive the band.</summary>
    public string DataMember => _dataMember;

    /// <summary>Header band — rendered once before the first child row.</summary>
    public SubDetailBuilder Header(Action<BandContent> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _header ??= new BandContent();
        configure(_header);
        return this;
    }

    /// <summary>Detail body — repeats once per child row. Required.</summary>
    public SubDetailBuilder Detail(Action<BandContent> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _detail ??= new BandContent();
        configure(_detail);
        return this;
    }

    /// <summary>Footer band — rendered once after the last child row.</summary>
    public SubDetailBuilder Footer(Action<BandContent> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _footer ??= new BandContent();
        configure(_footer);
        return this;
    }

    /// <summary>Show the header/footer even when the child source emits zero rows.</summary>
    public SubDetailBuilder PrintIfEmpty(bool value = true)
    {
        _printIfEmpty = value;
        return this;
    }

    /// <summary>Optional boolean expression — when it evaluates to false at runtime, the
    /// sub-band is skipped for that parent row.</summary>
    public SubDetailBuilder VisibleWhen(string expression)
    {
        _visibleExpression = expression;
        return this;
    }

    internal SubDetailBand Build()
    {
        var detail = _detail ?? new BandContent();
        var elements = detail.BuildElements();
        ReportBand? header = _header is null
            ? null
            : new ReportBand(BandKind.Detail, _header.BandHeight, _header.BuildElements());
        ReportBand? footer = _footer is null
            ? null
            : new ReportBand(BandKind.Detail, _footer.BandHeight, _footer.BuildElements());
        return new SubDetailBand(
            Name: _name,
            DataMember: _dataMember,
            Height: detail.BandHeight,
            Elements: elements,
            Header: header,
            Footer: footer,
            Visible: true,
            VisibleExpression: _visibleExpression,
            PrintIfEmpty: _printIfEmpty);
    }
}
