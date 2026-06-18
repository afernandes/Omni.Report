using Reporting.Designer.Blazor.ViewModels;
using Reporting.Geometry;
using Reporting.Styling;

namespace Reporting.Samples.BlazorServer;

/// <summary>
/// Builds a fully-configured master-detail demo for the "DB · SQLite" sandbox button.
///
/// <para><b>Why this exists</b>: Sub-bands (DevExpress <c>DetailReportBand</c>, FastReport
/// "Detail data band linked to MasterDataBand", Stimulsoft <c>DataBand</c> with master
/// pointer) are the trickiest concept in any banded report engine — without a working
/// example the configuration looks abstract. This seeder ships a complete layout so the
/// user can hit "Preview" immediately and see master-detail rendering, then study/tweak
/// the bands in the canvas.</para>
///
/// <para><b>Pattern used here</b>: the FastReport / DevExpress nested-band model — Detail
/// iterates the parent (Clientes), and a SubDetail band linked by relation name iterates
/// the matching children (Pedidos) right under it. This produces a "header + table"
/// layout per parent row, which is the most common shape (sales by customer, invoices by
/// vendor, orders by region). Crystal Reports achieves the same effect with a Group on
/// the parent key — both shapes are valid; we chose nested bands because they keep the
/// data graph explicit (relation → sub-band) instead of relying on group-by-expression.</para>
///
/// <para><b>Layout</b> (A4 portrait, content width ≈ 190 mm):
/// <code>
///   ┌── ReportHeader (18 mm) ───────────────────────────────────────────┐
///   │  "Vendas por Cliente"          (28pt, bold, centered)             │
///   │  "Período: {DataInicio} a {DataFim}"                              │
///   └────────────────────────────────────────────────────────────────────┘
///   ┌── PageHeader (8 mm) ──────────────────────────────────────────────┐
///   │  "Vendas por Cliente"                       "Página {N}/{Total}"  │
///   │  ────────────────────────────────────────────────────────────────  │
///   └────────────────────────────────────────────────────────────────────┘
///   ┌── Detail (16 mm) — fires ONCE per Cliente ────────────────────────┐
///   │  👤 {nome}                                              {cidade}  │
///   │  ────────────────────────────────────────────────────────────────  │
///   │  DATA   PRODUTO                  QTD     PREÇO UN.    SUBTOTAL    │
///   └────────────────────────────────────────────────────────────────────┘
///   ┌── SubDetail "PedidosDeCliente" (6 mm) — fires per Pedido ─────────┐
///   │  {data}  {produto}              {qtd}   {preco_unitario}  {qtd×p} │
///   └────────────────────────────────────────────────────────────────────┘
///   ┌── PageFooter (8 mm) ──────────────────────────────────────────────┐
///   │  "Gerado em {Now}"                          "Página {N}/{Total}"  │
///   └────────────────────────────────────────────────────────────────────┘
/// </code>
/// </para>
/// </summary>
public static class DemoSqliteReport
{
    // ── Layout constants — A4 portrait, 10 mm margins on each side ──────
    // Content width = 210 − 10 − 10 = 190 mm. Column X positions below
    // sum exactly to 190 so nothing overflows.
    private const double ContentWidthMm = 190;

    // Column geometry shared between the Detail's column-header strip and the
    // SubDetail's data row, so values line up visually.
    private const double ColDataX     = 0;    private const double ColDataW     = 22;
    private const double ColProdutoX  = 22;   private const double ColProdutoW  = 84;
    private const double ColQtdX      = 106;  private const double ColQtdW      = 18;
    private const double ColPrecoX    = 124;  private const double ColPrecoW    = 30;
    private const double ColSubtotalX = 154;  private const double ColSubtotalW = 36;

    public static void SeedInto(DesignerState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var cs = DemoSqliteDatabase.ConnectionString;

        // ── Data sources ─────────────────────────────────────────────────────
        state.DataSources.Clear();

        var clientes = new DesignerDataSource("Clientes", new[]
        {
            new DesignerField("id",     DesignerFieldType.Number),
            new DesignerField("nome",   DesignerFieldType.Text, "Ana Beatriz"),
            new DesignerField("cidade", DesignerFieldType.Text, "São Paulo"),
        })
        {
            Kind = DataConnectionKind.Sqlite,
            ConnectionString = cs,
            Sql = "SELECT id, nome, cidade FROM clientes ORDER BY nome",
        };
        state.DataSources.Add(clientes);

        var pedidos = new DesignerDataSource("Pedidos", new[]
        {
            new DesignerField("id",             DesignerFieldType.Number),
            new DesignerField("cliente_id",     DesignerFieldType.Number),
            new DesignerField("produto",        DesignerFieldType.Text,  "Caneta Bic Azul"),
            new DesignerField("quantidade",     DesignerFieldType.Number, "10"),
            new DesignerField("preco_unitario", DesignerFieldType.Money,  "2.50"),
            new DesignerField("data",           DesignerFieldType.Date,   "2026-05-03"),
        })
        {
            Kind = DataConnectionKind.Sqlite,
            ConnectionString = cs,
            Sql = "SELECT id, cliente_id, produto, quantidade, preco_unitario, data " +
                  "FROM pedidos WHERE data BETWEEN $dataInicio AND $dataFim ORDER BY cliente_id, data",
        };
        pedidos.SqlParameters.Add(new DesignerSqlParameter("$dataInicio", "DataInicio", literal: "2026-05-01"));
        pedidos.SqlParameters.Add(new DesignerSqlParameter("$dataFim",    "DataFim",    literal: "2026-05-31"));
        state.DataSources.Add(pedidos);

        // ── Master-detail relation ───────────────────────────────────────────
        // The relation NAME ("PedidosDeCliente") is what the SubDetail band's
        // DataMember points to. The paginator resolves it against the parent's
        // DataSourceDefinition.Relations to find the join (parentField → childField).
        state.Relations.Clear();
        state.Relations.Add(new DesignerRelation(
            name: "PedidosDeCliente",
            parentSource: "Clientes", parentField: "id",
            childSource:  "Pedidos",  childField:  "cliente_id"));

        // ── Report parameters ────────────────────────────────────────────────
        state.Parameters.Clear();
        state.Parameters.Add(new DesignerParameter("DataInicio", DesignerFieldType.Date, "2026-05-01"));
        state.Parameters.Add(new DesignerParameter("DataFim",    DesignerFieldType.Date, "2026-05-31"));

        // ── Build the report layout ──────────────────────────────────────────
        var vm = new ReportDefinitionViewModel("Vendas por Cliente (master-detail demo)")
        {
            // Override the default 20mm margins → 10mm. The column constants below (ColDataX,
            // ColProdutoX, …) assume a 190mm content width, which matches A4 (210mm) − 10mm
            // each side. Default margins would give 170mm content and clip the SUBTOTAL column.
            PageSetup = Reporting.Paper.PageSetup.A4Portrait with
            {
                Margins = Reporting.Geometry.Thickness.Uniform(Unit.FromMm(10)),
            },
        };
        BuildBands(vm);
        state.ReplaceActiveReport(vm);
    }

    /// <summary>Wipes the VM's default bands and rebuilds the canonical master-detail
    /// layout (RH → PH → Detail/Clientes → SubDetail/Pedidos → PF). Element positions
    /// use the column constants above so the SubDetail row aligns under the Detail's
    /// column-header strip.</summary>
    private static void BuildBands(ReportDefinitionViewModel vm)
    {
        // Replace the seeded PageHeader + Detail + PageFooter with our own ordered set.
        foreach (var b in vm.Bands.ToList()) vm.RemoveBand(b);

        // ── Report Header (banner — appears once, on page 1 only) ───────────
        var rh = vm.AddBand(new BandViewModel(DesignerBandKind.ReportHeader, Unit.FromMm(18)));
        rh.AddElement(Label(id: "rh-title",
            x: 0, y: 2, w: ContentWidthMm, h: 9,
            text: "Vendas por Cliente",
            fontSize: 18, bold: true,
            hAlign: HorizontalAlignment.Center));
        rh.AddElement(TextBox(id: "rh-period",
            x: 0, y: 12, w: ContentWidthMm, h: 5,
            expression: "Período: {Parameters.DataInicio:dd/MM/yyyy} a {Parameters.DataFim:dd/MM/yyyy}",
            fontSize: 9, italic: true,
            hAlign: HorizontalAlignment.Center,
            foreColor: Gray));

        // ── Page Header (compact ribbon repeating on every page) ────────────
        var ph = vm.AddBand(new BandViewModel(DesignerBandKind.PageHeader, Unit.FromMm(8)));
        ph.AddElement(Label(id: "ph-app",
            x: 0, y: 1, w: 120, h: 5,
            text: "Vendas por Cliente",
            fontSize: 8, bold: true, foreColor: Gray));
        ph.AddElement(TextBox(id: "ph-page",
            x: 120, y: 1, w: ContentWidthMm - 120, h: 5,
            expression: "Página {Page.PageNumber} de {Page.TotalPages}",
            fontSize: 8, foreColor: Gray,
            hAlign: HorizontalAlignment.Right));
        // Bottom rule (1 mm tall rectangle works as a divider)
        ph.AddElement(Rectangle(id: "ph-rule",
            x: 0, y: 7, w: ContentWidthMm, h: 0.25,
            fillColor: BorderLine));

        // ── Detail (master row — fires ONCE per Cliente) ────────────────────
        //
        // This band shows the "section header" for each Cliente. It carries
        // both the Cliente's name + cidade AND the column-header strip for the
        // Pedidos table that the SubDetail will fill in.
        var dt = vm.AddBand(new BandViewModel(DesignerBandKind.Detail, Unit.FromMm(16)));
        dt.AddElement(TextBox(id: "dt-nome",
            x: 0, y: 1, w: 120, h: 6,
            // Unqualified — relies on master-detail fallback to resolve `nome`
            // against the Clientes row (the live row is the parent in this band).
            expression: "👤 {Fields.nome}",
            fontSize: 13, bold: true));
        dt.AddElement(TextBox(id: "dt-cidade",
            x: 120, y: 2, w: ContentWidthMm - 120, h: 5,
            expression: "{Fields.cidade}",
            fontSize: 10, italic: true, foreColor: Gray,
            hAlign: HorizontalAlignment.Right));
        // Horizontal rule between cliente identity and the columns header.
        dt.AddElement(Rectangle(id: "dt-rule",
            x: 0, y: 8, w: ContentWidthMm, h: 0.25,
            fillColor: BorderLine));
        // Column-header strip — labels for the SubDetail's data row.
        dt.AddElement(Label(id: "dt-h-data",     x: ColDataX,     y: 10, w: ColDataW,     h: 5, text: "DATA",       fontSize: 8, bold: true, foreColor: Gray));
        dt.AddElement(Label(id: "dt-h-produto",  x: ColProdutoX,  y: 10, w: ColProdutoW,  h: 5, text: "PRODUTO",    fontSize: 8, bold: true, foreColor: Gray));
        dt.AddElement(Label(id: "dt-h-qtd",      x: ColQtdX,      y: 10, w: ColQtdW,      h: 5, text: "QTD",        fontSize: 8, bold: true, foreColor: Gray, hAlign: HorizontalAlignment.Right));
        dt.AddElement(Label(id: "dt-h-preco",    x: ColPrecoX,    y: 10, w: ColPrecoW,    h: 5, text: "PREÇO UN.",  fontSize: 8, bold: true, foreColor: Gray, hAlign: HorizontalAlignment.Right));
        dt.AddElement(Label(id: "dt-h-subtotal", x: ColSubtotalX, y: 10, w: ColSubtotalW, h: 5, text: "SUBTOTAL",   fontSize: 8, bold: true, foreColor: Gray, hAlign: HorizontalAlignment.Right));

        // ── SubDetail (linked to "PedidosDeCliente" — fires per Pedido) ─────
        //
        // DataMember = the relation NAME (not the source name). The paginator
        // resolves "PedidosDeCliente" against the parent's Relations array,
        // which yields child=Pedidos filtered by parent.id == pedido.cliente_id.
        var sd = vm.AddBand(new BandViewModel(DesignerBandKind.SubDetail, Unit.FromMm(6))
        {
            DataMember = "PedidosDeCliente",
            GroupName  = "PedidosDoCliente", // shown as the band's display name
            PrintIfEmpty = false,            // Crystal/DevExpress default: skip empty clients
        });
        // Each cell uses the {expr:format} template syntax for typed formatting.
        sd.AddElement(TextBox(id: "sd-data",     x: ColDataX,     y: 0, w: ColDataW,     h: 5,
            expression: "{Fields.data:dd/MM}", fontSize: 9));
        sd.AddElement(TextBox(id: "sd-produto",  x: ColProdutoX,  y: 0, w: ColProdutoW,  h: 5,
            expression: "{Fields.produto}", fontSize: 9));
        sd.AddElement(TextBox(id: "sd-qtd",      x: ColQtdX,      y: 0, w: ColQtdW,      h: 5,
            expression: "{Fields.quantidade}", fontSize: 9,
            hAlign: HorizontalAlignment.Right));
        sd.AddElement(TextBox(id: "sd-preco",    x: ColPrecoX,    y: 0, w: ColPrecoW,    h: 5,
            expression: "{Fields.preco_unitario:N2}", fontSize: 9,
            hAlign: HorizontalAlignment.Right));
        // Subtotal column = quantidade × preco_unitario. The NCalc parser handles
        // the arithmetic; the {:N2} format string keeps it currency-style.
        sd.AddElement(TextBox(id: "sd-subtotal", x: ColSubtotalX, y: 0, w: ColSubtotalW, h: 5,
            expression: "{Fields.quantidade * Fields.preco_unitario:N2}", fontSize: 9, bold: true,
            hAlign: HorizontalAlignment.Right));

        // ── Page Footer (timestamp + page number) ───────────────────────────
        var pf = vm.AddBand(new BandViewModel(DesignerBandKind.PageFooter, Unit.FromMm(8)));
        pf.AddElement(Rectangle(id: "pf-rule",
            x: 0, y: 0, w: ContentWidthMm, h: 0.25,
            fillColor: BorderLine));
        pf.AddElement(TextBox(id: "pf-stamp",
            x: 0, y: 2, w: 120, h: 5,
            expression: "Gerado em {Now:dd/MM/yyyy HH:mm}",
            fontSize: 8, italic: true, foreColor: Gray));
        pf.AddElement(TextBox(id: "pf-page",
            x: 120, y: 2, w: ContentWidthMm - 120, h: 5,
            expression: "Página {Page.PageNumber} de {Page.TotalPages}",
            fontSize: 8, foreColor: Gray,
            hAlign: HorizontalAlignment.Right));
    }

    // ── Element factory helpers ──────────────────────────────────────────
    // Keeping the helpers tight so the band-builder reads top-to-bottom like
    // a layout spec — every visual choice is on the call site, not buried in
    // a constructor chain.

    private static readonly Color Gray       = new(0x6B, 0x72, 0x80, 0xFF);
    private static readonly Color BorderLine = new(0xD1, 0xD5, 0xDB, 0xFF);

    private static ElementViewModel Label(
        string id, double x, double y, double w, double h, string text,
        double fontSize = 10, bool bold = false, bool italic = false,
        Color? foreColor = null,
        HorizontalAlignment hAlign = HorizontalAlignment.Left)
        => new(DesignerElementKind.Label, id)
        {
            X = Unit.FromMm(x), Y = Unit.FromMm(y),
            Width = Unit.FromMm(w), Height = Unit.FromMm(h),
            Text = text, Expression = text,
            FontSize = fontSize, IsBold = bold, IsItalic = italic,
            ForeColor = foreColor ?? Color.Black,
            HorizontalAlignment = hAlign,
            VerticalAlignment = VerticalAlignment.Middle,
        };

    private static ElementViewModel TextBox(
        string id, double x, double y, double w, double h, string expression,
        double fontSize = 10, bool bold = false, bool italic = false,
        Color? foreColor = null,
        HorizontalAlignment hAlign = HorizontalAlignment.Left)
        => new(DesignerElementKind.TextBox, id)
        {
            X = Unit.FromMm(x), Y = Unit.FromMm(y),
            Width = Unit.FromMm(w), Height = Unit.FromMm(h),
            Expression = expression,
            FontSize = fontSize, IsBold = bold, IsItalic = italic,
            ForeColor = foreColor ?? Color.Black,
            HorizontalAlignment = hAlign,
            VerticalAlignment = VerticalAlignment.Middle,
        };

    private static ElementViewModel Rectangle(
        string id, double x, double y, double w, double h, Color fillColor)
        => new(DesignerElementKind.Rectangle, id)
        {
            X = Unit.FromMm(x), Y = Unit.FromMm(y),
            Width = Unit.FromMm(w), Height = Unit.FromMm(h),
            FillColor = fillColor,
        };
}
