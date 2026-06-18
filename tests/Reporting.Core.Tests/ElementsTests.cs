using FluentAssertions;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Styling;
using Xunit;

namespace Reporting.Core.Tests;

public class ElementsTests
{
    [Fact]
    public void TextBox_can_grow_and_shrink_independently()
    {
        var tb = new TextBoxElement
        {
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 50.Mm(), 6.Mm()),
            Expression = "Fields.X",
            CanGrow = true,
            CanShrink = false,
        };
        tb.CanGrow.Should().BeTrue();
        tb.CanShrink.Should().BeFalse();
    }

    [Fact]
    public void Label_holds_literal_text()
    {
        var lbl = new LabelElement { Bounds = Rectangle.Empty, Text = "Hello" };
        lbl.Text.Should().Be("Hello");
    }

    [Fact]
    public void Line_default_pen_is_solid_thin_black()
    {
        var line = new LineElement { Bounds = Rectangle.Empty };
        line.Direction.Should().Be(LineDirection.TopLeftToBottomRight);
        line.Pen.Style.Should().Be(BorderLineStyle.Solid);
        line.Pen.IsVisible.Should().BeTrue();
    }

    [Theory]
    [InlineData(LineDirection.Horizontal)]
    [InlineData(LineDirection.Vertical)]
    [InlineData(LineDirection.BottomLeftToTopRight)]
    public void Line_directions_are_assignable(LineDirection direction)
    {
        var line = new LineElement { Bounds = Rectangle.Empty, Direction = direction };
        line.Direction.Should().Be(direction);
    }

    [Fact]
    public void Rectangle_and_ellipse_accept_fill_color()
    {
        var rect = new RectangleElement { Bounds = Rectangle.Empty, FillColor = Color.LightGray, CornerRadius = 2.Mm() };
        rect.FillColor.Should().Be(Color.LightGray);
        rect.CornerRadius.Should().Be(2.Mm());

        var ell = new EllipseElement { Bounds = Rectangle.Empty, FillColor = Color.Blue };
        ell.FillColor.Should().Be(Color.Blue);
    }

    [Fact]
    public void Image_supports_three_source_kinds()
    {
        new ImageElement { Bounds = Rectangle.Empty, Source = ImageSourceKind.Path, Path = "logo.png" }
            .Source.Should().Be(ImageSourceKind.Path);
        new ImageElement { Bounds = Rectangle.Empty, Source = ImageSourceKind.Expression, Expression = "Fields.Foto" }
            .Expression.Should().Be("Fields.Foto");
        new ImageElement { Bounds = Rectangle.Empty, Source = ImageSourceKind.Inline, InlineData = new byte[] { 1, 2, 3 } }
            .InlineData.Count.Should().Be(3);
    }

    [Theory]
    [InlineData(ImageSizing.Stretch)]
    [InlineData(ImageSizing.Fit)]
    [InlineData(ImageSizing.Fill)]
    [InlineData(ImageSizing.Native)]
    public void Image_sizing_modes(ImageSizing sizing)
    {
        var img = new ImageElement { Bounds = Rectangle.Empty, Sizing = sizing };
        img.Sizing.Should().Be(sizing);
    }

    [Fact]
    public void Barcode_holds_symbology_and_expression()
    {
        var bc = new BarcodeElement
        {
            Bounds = Rectangle.Empty,
            Symbology = BarcodeSymbology.QrCode,
            Expression = "Fields.Chave",
            ShowText = false,
        };
        bc.Symbology.Should().Be(BarcodeSymbology.QrCode);
        bc.Expression.Should().Be("Fields.Chave");
        bc.ShowText.Should().BeFalse();
    }

    [Fact]
    public void Chart_with_series_holds_them_as_value_equality_array()
    {
        var s1 = new ChartSeries("Vendas", "Fields.Mes", "Fields.Total", Color.Red);
        var s2 = new ChartSeries("Vendas", "Fields.Mes", "Fields.Total", Color.Red);
        var chart = new ChartElement
        {
            Bounds = Rectangle.Empty,
            Kind = ChartKind.Bar,
            Title = "Vendas mensais",
            ShowLegend = false,
            Series = EquatableArray.Create(s1),
        };
        chart.Kind.Should().Be(ChartKind.Bar);
        chart.Title.Should().Be("Vendas mensais");
        chart.ShowLegend.Should().BeFalse();
        s1.Should().Be(s2);
    }

    [Fact]
    public void Subreport_can_carry_inline_definition()
    {
        var inner = ReportDefinition.Empty("inner");
        var sub = new SubreportElement
        {
            Bounds = Rectangle.Empty,
            InlineDefinition = inner,
            ParameterBindings = new EquatableDictionary<string, string>(
                new Dictionary<string, string> { ["X"] = "Fields.Y" }),
            DataExpression = "Fields.Filhos",
        };
        sub.InlineDefinition.Should().Be(inner);
        sub.ParameterBindings["X"].Should().Be("Fields.Y");
        sub.DataExpression.Should().Be("Fields.Filhos");
    }

    [Fact]
    public void Table_columns_and_heights()
    {
        var col = new TableColumn("Cliente", 50.Mm(), HeaderText: "Cliente");
        var table = new TableElement
        {
            Bounds = Rectangle.Empty,
            Columns = EquatableArray.Create(col),
            HeaderHeight = 8.Mm(),
            DetailHeight = 6.Mm(),
            FooterHeight = 6.Mm(),
            DataExpression = "Fields.Linhas",
        };
        table.Columns[0].Width.Should().Be(50.Mm());
        table.HeaderHeight.Should().Be(8.Mm());
        table.FooterHeight.Should().Be(6.Mm());
        table.DataExpression.Should().Be("Fields.Linhas");
    }

    [Fact]
    public void Element_id_defaults_to_unique_guid()
    {
        var a = new LabelElement { Bounds = Rectangle.Empty, Text = "" };
        var b = new LabelElement { Bounds = Rectangle.Empty, Text = "" };
        a.Id.Should().NotBe(b.Id);
        a.Id.Should().HaveLength(32);
    }

    [Fact]
    public void Conditional_format_stores_condition_and_style()
    {
        var cf = new ConditionalFormat("Fields.Total > 100", new Style(ForeColor: Color.Red));
        cf.Condition.Should().Be("Fields.Total > 100");
        cf.Style.ForeColor.Should().Be(Color.Red);
    }
}
