using FluentAssertions;
using Reporting;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Serialization;
using Reporting.Styling;
using Xunit;

namespace Reporting.Serialization.Tests;

/// <summary>
/// Round-trip coverage for the RDL F1.8 (TextRun) and F2-scaffold (Tablix / Code /
/// Map / Gauge / DataBar / Sparkline / Indicator) elements. The renderer doesn't
/// draw these yet, but the wire format MUST be lossless — a .repx authored against
/// SSRS or via the Designer with these elements must reload identically. Without
/// these tests a future "we never use Map" assumption could silently break the
/// round-trip; with them the contract is mechanically enforced.
/// </summary>
public class AdvancedElementsRoundTripTests
{
    private static readonly RepxSerializer Repx = new();
    private static readonly RepJsonSerializer RepJson = new();

    [Fact]
    public void TextRun_with_style_and_action_round_trips()
    {
        var tb = new TextBoxElement
        {
            Expression = "{Fields.Total:C}",
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(60), Unit.FromMm(6)),
            TextRuns = EquatableArray.Create(
                new TextRun("Total: ",
                    Style: new Style(Font: new Font("Arial", 10, FontStyle.Bold))),
                new TextRun("{Fields.Total:C}",
                    Style: new Style(ForeColor: Color.FromHex("#C2410C"))),
                new TextRun(" (detalhes)",
                    Action: ElementAction.ToUrl("https://detalhes.example.com/{Fields.Id}"))),
        };
        AssertRoundTrip(Wrap(tb));
    }

    [Fact]
    public void TextBox_autosize_flags_round_trip()
    {
        // Regression: the .repx writer dropped CanGrow/CanShrink (the reader already read them), so
        // a growable TextBox authored in the designer lost its autosize on save.
        var tb = new TextBoxElement
        {
            Expression = "Fields.Nome",
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(60), Unit.FromMm(6)),
            CanGrow = true,
            CanShrink = true,
        };
        AssertRoundTrip(Wrap(tb));
    }

    [Fact]
    public void Property_expression_bindings_round_trip()
    {
        var tb = new TextBoxElement
        {
            Expression = "Fields.Nome",
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(60), Unit.FromMm(8)),
            PropertyExpressions = new EquatableDictionary<string, string>(new Dictionary<string, string>
            {
                ["Style.ForeColor"] = "Fields.Total > 1000 ? '#C00' : '#000'",
                ["Style.Font.Size"] = "Fields.Tamanho",
                ["Visible"] = "Fields.Mostrar",
            }),
        };
        AssertRoundTrip(Wrap(tb));
    }

    [Fact]
    public void Tablix_with_groups_and_cells_round_trips()
    {
        var tablix = new TablixElement
        {
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(150), Unit.FromMm(80)),
            DataSetName = "Vendas",
            RowGroups = EquatableArray.Create(
                new TablixGroup("Ano", "Fields.Ano"),
                new TablixGroup("Mes", "Fields.Mes", "Fields.MesNumero", SortDescending: false)),
            ColumnGroups = EquatableArray.Create(
                new TablixGroup("Categoria", "Fields.Categoria")),
            ColumnWidths = EquatableArray.Create(1.0, 2.5, 1.25),
            Cells = EquatableArray.Create(
                new TablixCell(0, 0, new TextBoxElement
                {
                    Expression = "{Sum(Fields.Total)}",
                    Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(30), Unit.FromMm(6)),
                })),
        };
        AssertRoundTrip(Wrap(tablix));
    }

    [Fact]
    public void Code_element_preserves_source_with_special_chars()
    {
        var code = new CodeElement
        {
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(1), Unit.FromMm(1)),
            Language = CodeLanguage.CSharp,
            Source = """
                // Markdown-flavoured math: a < b && b > c, plus a "quoted" string.
                public static double FormatCurrency(double v) => v >= 0 ? v : -v;
                """,
        };
        AssertRoundTrip(Wrap(code));
    }

    [Fact]
    public void Map_with_geo_columns_round_trips()
    {
        var map = new MapElement
        {
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(120), Unit.FromMm(80)),
            Basemap = "OpenStreetMap",
            DataSetName = "Lojas",
            LatitudeExpression = "Fields.Lat",
            LongitudeExpression = "Fields.Lon",
            ShapeSet = "brazil",
            ShapesGeoJson = "{\"type\":\"Polygon\",\"coordinates\":[[[-50,-20],[-40,-20],[-45,-10],[-50,-20]]]}",
            ShowGraticule = true,
            ShapeFill = "#DDE7D5",
            ShapeStroke = "#778",
        };
        AssertRoundTrip(Wrap(map));
    }

    [Fact]
    public void Gauge_with_ranges_round_trips()
    {
        var gauge = new GaugeElement
        {
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(40), Unit.FromMm(40)),
            Kind = GaugeKind.Radial,
            ValueExpression = "Fields.Velocidade",
            MinimumExpression = "0",
            MaximumExpression = "180",
            Ranges = EquatableArray.Create(
                new GaugeRange("0",   "60",  "#22c55e"),
                new GaugeRange("60",  "120", "#eab308"),
                new GaugeRange("120", "180", "#dc2626")),
        };
        AssertRoundTrip(Wrap(gauge));
    }

    [Fact]
    public void DataBar_round_trips_value_min_max_fill()
    {
        var bar = new DataBarElement
        {
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(40), Unit.FromMm(4)),
            ValueExpression = "Fields.Progresso",
            MinimumExpression = "0",
            MaximumExpression = "100",
            FillColor = "#0EA5E9",
        };
        AssertRoundTrip(Wrap(bar));
    }

    [Fact]
    public void Sparkline_with_category_round_trips()
    {
        var spark = new SparklineElement
        {
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(30), Unit.FromMm(8)),
            Kind = SparklineKind.Column,
            DataSetName = "VendasMensais",
            ValueExpression = "Fields.Total",
            CategoryExpression = "Fields.Mes",
        };
        AssertRoundTrip(Wrap(spark));
    }

    [Fact]
    public void Indicator_with_states_round_trips()
    {
        var ind = new IndicatorElement
        {
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(8), Unit.FromMm(8)),
            Kind = IndicatorKind.Shape,
            ValueExpression = "Fields.MetaAtingida",
            States = EquatableArray.Create(
                new IndicatorState("0",   "33",  "circle-red"),
                new IndicatorState("33",  "66",  "circle-yellow"),
                new IndicatorState("66",  "100", "circle-green")),
        };
        AssertRoundTrip(Wrap(ind));
    }

    /// <summary>Wraps an element in a minimal definition so the serializer has a Detail
    /// band to put it in. Using the same template for every test keeps the assertion
    /// focused on the element's own round-trip rather than the surrounding skeleton.</summary>
    private static ReportDefinition Wrap(ReportElement el)
    {
        var detail = new DetailBand(
            Unit.FromMm(20),
            EquatableArray.Create(el));
        return new ReportDefinition("Advanced", Paper.PageSetup.A4Portrait, detail);
    }

    /// <summary>Asserts a lossless round-trip through BOTH wire formats. The .repjson path is
    /// covered explicitly because it has its own element-type switch (separate from .repx) — a
    /// missing case there throws "Unsupported element type" at save time, which a repx-only test
    /// would never catch.</summary>
    private static void AssertRoundTrip(ReportDefinition original)
    {
        var repxBytes = Repx.SaveToBytes(original);
        Repx.LoadFromBytes(repxBytes).Should().Be(original);

        var jsonBytes = RepJson.SaveToBytes(original);
        RepJson.LoadFromBytes(jsonBytes).Should().Be(original);
    }
}
