using Reporting;
using Reporting.Aggregates;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Data;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Paper;
using Reporting.Parameters;
using Reporting.Styling;

namespace Reporting.Serialization.Tests;

internal static class Fixtures
{
    /// <summary>A non-trivial report that exercises most of the model surface.</summary>
    public static ReportDefinition KitchenSink()
    {
        var detail = new DetailBand(
            Unit.FromMm(6),
            EquatableArray.Create<ReportElement>(
                new TextBoxElement
                {
                    Id = "tb-produto",
                    Bounds = new Rectangle(0.Mm(), 0.Mm(), 80.Mm(), 6.Mm()),
                    Expression = "{Fields.Produto}",
                    Style = new Style(
                        Font: new Font("Arial", 10, FontStyle.Regular),
                        ForeColor: Color.Black,
                        HorizontalAlignment: HorizontalAlignment.Left),
                    ConditionalFormats = EquatableArray.Create(
                        new ConditionalFormat("Fields.Total > 100",
                            new Style(ForeColor: Color.Red, Font: new Font("Arial", 10, FontStyle.Bold)))),
                },
                new LabelElement
                {
                    Id = "lbl-sep",
                    Bounds = new Rectangle(80.Mm(), 0.Mm(), 2.Mm(), 6.Mm()),
                    Text = "·",
                },
                new LineElement
                {
                    Id = "ln-sep",
                    Bounds = new Rectangle(0.Mm(), 5.Mm(), 170.Mm(), 0.Mm()),
                    Direction = LineDirection.Horizontal,
                    Pen = new BorderSide(BorderLineStyle.Dashed, 0.5.Pt(), Color.Gray),
                }),
            CanGrow: true, CanShrink: false);

        var groupHeader = new ReportBand(BandKind.GroupHeader, Unit.FromMm(10),
            EquatableArray.Create<ReportElement>(
                new TextBoxElement
                {
                    Id = "gh-cliente",
                    Bounds = new Rectangle(0.Mm(), 0.Mm(), 100.Mm(), 8.Mm()),
                    Expression = "{Fields.Cliente}",
                    Style = new Style(Font: new Font("Arial", 12, FontStyle.Bold), ForeColor: Color.FromHex("#C2410C")),
                }));

        var groupFooter = new ReportBand(BandKind.GroupFooter, Unit.FromMm(8),
            EquatableArray.Create<ReportElement>(
                new TextBoxElement
                {
                    Id = "gf-sum",
                    Bounds = new Rectangle(0.Mm(), 0.Mm(), 170.Mm(), 6.Mm()),
                    Expression = "{Sum(Fields.Total, 'Group'):C}",
                    Style = new Style(HorizontalAlignment: HorizontalAlignment.Right, Font: new Font("Arial", 10, FontStyle.Bold)),
                }));

        var group = new GroupBand(
            "PorCliente", "Fields.Cliente",
            groupHeader, groupFooter,
            KeepTogether: true, NewPageBefore: false, RepeatHeaderOnNewPage: true);

        var rectEl = new RectangleElement
        {
            Id = "rect-bg",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 170.Mm(), 30.Mm()),
            FillColor = Color.LightGray,
            CornerRadius = Unit.FromMm(2),
            Style = Style.Default with
            {
                Border = Border.Uniform(BorderLineStyle.Solid, 0.5.Pt(), Color.Black),
            },
        };

        var ellipse = new EllipseElement
        {
            Id = "ell",
            Bounds = new Rectangle(140.Mm(), 0.Mm(), 8.Mm(), 8.Mm()),
            FillColor = Color.Red,
        };

        var img = new ImageElement
        {
            Id = "img-inline",
            Bounds = new Rectangle(2.Mm(), 2.Mm(), 26.Mm(), 26.Mm()),
            Source = ImageSourceKind.Inline,
            Sizing = ImageSizing.Fit,
            InlineData = new EquatableArray<byte>(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A }),
        };

        var bc = new BarcodeElement
        {
            Id = "bc-ean",
            Bounds = new Rectangle(60.Mm(), 2.Mm(), 40.Mm(), 12.Mm()),
            Symbology = BarcodeSymbology.Ean13,
            Expression = "{Fields.Ean}",
            ShowText = true,
        };

        var chart = new ChartElement
        {
            Id = "chart-1",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 100.Mm(), 60.Mm()),
            Kind = ChartKind.Bar,
            Title = "Vendas por mês",
            ShowLegend = true,
            Series = EquatableArray.Create(
                new ChartSeries("Vendas", "Fields.Mes", "Fields.Total", Color.FromHex("#C2410C"))),
        };

        var sub = new SubreportElement
        {
            Id = "sub-1",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 170.Mm(), 100.Mm()),
            ReportId = "Itens",
            DataExpression = "Fields.Filhos",
            ParameterBindings = new EquatableDictionary<string, string>(
                new Dictionary<string, string> { ["clienteId"] = "Fields.Id" }),
        };

        var table = new TableElement
        {
            Id = "tbl",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 170.Mm(), 0.Mm()),
            HeaderHeight = Unit.FromMm(8),
            DetailHeight = Unit.FromMm(6),
            FooterHeight = Unit.FromMm(8),
            Columns = EquatableArray.Create(
                new TableColumn("Produto", 80.Mm(), HeaderText: "Produto", DetailExpression: "Fields.Produto"),
                new TableColumn("Total", 30.Mm(), HeaderText: "Total", DetailExpression: "Fields.Total", FooterExpression: "Sum(Fields.Total)")),
            DataExpression = "Fields.Itens",
        };

        var reportHeader = new ReportBand(BandKind.ReportHeader, Unit.FromMm(30),
            EquatableArray.Create<ReportElement>(rectEl, ellipse, img, bc, chart, sub, table));

        var pageHeader = new ReportBand(BandKind.PageHeader, Unit.FromMm(8),
            EquatableArray.Create<ReportElement>(
                new LabelElement
                {
                    Bounds = new Rectangle(0.Mm(), 0.Mm(), 100.Mm(), 6.Mm()),
                    Text = "Produto",
                })) with
        {
            VisibleExpression = "Page.Number > 0",
        };

        var pageFooter = new ReportBand(BandKind.PageFooter, Unit.FromMm(6),
            EquatableArray.Create<ReportElement>(
                new TextBoxElement
                {
                    Bounds = new Rectangle(0.Mm(), 0.Mm(), 170.Mm(), 5.Mm()),
                    Expression = "Página {Page.Number} de {Page.Total}",
                }))
        {
            PrintOnFirstPage = false,
            PrintOnLastPage = true,
        };

        var reportFooter = new ReportBand(BandKind.ReportFooter, Unit.FromMm(10),
            EquatableArray.Create<ReportElement>(
                new TextBoxElement
                {
                    Bounds = new Rectangle(0.Mm(), 0.Mm(), 170.Mm(), 8.Mm()),
                    Expression = "{Sum(Fields.Total):C}",
                }));

        var dataSource = new DataSourceDefinition(
            "Vendas",
            DataMember: "dbo.vw_vendas",
            Fields: EquatableArray.Create(
                new DataField("Cliente", typeof(string), DisplayName: "Cliente"),
                new DataField("Total", typeof(decimal)),
                new DataField("Ean", typeof(string))),
            Relations: EquatableArray.Create(
                new DataRelation("VendasItens", "Vendas", "Id", "Itens", "VendaId")),
            Parameters: new EquatableDictionary<string, string>(
                new Dictionary<string, string> { ["since"] = "Parameters.DataInicio" }));

        return new ReportDefinition("KitchenSink", PageSetup.A4Portrait, detail)
        {
            SchemaVersion = SchemaVersion.Current.ToString(),
            Parameters = EquatableArray.Create(
                new ReportParameter("DataInicio", typeof(DateTime), "Data inicial", new DateTime(2026, 1, 1)),
                new ReportParameter("Limite", typeof(decimal), DefaultValue: 100m, Required: false)),
            Variables = EquatableArray.Create(
                new ReportVariable("AccVendas", "Sum(Fields.Total)", VariableScope.Report),
                new ReportVariable("Linha", "Count(Fields.Total)", VariableScope.Row)),
            DataSources = EquatableArray.Create(dataSource),
            Groups = EquatableArray.Create(group),
            ReportHeader = reportHeader,
            PageHeader = pageHeader,
            PageFooter = pageFooter,
            ReportFooter = reportFooter,
            Metadata = new EquatableDictionary<string, string>(
                new Dictionary<string, string> { ["Author"] = "ana", ["Version"] = "0.1" }),
        };
    }

    public static ReportDefinition MinimalReport()
        => ReportDefinition.Empty("Minimal");
}
