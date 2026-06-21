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
/// End-to-end round-trip tests for the RDL-compatibility Phase 1 features:
/// ElementAction (Hyperlink / BookmarkLink / Drillthrough), Bookmark + DocumentMapLabel,
/// PageBreak on bands and groups, NoRowsMessage on data regions, CalculatedField on data
/// sources, FilterExpression + SortExpressions at every level, and group Variables. Each
/// test builds a definition that exercises one RDL feature, saves it through the .repx
/// canonical serializer, loads it back, and asserts structural equality — proving the
/// feature survives a save/load cycle lossless.
/// </summary>
public class RdlFeaturesRoundTripTests
{
    private static readonly RepxSerializer Serializer = new();

    [Fact]
    public void Hyperlink_action_round_trips()
    {
        var definition = WithFirstDetailElementMutated(MinimalReport(), e => e with
        {
            Action = ElementAction.ToUrl("https://example.com/{Fields.Id}"),
        });
        AssertRoundTrip(definition);
    }

    [Fact]
    public void Bookmark_link_action_round_trips()
    {
        var definition = WithFirstDetailElementMutated(MinimalReport(), e => e with
        {
            Action = ElementAction.ToBookmark("anchor-totals"),
        });
        AssertRoundTrip(definition);
    }

    [Fact]
    public void Drillthrough_action_with_parameters_round_trips()
    {
        var definition = WithFirstDetailElementMutated(MinimalReport(), e => e with
        {
            Action = ElementAction.ToDrillthrough("DetalhesPedido",
                new DrillthroughParameter("PedidoId", "{Fields.Id}"),
                new DrillthroughParameter("AnoFiscal", "{Parameters.Ano}", Omit: true)),
        });
        AssertRoundTrip(definition);
    }

    [Fact]
    public void Bookmark_and_document_map_label_round_trip()
    {
        var definition = WithFirstDetailElementMutated(MinimalReport(), e => e with
        {
            Bookmark = "row-totals",
            DocumentMapLabel = "Total geral",
        });
        AssertRoundTrip(definition);
    }

    [Fact]
    public void ToggleItem_and_initially_hidden_round_trip()
    {
        var definition = WithFirstDetailElementMutated(MinimalReport(), e => e with
        {
            ToggleItemId = "expand-control",
            InitiallyHidden = true,
        });
        AssertRoundTrip(definition);
    }

    [Theory]
    [InlineData(PageBreak.Start)]
    [InlineData(PageBreak.End)]
    [InlineData(PageBreak.StartAndEnd)]
    public void Detail_page_break_round_trips(PageBreak rule)
    {
        var def = MinimalReport();
        var detailWithBreak = def.Detail with { PageBreak = rule };
        var definition = def with { Detail = detailWithBreak };
        AssertRoundTrip(definition);
    }

    [Theory]
    [InlineData(PageBreak.Between)]
    [InlineData(PageBreak.Start)]
    public void Group_page_break_round_trips(PageBreak rule)
    {
        var def = MinimalReport();
        var group = new GroupBand("g1", "{Fields.Cliente}", PageBreak: rule);
        var definition = def with { Groups = EquatableArray.Create(group) };
        AssertRoundTrip(definition);
    }

    [Fact]
    public void Detail_no_rows_message_round_trips()
    {
        var def = MinimalReport();
        var detail = def.Detail with { NoRowsMessage = "Nenhum dado encontrado para os filtros." };
        AssertRoundTrip(def with { Detail = detail });
    }

    [Fact]
    public void Detail_data_set_name_round_trips()
    {
        var def = MinimalReport();
        AssertRoundTrip(def with { Detail = def.Detail with { DataSetName = "Pedidos" } });
        // null stays null (attribute/key absent) — additive default.
        (def.Detail.DataSetName).Should().BeNull();
    }

    [Fact]
    public void Detail_filter_and_sort_round_trip()
    {
        var def = MinimalReport();
        var detail = def.Detail with
        {
            FilterExpression = "Fields.Total > 100",
            SortExpressions = EquatableArray.Create(
                new SortDescriptor("Fields.Cliente", SortDirection.Ascending),
                new SortDescriptor("Fields.Total", SortDirection.Descending)),
        };
        AssertRoundTrip(def with { Detail = detail });
    }

    [Fact]
    public void Data_source_calculated_fields_round_trip()
    {
        var ds = new DataSourceDefinition("Pedidos",
            Fields: EquatableArray.Create(new DataField("Total", typeof(decimal))),
            CalculatedFields: EquatableArray.Create(
                new CalculatedField("Imposto", "{Fields.Total * 0.18}", typeof(decimal)),
                new CalculatedField("TotalComImposto", "{Fields.Total + Fields.Imposto}")));
        var definition = MinimalReport() with { DataSources = EquatableArray.Create(ds) };
        AssertRoundTrip(definition);
    }

    [Fact]
    public void Data_source_filter_and_sort_round_trip()
    {
        var ds = new DataSourceDefinition("Pedidos",
            FilterExpression: "Fields.Status == 'Pago'",
            SortExpressions: EquatableArray.Create(new SortDescriptor("Fields.Data", SortDirection.Descending)));
        var definition = MinimalReport() with { DataSources = EquatableArray.Create(ds) };
        AssertRoundTrip(definition);
    }

    [Fact]
    public void Group_variables_filter_and_sort_round_trip()
    {
        var group = new GroupBand("g1", "{Fields.Cliente}",
            FilterExpression: "Sum(Fields.Total) > 1000",
            SortExpressions: EquatableArray.Create(new SortDescriptor("Fields.Cliente", SortDirection.Ascending)),
            Variables: EquatableArray.Create(
                new ReportVariable("Subtotal", "{Sum(Fields.Total)}", VariableScope.Group),
                new ReportVariable("Counter", "{Count()}", VariableScope.Group)));
        var def = MinimalReport() with { Groups = EquatableArray.Create(group) };
        AssertRoundTrip(def);
    }

    [Fact]
    public void Full_combination_round_trips_all_features()
    {
        // The kitchen sink — every RDL Phase 1 feature on a single definition. Proves the
        // additions compose correctly with one another (e.g. an element with Bookmark +
        // DocumentMapLabel + Action + ToggleItem, inside a group with PageBreak + Filter +
        // Sort + Variables, hung off a data source with Calculated + Filter + Sort).
        var ds = new DataSourceDefinition("Pedidos",
            Fields: EquatableArray.Create(new DataField("Total", typeof(decimal))),
            CalculatedFields: EquatableArray.Create(
                new CalculatedField("Liquido", "{Fields.Total - Fields.Desconto}")),
            FilterExpression: "Fields.Status == 'Pago'",
            SortExpressions: EquatableArray.Create(new SortDescriptor("Fields.Data")));

        var detail = new DetailBand(
            Unit.FromMm(8),
            EquatableArray.Create<ReportElement>(new TextBoxElement
            {
                Expression = "{Fields.Liquido:C}",
                Bounds = new Rectangle(Unit.FromMm(0), Unit.FromMm(0), Unit.FromMm(50), Unit.FromMm(8)),
                Bookmark = "linha-total",
                DocumentMapLabel = "Total",
                Action = ElementAction.ToDrillthrough("Detalhes",
                    new DrillthroughParameter("Id", "{Fields.Id}")),
                ToggleItemId = "toggle-grupo",
                InitiallyHidden = false,
            }),
            NoRowsMessage: "Nada para mostrar.",
            FilterExpression: "Fields.Total >= 0",
            SortExpressions: EquatableArray.Create(new SortDescriptor("Fields.Total", SortDirection.Descending)),
            PageBreak: PageBreak.End);

        var group = new GroupBand("PorCliente", "{Fields.Cliente}",
            FilterExpression: "Sum(Fields.Total) > 500",
            SortExpressions: EquatableArray.Create(new SortDescriptor("Fields.Cliente")),
            Variables: EquatableArray.Create(
                new ReportVariable("Subtotal", "{Sum(Fields.Liquido)}", VariableScope.Group)),
            PageBreak: PageBreak.Between);

        var definition = MinimalReport() with
        {
            DataSources = EquatableArray.Create(ds),
            Detail = detail,
            Groups = EquatableArray.Create(group),
        };
        AssertRoundTrip(definition);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static ReportDefinition MinimalReport()
    {
        var detail = new DetailBand(
            Unit.FromMm(6),
            EquatableArray.Create<ReportElement>(new TextBoxElement
            {
                Expression = "{Fields.Total:C}",
                Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(40), Unit.FromMm(6)),
            }));
        return new ReportDefinition("RDL test", Paper.PageSetup.A4Portrait, detail);
    }

    private static ReportDefinition WithFirstDetailElementMutated(ReportDefinition def,
        Func<ReportElement, ReportElement> mutator)
    {
        var first = def.Detail.Elements[0];
        var mutated = mutator(first);
        var elements = EquatableArray.Create(new[] { mutated }
            .Concat(def.Detail.Elements.Skip(1)).ToArray());
        return def with { Detail = def.Detail with { Elements = elements } };
    }

    private static void AssertRoundTrip(ReportDefinition original)
    {
        var bytes = Serializer.SaveToBytes(original);
        var loaded = Serializer.LoadFromBytes(bytes);
        loaded.Should().Be(original);
    }
}
