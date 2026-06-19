using FluentAssertions;
using Reporting;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Data;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Parameters;
using Reporting.Serialization;
using Xunit;

namespace Reporting.Serialization.Tests;

/// <summary>
/// Parity guard for the <c>.repjson</c> writer/reader. An adversarial audit found that the JSON
/// format silently dropped several fields the XML format already preserved (DetailBand SubDetails —
/// master-detail! — plus per-band/group/datasource sort, filter, variables, page-break and
/// calculated fields). <see cref="AssertRoundTrip"/> asserts full equality through BOTH formats, so
/// any field written-but-not-read (or vice versa) fails the build.
/// </summary>
public class RepJsonParityTests
{
    private static readonly RepxSerializer Repx = new();
    private static readonly RepJsonSerializer RepJson = new();

    [Fact]
    public void Bands_groups_and_datasource_extras_round_trip_in_both_formats()
    {
        var sorts = EquatableArray.Create(new SortDescriptor("Fields.Nome", SortDirection.Descending));
        var tb = new TextBoxElement
        {
            Expression = "Fields.Produto",
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(30), Unit.FromMm(6)),
        };

        // Detail with a master-detail sub-band + sort/filter/no-rows/page-break.
        var sub = new SubDetailBand("Itens", "PedidoItens", Unit.FromMm(6),
            EquatableArray.Create<ReportElement>(tb),
            NoRowsMessage: "Sem itens", FilterExpression: "Fields.Qtd > 0", SortExpressions: sorts,
            PrintIfEmpty: true);
        var detail = new DetailBand(Unit.FromMm(8), EquatableArray<ReportElement>.Empty,
            PageBreak: PageBreak.End, NoRowsMessage: "Vazio", FilterExpression: "Fields.Total > 0",
            SortExpressions: sorts, SubDetails: EquatableArray.Create(sub));

        // Group with filter/sort/variables/page-break.
        var group = new GroupBand("Regiao", "Fields.Regiao",
            Header: new ReportBand(BandKind.GroupHeader, Unit.FromMm(6),
                EquatableArray<ReportElement>.Empty, PageBreak: PageBreak.Start),
            FilterExpression: "Fields.Ativo",
            SortExpressions: sorts,
            Variables: EquatableArray.Create(new ReportVariable("Total", "Sum(Fields.Total)", VariableScope.Group)),
            PageBreak: PageBreak.Between);

        // DataSource with calculated fields + filter + sort.
        var ds = new DataSourceDefinition("Pedidos", null,
            EquatableArray<DataField>.Empty, EquatableArray<DataRelation>.Empty,
            new EquatableDictionary<string, string>(new Dictionary<string, string>()),
            CalculatedFields: EquatableArray.Create(new CalculatedField("Margem", "Fields.Lucro / Fields.Receita")),
            FilterExpression: "Fields.Status = 'OK'",
            SortExpressions: sorts);

        var def = new ReportDefinition("Rich", Paper.PageSetup.A4Portrait, detail)
        {
            Groups = EquatableArray.Create(group),
            DataSources = EquatableArray.Create(ds),
        };

        AssertRoundTrip(def);
    }

    private static void AssertRoundTrip(ReportDefinition original)
    {
        Repx.LoadFromBytes(Repx.SaveToBytes(original)).Should().Be(original);
        RepJson.LoadFromBytes(RepJson.SaveToBytes(original)).Should().Be(original);
    }
}
