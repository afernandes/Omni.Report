using FluentAssertions;
using Reporting;
using Reporting.Bands;
using Reporting.Common;
using Reporting.DataSources;
using Reporting.DataSources.Enumerable;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Reporting.Paper;
using Reporting.Styling;
using Xunit;

namespace Reporting.Layout.Tests;

/// <summary>Exercises the various ReportElement paths inside BandRenderer so the
/// layout engine is covered beyond the happy "TextBox + Label" path.</summary>
public class BandRendererCoverageTests
{
    private static PaginationRequest WithSingleRowDefinition(params ReportElement[] elements)
    {
        var detail = new DetailBand(
            Unit.FromMm(20),
            new EquatableArray<ReportElement>(elements));
        var def = new ReportDefinition("c", PageSetup.A4Portrait, detail);
        return TestData.BuildRequest(def, [new Venda("c", "p", 1m)]);
    }

    [Fact]
    public async Task Line_element_emits_draw_line_primitive()
    {
        var req = WithSingleRowDefinition(new LineElement
        {
            Id = "ln",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 50.Mm(), 0.Mm()),
            Direction = LineDirection.Horizontal,
        });
        var report = await new ReportPaginator().PaginateAsync(req);
        report.Pages[0].Primitives.OfType<DrawLinePrimitive>().Should().HaveCount(1);
    }

    [Fact]
    public async Task TextBox_with_TextRuns_renders_the_concatenated_runs()
    {
        var req = WithSingleRowDefinition(new TextBoxElement
        {
            Id = "tb",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 80.Mm(), 6.Mm()),
            Expression = "ignored when runs are present",
            TextRuns = EquatableArray.Create(
                new TextRun("Olá "),
                new TextRun("Fields.produto"), // expression run → resolved per-run
                new TextRun("!")),
        });
        var report = await new ReportPaginator().PaginateAsync(req);
        var texts = report.Pages[0].Primitives.OfType<DrawTextPrimitive>().Select(t => t.Text).ToList();
        // The runs are resolved individually and concatenated; "Fields.produto" evaluates to the row value "p".
        texts.Should().Contain("Olá p!");
    }

    [Fact]
    public async Task Rectangle_with_fill_and_border_emits_primitive()
    {
        var req = WithSingleRowDefinition(new RectangleElement
        {
            Id = "rect",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 50.Mm(), 5.Mm()),
            FillColor = Color.LightGray,
            Style = Style.Default with { Border = Border.Uniform(BorderLineStyle.Solid, 1.Pt(), Color.Black) },
        });
        var report = await new ReportPaginator().PaginateAsync(req);
        var prim = report.Pages[0].Primitives.OfType<DrawRectanglePrimitive>().Single();
        prim.Fill.Should().NotBeNull();
        prim.Pen.Should().NotBeNull();
    }

    [Fact]
    public async Task Ellipse_emits_draw_ellipse_primitive()
    {
        var req = WithSingleRowDefinition(new EllipseElement
        {
            Id = "el",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 5.Mm(), 5.Mm()),
            FillColor = Color.Blue,
        });
        var report = await new ReportPaginator().PaginateAsync(req);
        report.Pages[0].Primitives.OfType<DrawEllipsePrimitive>().Should().HaveCount(1);
    }

    [Fact]
    public async Task Inline_image_emits_draw_image_primitive()
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var req = WithSingleRowDefinition(new ImageElement
        {
            Id = "img",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 10.Mm(), 10.Mm()),
            Source = ImageSourceKind.Inline,
            InlineData = bytes,
        });
        var report = await new ReportPaginator().PaginateAsync(req);
        var prim = report.Pages[0].Primitives.OfType<DrawImagePrimitive>().Single();
        prim.Data.Count.Should().Be(4);
    }

    [Fact]
    public async Task Invisible_element_emits_no_primitives()
    {
        var req = WithSingleRowDefinition(new LabelElement
        {
            Id = "hidden",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 10.Mm(), 5.Mm()),
            Text = "hidden",
            Visible = false,
        });
        var report = await new ReportPaginator().PaginateAsync(req);
        report.Pages[0].Primitives.Should().BeEmpty();
    }

    [Fact]
    public async Task Visible_expression_false_hides_element()
    {
        var req = WithSingleRowDefinition(new TextBoxElement
        {
            Id = "cond",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 10.Mm(), 5.Mm()),
            Expression = "Fields.Cliente",
            VisibleExpression = "Fields.Cliente == 'X'",
        });
        var report = await new ReportPaginator().PaginateAsync(req);
        report.Pages[0].Primitives.OfType<DrawTextPrimitive>().Should().BeEmpty();
    }

    [Fact]
    public async Task Conditional_format_layers_style_on_match()
    {
        var req = WithSingleRowDefinition(new TextBoxElement
        {
            Id = "cf",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 30.Mm(), 5.Mm()),
            Expression = "Fields.Total",
            ConditionalFormats = EquatableArray.Create(
                new ConditionalFormat("Fields.Total >= 1", new Style(ForeColor: Color.Red))),
        });
        var report = await new ReportPaginator().PaginateAsync(req);
        var tb = report.Pages[0].Primitives.OfType<DrawTextPrimitive>().Single(t => t.SourceElementId == "cf");
        tb.Style.ForeColor.Should().Be(Color.Red);
    }

    [Fact]
    public async Task Template_expression_renders_currency()
    {
        var req = WithSingleRowDefinition(new TextBoxElement
        {
            Id = "currency",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 30.Mm(), 5.Mm()),
            Expression = "Total: {Fields.Total:C}",
        });
        var report = await new ReportPaginator().PaginateAsync(req);
        var t = report.Pages[0].Primitives.OfType<DrawTextPrimitive>().Single();
        t.Text.Should().StartWith("Total:");
    }

    [Fact]
    public async Task Report_header_only_emitted_once()
    {
        var rh = new ReportBand(BandKind.ReportHeader, Unit.FromMm(8),
            EquatableArray.Create<ReportElement>(
                new LabelElement { Id = "title", Bounds = new Rectangle(0.Mm(), 0.Mm(), 100.Mm(), 8.Mm()), Text = "Title" }));
        var detail = new DetailBand(
            Unit.FromMm(6),
            EquatableArray.Create<ReportElement>(
                new LabelElement { Bounds = new Rectangle(0.Mm(), 0.Mm(), 50.Mm(), 6.Mm()), Text = "row" }));
        var def = new ReportDefinition("h", PageSetup.A4Portrait, detail) { ReportHeader = rh };
        var req = TestData.BuildRequest(def, TestData.ManyRows(50));

        var report = await new ReportPaginator().PaginateAsync(req);
        var headers = report.Pages.SelectMany(p => p.Primitives).OfType<DrawTextPrimitive>().Where(t => t.SourceElementId == "title").ToList();
        headers.Should().HaveCount(1);
    }

    [Fact]
    public async Task Report_footer_appears_at_end()
    {
        var rf = new ReportBand(BandKind.ReportFooter, Unit.FromMm(8),
            EquatableArray.Create<ReportElement>(
                new LabelElement { Id = "rep-footer", Bounds = new Rectangle(0.Mm(), 0.Mm(), 100.Mm(), 8.Mm()), Text = "End" }));
        var detail = new DetailBand(
            Unit.FromMm(6),
            EquatableArray.Create<ReportElement>(
                new LabelElement { Bounds = new Rectangle(0.Mm(), 0.Mm(), 50.Mm(), 6.Mm()), Text = "row" }));
        var def = new ReportDefinition("rf", PageSetup.A4Portrait, detail) { ReportFooter = rf };
        var req = TestData.BuildRequest(def, TestData.ThreeRows());

        var report = await new ReportPaginator().PaginateAsync(req);
        var lastPage = report.Pages[^1];
        lastPage.Primitives.OfType<DrawTextPrimitive>().Should().Contain(t => t.SourceElementId == "rep-footer");
    }
}
