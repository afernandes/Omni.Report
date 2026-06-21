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
    public async Task CanGrow_multirun_textbox_with_a_literal_brace_measures_without_throwing()
    {
        // Regression: Measure() must resolve the runs (not the fallback Expression template). A literal "{"
        // in a run would make the template path throw FormatException and abort pagination if Measure read
        // tb.Expression instead of the runs.
        var req = WithSingleRowDefinition(new TextBoxElement
        {
            Id = "tb",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 80.Mm(), 6.Mm()),
            CanGrow = true,
            Expression = "fallback",
            // Run values carry template semantics (like Expression): a literal brace is escaped {{ }}. The
            // key point is Measure resolves the RUNS (not tb.Expression), so CanGrow doesn't crash/mis-size.
            TextRuns = EquatableArray.Create(new TextRun("a {{literal}} brace "), new TextRun("Fields.produto")),
        });
        var report = await new ReportPaginator().PaginateAsync(req);
        var texts = report.Pages[0].Primitives.OfType<DrawTextPrimitive>().Select(t => t.Text).ToList();
        texts.Should().Contain(t => t.Contains("a {literal} brace ") && t.Contains("p"));
    }

    [Fact]
    public async Task TextBox_with_BackColor_emits_a_background_fill_behind_the_text()
    {
        var req = WithSingleRowDefinition(new TextBoxElement
        {
            Id = "tb",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 50.Mm(), 8.Mm()),
            Expression = "'Olá'",
            Style = Style.Default with { BackColor = Color.LightGray },
        });
        var report = await new ReportPaginator().PaginateAsync(req);
        var prims = report.Pages[0].Primitives;
        // A fill rectangle of the BackColor is emitted, and it comes BEFORE the text (drawn underneath).
        var fill = prims.OfType<DrawRectanglePrimitive>().Single(r => r.Fill?.Color == Color.LightGray);
        var fillIdx = prims.ToList().IndexOf(fill);
        var textIdx = prims.ToList().FindIndex(p => p is DrawTextPrimitive);
        fillIdx.Should().BeLessThan(textIdx, "the background fill is drawn before (under) the text");
    }

    [Fact]
    public async Task Rectangle_container_draws_children_on_top_at_relative_positions()
    {
        var req = WithSingleRowDefinition(new RectangleElement
        {
            Id = "box",
            Bounds = new Rectangle(10.Mm(), 10.Mm(), 80.Mm(), 40.Mm()),
            FillColor = Color.LightGray,
            Children = EquatableArray.Create<ReportElement>(new LabelElement
            {
                Id = "child",
                Text = "dentro",
                Bounds = new Rectangle(5.Mm(), 5.Mm(), 30.Mm(), 6.Mm()), // RELATIVE to the rectangle
            }),
        });
        var report = await new ReportPaginator().PaginateAsync(req);
        var prims = report.Pages[0].Primitives.ToList();

        var fill = prims.OfType<DrawRectanglePrimitive>().Single(r => r.SourceElementId == "box");
        var child = prims.OfType<DrawTextPrimitive>().Single(t => t.Text == "dentro");
        // Z-order: the rectangle fill is emitted BEFORE its child (child drawn on top).
        prims.IndexOf(fill).Should().BeLessThan(prims.IndexOf(child));
        // The child sits at the rectangle's top-left + its RELATIVE bounds (5mm, 5mm) — not flattened away.
        (child.Bounds.X - fill.Bounds.X).Should().Be(5.Mm());
        (child.Bounds.Y - fill.Bounds.Y).Should().Be(5.Mm());
        // The child is clipped to the container; the fill itself carries no clip.
        child.ClipBounds.Should().Be(fill.Bounds);
        fill.ClipBounds.Should().BeNull();
    }

    [Fact]
    public async Task Nested_rectangles_intersect_their_clips()
    {
        var req = WithSingleRowDefinition(new RectangleElement
        {
            Id = "outer",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 100.Mm(), 50.Mm()),
            Children = EquatableArray.Create<ReportElement>(new RectangleElement
            {
                Id = "inner",
                Bounds = new Rectangle(10.Mm(), 10.Mm(), 200.Mm(), 200.Mm()), // deliberately overflows outer
                Children = EquatableArray.Create<ReportElement>(new LabelElement
                {
                    Id = "leaf",
                    Text = "fundo",
                    Bounds = new Rectangle(5.Mm(), 5.Mm(), 20.Mm(), 6.Mm()),
                }),
            }),
        });
        var report = await new ReportPaginator().PaginateAsync(req);
        var prims = report.Pages[0].Primitives.ToList();

        var outerFill = prims.OfType<DrawRectanglePrimitive>().Single(r => r.SourceElementId == "outer");
        var leaf = prims.OfType<DrawTextPrimitive>().Single(t => t.Text == "fundo");
        // The leaf's clip is the INTERSECTION of inner (origin+10,10 .. huge) and outer (0,0..100,50):
        // x = max(0, 10) = 10mm; right = min(100, 210) = 100mm; bottom = min(50, 210) = 50mm.
        var clip = leaf.ClipBounds!.Value;
        clip.X.Should().Be(outerFill.Bounds.X + 10.Mm());
        clip.Right.Should().Be(outerFill.Bounds.Right, "the inner overflows, so the outer's right edge wins");
        clip.Bottom.Should().Be(outerFill.Bounds.Bottom);
    }

    [Fact]
    public async Task Container_rectangle_action_does_not_leak_onto_its_children()
    {
        // Regression: the per-element Action/Bookmark propagation tail must NOT span the children appended
        // during recursion — a child without its own link must stay link-less, and a child WITH its own link
        // must keep it (not be overwritten by the parent rectangle's).
        var req = WithSingleRowDefinition(new RectangleElement
        {
            Id = "box",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 80.Mm(), 30.Mm()),
            Action = ElementAction.ToUrl("https://rect.example.com"),
            Children = EquatableArray.Create<ReportElement>(
                new LabelElement { Id = "plain", Text = "semlink", Bounds = new Rectangle(2.Mm(), 2.Mm(), 30.Mm(), 6.Mm()) },
                new LabelElement
                {
                    Id = "own",
                    Text = "comlink",
                    Bounds = new Rectangle(2.Mm(), 10.Mm(), 30.Mm(), 6.Mm()),
                    Action = ElementAction.ToUrl("https://child.example.com"),
                }),
        });
        var report = await new ReportPaginator().PaginateAsync(req);
        var prims = report.Pages[0].Primitives;

        prims.OfType<DrawRectanglePrimitive>().Single(r => r.SourceElementId == "box")
            .LinkTarget.Should().Be("https://rect.example.com");
        var plain = prims.OfType<DrawTextPrimitive>().Single(t => t.Text == "semlink");
        plain.LinkTarget.Should().BeNull("the parent rectangle's link must not leak onto a plain child");
        var own = prims.OfType<DrawTextPrimitive>().Single(t => t.Text == "comlink");
        own.LinkTarget.Should().Be("https://child.example.com", "a child keeps its OWN link, not the parent's");
    }

    [Fact]
    public async Task CanGrow_textbox_background_fill_grows_with_the_text()
    {
        var req = WithSingleRowDefinition(new TextBoxElement
        {
            Id = "tb",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 30.Mm(), 5.Mm()), // narrow + short → wraps to many lines
            CanGrow = true,
            Expression = "'Uma frase bem longa que vai quebrar em varias linhas dentro de uma caixa estreita'",
            Style = Style.Default with { BackColor = Color.LightGray },
        });
        var report = await new ReportPaginator().PaginateAsync(req);
        var prims = report.Pages[0].Primitives;
        var fill = prims.OfType<DrawRectanglePrimitive>().Single(r => r.Fill?.Color == Color.LightGray);
        var text = prims.OfType<DrawTextPrimitive>().First();
        // The fill grew past the declared 5mm to cover the wrapped text (matches the textbox's final height).
        fill.Bounds.Height.Should().Be(text.Bounds.Height);
        fill.Bounds.Height.Should().BeGreaterThan(Unit.FromMm(5));
    }

    [Fact]
    public async Task ReportName_global_renders_the_report_name()
    {
        // Closed bug: =Globals!ReportName imports to the bare identifier "ReportName", which the evaluator
        // used to resolve to null (empty). The paginator now seeds ctx.ReportName from def.Name.
        var req = WithSingleRowDefinition(new TextBoxElement
        {
            Id = "tb",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 60.Mm(), 8.Mm()),
            Expression = "ReportName",
        });
        var report = await new ReportPaginator().PaginateAsync(req);
        // WithSingleRowDefinition names the report "c" → that's what ReportName resolves to.
        report.Pages[0].Primitives.OfType<DrawTextPrimitive>().Select(t => t.Text).Should().Contain("c");
    }

    [Fact]
    public async Task TextBox_without_BackColor_emits_no_background_fill()
    {
        var req = WithSingleRowDefinition(new TextBoxElement
        {
            Id = "tb",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 50.Mm(), 8.Mm()),
            Expression = "'Olá'",
        });
        var report = await new ReportPaginator().PaginateAsync(req);
        // No BackColor → no fill rectangle (no regression for the default case).
        report.Pages[0].Primitives.OfType<DrawRectanglePrimitive>().Should().BeEmpty();
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
