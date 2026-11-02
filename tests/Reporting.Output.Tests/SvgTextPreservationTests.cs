using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;
using Reporting.Common;
using Reporting.Geometry;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Reporting.Output.Html;
using Reporting.Output.Svg;
using Reporting.Paper;
using Reporting.Rendering;
using Reporting.Styling;
using SkiaSharp;
using Xunit;

namespace Reporting.Output.Tests;

public sealed class SvgTextPreservationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Export_PreservesOriginalUnicodeAndXmlCharacters(bool html)
    {
        // NBSP and the ohm sign can share glyphs with space and omega: a glyph-to-Unicode
        // reverse lookup cannot recover the original text, even when the native lookup succeeds.
        const string text = "A\u00a0B \u2126 & <tag> \" ' \U0001f600";
        var report = Report(Text(text));
        IReportExporter exporter = html ? new SvgHtmlExporter() : new SvgExporter();

        foreach (var bytes in new[] { exporter.ExportToBytes(report), await exporter.ExportToBytesAsync(report) })
        {
            var nodes = TextNodes(bytes);
            string.Concat(nodes.Select(t => t.Value)).Should().Be(text);
            nodes.Sum(t => Coordinates(t, "x").Length).Should().Be(text.EnumerateRunes().Count());
        }
    }

    [Fact]
    public void Export_InvalidXmlCharactersDoNotCorruptDocumentOrShiftFollowingGlyphs()
    {
        var nodes = TextNodes(new SvgExporter().ExportToBytes(Report(Text("A\u0001B\ud800C"))));

        string.Concat(nodes.Select(t => t.Value)).Should().Be("AB\ufffdC");
        nodes.Sum(t => Coordinates(t, "x").Length).Should().Be(4);
        // The omitted control's advance still belongs to the original run's geometry.
        using var font = new SKFont(SKTypeface.FromFamilyName("Arial"), 10);
        var positions = font.GetGlyphPositions(font.GetGlyphs(Encoding.UTF8.GetBytes("A\u0001B"), SKTextEncoding.Utf8));
        var x = Coordinates(nodes[0], "x");
        (x[1] - x[0]).Should().BeApproximately(positions[2].X, 0.001f);
    }

    [Fact]
    public void RenderPage_PreservesSpacesAndGlyphPositions()
    {
        const string text = "  A  B  ";
        var page = Report(Text(text)).Pages[0];
        var node = XDocument.Parse(new SvgExporter().RenderPage(page).SvgMarkup).Descendants()
            .Single(e => e.Name.LocalName == "text");

        node.Value.Should().Be(text);
        node.Attribute(XNamespace.Xml + "space")!.Value.Should().Be("preserve");
        var x = Coordinates(node, "x");
        x.Should().HaveCount(text.Length);
        using var font = new SKFont(SKTypeface.FromFamilyName("Arial"), 10);
        var advances = font.GetGlyphWidths(font.GetGlyphs(text));
        x[0].Should().BeApproximately(20, 0.1f);
        for (int i = 1; i < x.Length; i++)
        {
            (x[i] - x[i - 1]).Should().BeApproximately(advances[i - 1], 0.001f);
        }
    }

    [Fact]
    public void Export_ClippedRunDoesNotDisplaceFollowingTextOrItsStyle()
    {
        var report = Report(
            Text("hidden") with { ClipBounds = Box(300, 300, 10, 10) },
            Text("visible & intact") with
            {
                ClipBounds = Box(20, 20, 70, 20),
                Style = TextStyle.Default with { ForeColor = Color.Red, WordWrap = false },
            },
            Text("last", 50));
        var nodes = TextNodes(new SvgExporter().ExportToBytes(report));

        nodes.Select(t => t.Value.Trim('\r', '\n', '\t')).Should().Equal("visible & intact", "last");
        nodes[0].Attribute("fill")!.Value.Should().Be("red");
        nodes[0].Ancestors().Should().Contain(e => e.Attribute("clip-path") != null);
        nodes[0].Attribute("font-size")!.Value.Should().Be("10");
        Coordinates(nodes[1], "y")[0].Should().BeGreaterThan(Coordinates(nodes[0], "y")[0]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentExports_PreserveEveryLabelAcrossPages(bool html)
    {
        var expected = Enumerable.Range(0, 120).Select(i => $"Item {i} & value {i * 3}").ToArray();
        var pages = expected.Chunk(40).Select((labels, page) => new RenderedPage(page + 1,
            PageSetup.A4Portrait, EquatableArray.From<LayoutPrimitive>(labels.Select((label, row) => Text(label, 20 + row * 18)))));
        var report = new RenderedReport("Concurrent SVG", EquatableArray.From(pages));
        IReportExporter exporter = html ? new SvgHtmlExporter() : new SvgExporter();

        await Task.WhenAll(Enumerable.Range(0, 6).Select(worker => Task.Run(async () =>
        {
            for (int iteration = 0; iteration < 3; iteration++)
            {
                var bytes = iteration % 2 == 0 ? exporter.ExportToBytes(report) : await exporter.ExportToBytesAsync(report);
                TextNodes(bytes).Select(t => t.Value.Trim('\r', '\n', '\t')).Should().Equal(expected,
                    $"worker {worker}, iteration {iteration}: successful exports must not silently drop text");
            }
        })));
    }

    private static float[] Coordinates(XElement node, string name) => node.Attribute(name)!.Value
        .Split([',', ' '], StringSplitOptions.RemoveEmptyEntries)
        .Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();

    private static XElement[] TextNodes(byte[] bytes) => Regex.Matches(Encoding.UTF8.GetString(bytes),
        @"<svg\b.*?</svg>", RegexOptions.Singleline).SelectMany(m => XDocument.Parse(m.Value).Descendants()
            .Where(e => e.Name.LocalName == "text")).ToArray();

    private static RenderedReport Report(params LayoutPrimitive[] primitives) => new("SVG text",
        EquatableArray.Create(new RenderedPage(1, PageSetup.A4Portrait, EquatableArray.Create(primitives))));

    private static DrawTextPrimitive Text(string text, double y = 20) => new()
    {
        Text = text,
        Bounds = Box(20, y, 500, 20),
        Style = TextStyle.Default with { WordWrap = false },
    };

    private static Rectangle Box(double x, double y, double width, double height) => new(
        Unit.FromPoint(x), Unit.FromPoint(y), Unit.FromPoint(width), Unit.FromPoint(height));
}
