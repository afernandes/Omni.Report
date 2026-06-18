using FluentAssertions;
using Reporting.Common;
using Reporting.Geometry;
using Reporting.Styling;
using Xunit;

namespace Reporting.Core.Tests;

public class StylingTests
{
    [Fact]
    public void Color_from_argb_and_rgb_helpers()
    {
        var c = Color.FromArgb(128, 10, 20, 30);
        c.A.Should().Be(128);
        c.R.Should().Be(10);

        var rgb = Color.FromRgb(1, 2, 3);
        rgb.A.Should().Be(255);
    }

    [Fact]
    public void Color_to_string_returns_hex()
    {
        Color.Black.ToString().Should().Be("#000000");
    }

    [Fact]
    public void Color_predefined_constants()
    {
        Color.Transparent.A.Should().Be(0);
        Color.White.R.Should().Be(255);
        Color.Red.R.Should().Be(255);
        Color.Green.G.Should().Be(128);
        Color.Blue.B.Should().Be(255);
        Color.Gray.R.Should().Be(128);
        Color.LightGray.R.Should().Be(211);
    }

    [Fact]
    public void Font_default_and_mutations()
    {
        Font.Default.Family.Should().Be("Arial");
        Font.Default.Size.Should().Be(10);

        var bigger = Font.Default.WithSize(16);
        bigger.Size.Should().Be(16);
        bigger.Family.Should().Be("Arial");

        var bold = Font.Default.AddStyle(FontStyle.Bold);
        bold.Style.Should().HaveFlag(FontStyle.Bold);

        var italic = bold.WithStyle(FontStyle.Italic);
        italic.Style.Should().Be(FontStyle.Italic);
    }

    [Fact]
    public void Border_uniform_with_side()
    {
        var side = new BorderSide(BorderLineStyle.Dashed, 1.Pt(), Color.Gray);
        var border = Border.Uniform(side);
        border.Left.Should().Be(side);
        border.Top.Should().Be(side);
        border.Right.Should().Be(side);
        border.Bottom.Should().Be(side);
    }

    [Fact]
    public void Border_none_constant()
    {
        Border.None.Left.IsVisible.Should().BeFalse();
        Border.None.Top.IsVisible.Should().BeFalse();
        Border.None.Right.IsVisible.Should().BeFalse();
        Border.None.Bottom.IsVisible.Should().BeFalse();
    }

    [Fact]
    public void Style_default_has_left_alignment_top_word_wrap()
    {
        var s = Style.Default;
        s.HorizontalAlignment.Should().Be(HorizontalAlignment.Left);
        s.VerticalAlignment.Should().Be(VerticalAlignment.Top);
        s.WordWrap.Should().BeTrue();
        s.Font.Should().BeNull();
    }

    [Fact]
    public void Style_with_record_mutation()
    {
        var s = new Style(
            Font: Font.Default.WithSize(12),
            ForeColor: Color.Red,
            BackColor: Color.White,
            Border: Border.None,
            Padding: Thickness.Uniform(1.Mm()),
            HorizontalAlignment: HorizontalAlignment.Right,
            VerticalAlignment: VerticalAlignment.Middle,
            WordWrap: false,
            Format: "C2");
        s.Format.Should().Be("C2");
        s.WordWrap.Should().BeFalse();
        s.HorizontalAlignment.Should().Be(HorizontalAlignment.Right);
    }
}

public class CommonTests
{
    [Fact]
    public void Equatable_array_empty_is_singleton_like()
    {
        EquatableArray<int>.Empty.Count.Should().Be(0);
        var explicitEmpty = new EquatableArray<int>(Array.Empty<int>());
        explicitEmpty.Equals(EquatableArray<int>.Empty).Should().BeTrue();
    }

    [Fact]
    public void Equatable_array_indexer_and_enumeration()
    {
        var arr = EquatableArray.Create(10, 20, 30);
        arr[1].Should().Be(20);
        arr.Should().HaveCount(3);
        arr.Sum().Should().Be(60);
    }

    [Fact]
    public void Equatable_array_inequality_operator()
    {
        var a = EquatableArray.Create(1, 2);
        var b = EquatableArray.Create(1, 3);
        var aCopy = a;
        (a != b).Should().BeTrue();
        (a != aCopy).Should().BeFalse();
    }

    [Fact]
    public void Equatable_array_from_enumerable()
    {
        var arr = EquatableArray.From(System.Linq.Enumerable.Range(1, 3));
        arr.Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    [Fact]
    public void Equatable_dictionary_structural_equality()
    {
        var a = new EquatableDictionary<string, int>(new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 });
        var b = new EquatableDictionary<string, int>(new Dictionary<string, int> { ["b"] = 2, ["a"] = 1 });
        var c = new EquatableDictionary<string, int>(new Dictionary<string, int> { ["a"] = 1 });

        a.Equals(b).Should().BeTrue();
        a.Equals(c).Should().BeFalse();
        a.GetHashCode().Should().Be(b.GetHashCode());
        (a == b).Should().BeTrue();
        (a == c).Should().BeFalse();
    }

    [Fact]
    public void Equatable_dictionary_indexer_and_keys()
    {
        var d = new EquatableDictionary<string, int>(new Dictionary<string, int> { ["x"] = 42 });
        d["x"].Should().Be(42);
        d.ContainsKey("x").Should().BeTrue();
        d.TryGetValue("x", out var v).Should().BeTrue();
        v.Should().Be(42);
        d.Keys.Should().Contain("x");
        d.Values.Should().Contain(42);
        d.Count.Should().Be(1);
    }
}

public class UnitConversionTests
{
    [Fact]
    public void Point_and_pixel_conversions()
    {
        Unit.FromPoint(72).ToInches().Should().BeApproximately(1, 0.001);
        Unit.FromInch(1).ToPoints().Should().BeApproximately(72, 0.001);
        Unit.FromPixels(96, 96).ToInches().Should().BeApproximately(1, 0.001);
        Unit.FromInch(1).ToPixels(96).Should().BeApproximately(96, 0.001);
    }

    [Fact]
    public void Cm_conversions()
    {
        Unit.FromCm(2.54).ToInches().Should().BeApproximately(1, 0.001);
        Unit.FromInch(1).ToCm().Should().BeApproximately(2.54, 0.001);
    }

    [Fact]
    public void Division_and_negation()
    {
        var u = 100.Mm();
        (u / 2).Mils.Should().Be(u.Mils / 2);
        (-u).Mils.Should().Be(-u.Mils);
    }

    [Fact]
    public void Multiply_by_double()
    {
        var u = 10.Mm();
        (u * 0.5).Mils.Should().Be((int)Math.Round(u.Mils * 0.5));
    }

    [Fact]
    public void Comparable_and_string()
    {
        Unit.Zero.CompareTo(10.Mm()).Should().BeLessThan(0);
        10.Mm().ToString().Should().Contain("mm");
    }

    [Fact]
    public void Point_origin_and_size_empty()
    {
        Point.Origin.X.Should().Be(Unit.Zero);
        Size.Empty.IsEmpty.Should().BeTrue();
        (Point.Origin + new Size(10.Mm(), 5.Mm())).X.Should().Be(10.Mm());
        (Point.Origin - new Size(10.Mm(), 5.Mm())).X.Should().Be(-10.Mm());
    }

    [Fact]
    public void Size_arithmetic()
    {
        var a = new Size(20.Mm(), 10.Mm());
        var b = new Size(5.Mm(), 3.Mm());
        // Mils-based conversions can drift by 1 mil; assert via mm with tolerance.
        (a + b).Width.ToMm().Should().BeApproximately(25, 0.1);
        (a - b).Width.ToMm().Should().BeApproximately(15, 0.1);
    }

    [Fact]
    public void Rectangle_helpers()
    {
        Rectangle.Empty.Width.Should().Be(Unit.Zero);
        var r = Rectangle.FromLocationSize(new Point(5.Mm(), 5.Mm()), new Size(10.Mm(), 10.Mm()));
        r.X.Should().Be(5.Mm());
        r.Size.Width.Should().Be(10.Mm());
        r.Location.X.Should().Be(5.Mm());
    }

    [Fact]
    public void Thickness_zero_constant()
    {
        Thickness.Zero.Left.Should().Be(Unit.Zero);
        Thickness.Zero.Horizontal.Should().Be(Unit.Zero);
    }
}
