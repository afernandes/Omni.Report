using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Elements;
using Reporting.Styling;
using Xunit;

namespace Reporting.CodeFirst.Tests;

public class BandContentTests
{
    private static BandContent Build(Action<BandContent> configure)
    {
        var b = new BandContent();
        configure(b);
        return b;
    }

    [Fact]
    public void Each_element_starter_produces_correct_concrete_type()
    {
        var b = Build(c => c
            .Text("Fields.X")
            .Label("hello")
            .Line()
            .Rectangle()
            .Ellipse()
            .Image(path: "logo.png")
            .Barcode("Fields.Code"));
        var elements = b.BuildElements();
        elements.Select(e => e.GetType().Name).Should().BeEquivalentTo(new[]
        {
            nameof(TextBoxElement), nameof(LabelElement), nameof(LineElement),
            nameof(RectangleElement), nameof(EllipseElement), nameof(ImageElement), nameof(BarcodeElement),
        });
    }

    [Fact]
    public void At_and_size_position_pending_element()
    {
        var b = Build(c => c.Text("Fields.X").At(10, 20).Size(80, 6));
        var element = (TextBoxElement)b.BuildElements()[0];
        element.Bounds.X.ToMm().Should().BeApproximately(10, 0.1);
        element.Bounds.Y.ToMm().Should().BeApproximately(20, 0.1);
        element.Bounds.Width.ToMm().Should().BeApproximately(80, 0.1);
        element.Bounds.Height.ToMm().Should().BeApproximately(6, 0.1);
    }

    [Fact]
    public void Bounds_helper_sets_all_at_once()
    {
        var b = Build(c => c.Label("x").Bounds(1, 2, 30, 5));
        var lbl = (LabelElement)b.BuildElements()[0];
        lbl.Bounds.X.ToMm().Should().BeApproximately(1, 0.1);
        lbl.Bounds.Width.ToMm().Should().BeApproximately(30, 0.1);
    }

    [Fact]
    public void Font_helpers_stack()
    {
        var b = Build(c => c.Text("e").Font("Arial", 12).Bold().Italic().Underline());
        var tb = (TextBoxElement)b.BuildElements()[0];
        tb.Style.Font!.Family.Should().Be("Arial");
        tb.Style.Font.Size.Should().Be(12);
        tb.Style.Font.Style.Should().HaveFlag(FontStyle.Bold);
        tb.Style.Font.Style.Should().HaveFlag(FontStyle.Italic);
        tb.Style.Font.Style.Should().HaveFlag(FontStyle.Underline);
    }

    [Fact]
    public void Font_size_changes_only_size()
    {
        var b = Build(c => c.Text("e").FontSize(14));
        var tb = (TextBoxElement)b.BuildElements()[0];
        tb.Style.Font!.Size.Should().Be(14);
    }

    [Fact]
    public void BackgroundGradient_sets_start_end_and_direction()
    {
        var b = Build(c => c.Text("e").BackgroundGradient(
            Color.FromRgb(255, 0, 0), Color.FromRgb(0, 0, 255), BackgroundGradientType.LeftRight));
        var tb = (TextBoxElement)b.BuildElements()[0];
        tb.Style.BackColor.Should().Be(Color.FromRgb(255, 0, 0), "start is the BackColor");
        tb.Style.BackColorEnd.Should().Be(Color.FromRgb(0, 0, 255));
        tb.Style.BackgroundGradient.Should().Be(BackgroundGradientType.LeftRight);
    }

    [Theory]
    [InlineData("AlignLeft", HorizontalAlignment.Left)]
    [InlineData("AlignRight", HorizontalAlignment.Right)]
    [InlineData("Center", HorizontalAlignment.Center)]
    public void Horizontal_alignment_setters(string method, HorizontalAlignment expected)
    {
        var b = new BandContent();
        b.Text("e");
        var m = typeof(BandContent).GetMethod(method)!;
        m.Invoke(b, null);
        var tb = (TextBoxElement)b.BuildElements()[0];
        tb.Style.HorizontalAlignment.Should().Be(expected);
    }

    [Fact]
    public void Vertical_alignment_setters()
    {
        var top = Build(c => c.Text("e").AlignTop());
        var mid = Build(c => c.Text("e").AlignMiddle());
        var bot = Build(c => c.Text("e").AlignBottom());
        ((TextBoxElement)top.BuildElements()[0]).Style.VerticalAlignment.Should().Be(VerticalAlignment.Top);
        ((TextBoxElement)mid.BuildElements()[0]).Style.VerticalAlignment.Should().Be(VerticalAlignment.Middle);
        ((TextBoxElement)bot.BuildElements()[0]).Style.VerticalAlignment.Should().Be(VerticalAlignment.Bottom);
    }

    [Fact]
    public void Color_and_background()
    {
        var b = Build(c => c.Text("e").Color(Color.Red).Background(Color.LightGray));
        var tb = (TextBoxElement)b.BuildElements()[0];
        tb.Style.ForeColor.Should().Be(Color.Red);
        tb.Style.BackColor.Should().Be(Color.LightGray);
    }

    [Fact]
    public void NoWrap_and_format()
    {
        var b = Build(c => c.Text("e").NoWrap().Format("C2"));
        var tb = (TextBoxElement)b.BuildElements()[0];
        tb.Style.WordWrap.Should().BeFalse();
        tb.Style.Format.Should().Be("C2");
    }

    [Fact]
    public void Border_helpers()
    {
        var b = Build(c => c.Text("e").Border(BorderLineStyle.Solid, 1, Color.Black));
        var tb = (TextBoxElement)b.BuildElements()[0];
        tb.Style.Border.Should().NotBeNull();
        tb.Style.Border!.Top.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void Line_from_to_thickness()
    {
        var b = Build(c => c.Line().From(0, 0).To(100, 0).Thickness(0.5).Direction(LineDirection.Horizontal));
        var line = (LineElement)b.BuildElements()[0];
        line.Bounds.X.ToMm().Should().BeApproximately(0, 0.1);
        line.Bounds.Width.ToMm().Should().BeApproximately(100, 0.1);
        line.Direction.Should().Be(LineDirection.Horizontal);
        line.Pen.Thickness.ToPoints().Should().BeApproximately(0.5, 0.05);
    }

    [Fact]
    public void Rectangle_fill_and_corner_radius()
    {
        var b = Build(c => c.Rectangle().Fill(Color.LightGray).CornerRadius(2));
        var r = (RectangleElement)b.BuildElements()[0];
        r.FillColor.Should().Be(Color.LightGray);
        r.CornerRadius.ToMm().Should().BeApproximately(2, 0.1);
    }

    [Fact]
    public void Rectangle_as_container_holds_children_with_relative_bounds()
    {
        var b = Build(c => c
            .Rectangle(box => box
                .Label("Resumo").At(2, 2).Size(30, 6)
                .Text("Fields.Total").At(2, 10).Size(40, 6))
            .At(10, 10).Size(80, 40).Fill(Color.LightGray));
        var r = (RectangleElement)b.BuildElements()[0];
        r.FillColor.Should().Be(Color.LightGray);
        r.Bounds.X.ToMm().Should().BeApproximately(10, 0.1); // config after Rectangle(...) applies to the rect
        r.Children.Should().HaveCount(2);
        r.Children[0].Should().BeOfType<LabelElement>().Which.Bounds.X.ToMm().Should().BeApproximately(2, 0.1);
        r.Children[1].Should().BeOfType<TextBoxElement>();
    }

    [Fact]
    public void Ellipse_fill()
    {
        var b = Build(c => c.Ellipse().Fill(Color.Red));
        var el = (EllipseElement)b.BuildElements()[0];
        el.FillColor.Should().Be(Color.Red);
    }

    [Fact]
    public void Image_sources_distinguished()
    {
        var inline = Build(c => c.Image(bytes: [1, 2, 3]));
        var path = Build(c => c.Image(path: "logo.png"));
        var expr = Build(c => c.Image(expression: "Fields.Photo"));
        ((ImageElement)inline.BuildElements()[0]).Source.Should().Be(ImageSourceKind.Inline);
        ((ImageElement)path.BuildElements()[0]).Source.Should().Be(ImageSourceKind.Path);
        ((ImageElement)expr.BuildElements()[0]).Source.Should().Be(ImageSourceKind.Expression);
    }

    [Fact]
    public void Image_sizing_set()
    {
        var b = Build(c => c.Image(path: "x.png").ImageSizing(ImageSizing.Fill));
        ((ImageElement)b.BuildElements()[0]).Sizing.Should().Be(ImageSizing.Fill);
    }

    [Fact]
    public void Barcode_with_symbology()
    {
        var b = Build(c => c.Barcode("Fields.Ean", BarcodeSymbology.Ean13));
        var bc = (BarcodeElement)b.BuildElements()[0];
        bc.Symbology.Should().Be(BarcodeSymbology.Ean13);
        bc.Expression.Should().Be("Fields.Ean");
    }

    [Fact]
    public void Band_height_settable_in_mm_or_unit()
    {
        var b1 = Build(c => c.Height(8));
        var b2 = Build(c => c.Height(Reporting.Geometry.Unit.FromMm(8)));
        b1.BandHeight.Should().Be(b2.BandHeight);
    }

    [Fact]
    public void Detail_can_grow_shrink_flags()
    {
        var b = Build(c => c.Height(6).CanGrow().CanShrink());
        b.DetailCanGrow.Should().BeTrue();
        b.DetailCanShrink.Should().BeTrue();
    }

    [Fact]
    public void Element_visible_if_and_hidden_flags()
    {
        var b = Build(c => c
            .Label("a").Hidden()
            .Label("b").VisibleIf("Fields.X > 0"));
        var elements = b.BuildElements();
        elements[0].Visible.Should().BeFalse();
        elements[1].VisibleExpression.Should().Be("Fields.X > 0");
    }

    [Fact]
    public void Element_can_grow_only_on_textbox()
    {
        var b = Build(c => c.Text("e").ElementCanGrow().ElementCanShrink());
        var tb = (TextBoxElement)b.BuildElements()[0];
        tb.CanGrow.Should().BeTrue();
        tb.CanShrink.Should().BeTrue();
    }

    [Fact]
    public void Conditional_format_appends_rule()
    {
        var b = Build(c => c
            .Text("e")
            .ConditionalFormat("Fields.X > 100", new Style(ForeColor: Color.Red)));
        var tb = (TextBoxElement)b.BuildElements()[0];
        tb.ConditionalFormats.Should().HaveCount(1);
    }

    [Fact]
    public void Element_name_persists()
    {
        var b = Build(c => c.Text("e").Name("titulo"));
        b.BuildElements()[0].Name.Should().Be("titulo");
    }

    [Fact]
    public void Band_visible_when()
    {
        var b = Build(c => c.VisibleWhen("Parameters.Mostrar"));
        b.VisibleExpression.Should().Be("Parameters.Mostrar");
    }

    [Fact]
    public void Print_on_first_last_page_flags()
    {
        var b = Build(c => c.NotOnFirstPage().NotOnLastPage());
        b.PrintOnFirstPage.Should().BeFalse();
        b.PrintOnLastPage.Should().BeFalse();
    }
}
