using FluentAssertions;
using Reporting.Geometry;
using Reporting.Rendering;
using Reporting.Styling;
using Xunit;

namespace Reporting.Rendering.Tests;

public class StyleAbstractionTests
{
    [Fact]
    public void TextStyle_default_is_arial_10_black()
    {
        TextStyle.Default.Font.Family.Should().Be("Arial");
        TextStyle.Default.Font.Size.Should().Be(10);
        TextStyle.Default.ForeColor.Should().Be(Color.Black);
    }

    [Fact]
    public void TextStyle_with_methods()
    {
        var s = TextStyle.Default.WithFont(new Font("Verdana", 12)).WithColor(Color.Red);
        s.Font.Family.Should().Be("Verdana");
        s.ForeColor.Should().Be(Color.Red);
    }

    [Fact]
    public void PenStyle_visibility()
    {
        PenStyle.Default.IsVisible.Should().BeTrue();
        new PenStyle(Color.Black, Unit.Zero).IsVisible.Should().BeFalse();
        new PenStyle(Color.Black, 1.Pt(), BorderLineStyle.None).IsVisible.Should().BeFalse();
        PenStyle.Thin.Thickness.Should().Be(Unit.FromPoint(0.25));
    }

    [Fact]
    public void PenStyle_from_border_side_returns_null_when_invisible()
    {
        PenStyle.FromBorderSide(BorderSide.None).Should().BeNull();
        PenStyle.FromBorderSide(new BorderSide(BorderLineStyle.Solid, 1.Pt(), Color.Black))
            .Should().NotBeNull();
    }

    [Fact]
    public void BrushStyle_visibility()
    {
        BrushStyle.Black.IsVisible.Should().BeTrue();
        BrushStyle.Transparent.IsVisible.Should().BeFalse();
        new BrushStyle(Color.FromArgb(0, 1, 2, 3)).IsVisible.Should().BeFalse();
    }
}

public class AverageWidthTextMeasurerTests
{
    [Fact]
    public void Empty_text_yields_zero_width_and_one_line_height()
    {
        var m = new AverageWidthTextMeasurer();
        var size = m.Measure(string.Empty, TextStyle.Default);
        size.Width.Should().Be(Unit.Zero);
        size.Height.Mils.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Width_proportional_to_character_count_when_no_max_width()
    {
        var m = new AverageWidthTextMeasurer();
        var style = TextStyle.Default;
        var short5 = m.Measure(new string('a', 5), style);
        var long10 = m.Measure(new string('a', 10), style);
        long10.Width.Mils.Should().BeGreaterThan(short5.Width.Mils);
    }

    [Fact]
    public void Wraps_to_multiple_lines_when_word_wrap_and_max_width()
    {
        var m = new AverageWidthTextMeasurer();
        var style = TextStyle.Default;
        var wrapped = m.Measure(new string('x', 200), style, maxWidth: 20.Mm());
        var single = m.Measure(new string('x', 5), style, maxWidth: 20.Mm());
        wrapped.Height.Mils.Should().BeGreaterThan(single.Height.Mils);
    }

    [Fact]
    public void No_wrap_when_word_wrap_false()
    {
        var m = new AverageWidthTextMeasurer();
        var style = TextStyle.Default with { WordWrap = false };
        var noWrap = m.Measure(new string('a', 100), style, maxWidth: 5.Mm());
        // Without wrap, height stays at one line.
        var oneLine = m.Measure("a", style);
        noWrap.Height.Should().Be(oneLine.Height);
    }

    [Fact]
    public void Zero_max_width_returns_zero_width()
    {
        var m = new AverageWidthTextMeasurer();
        var size = m.Measure("hello", TextStyle.Default, maxWidth: Unit.Zero);
        size.Width.Should().Be(Unit.Zero);
    }

    [Fact]
    public void Implements_text_measurer_interface()
    {
        ITextMeasurer m = new AverageWidthTextMeasurer();
        m.Should().NotBeNull();
        m.Measure("test", TextStyle.Default).Width.Mils.Should().BeGreaterThan(0);
    }
}
