using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Common;
using Reporting.Geometry;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Reporting.Output.Pdf;
using Reporting.Paper;
using Reporting.Rendering;
using Reporting.Samples.CodeFirst.Reports;
using Reporting.Styling;
using SkiaSharp;
using UglyToad.PdfPig;
using Xunit;

namespace Reporting.Output.Pdf.Tests;

/// <summary>
/// Locks the guarantee that an exported PDF carries its own fonts.
/// </summary>
/// <remarks>
/// <para><b>Why this is a guard and not a feature.</b> The roadmap listed font embedding as missing, inferring
/// it from <c>EmbedFont</c>/<c>FontSubset</c> having no occurrences in <c>src/</c>. Inspecting an actual export
/// showed Skia's PDF backend does embed by default. It looked like it subsetted too — the font names carry the
/// "ABCDEF+" prefix — but that was only the name: the libSkiaSharp on NuGet is built without HarfBuzz, so Skia
/// embedded every font WHOLE (a one-page report weighed 1.1 MB). The exporter now subsets on its own
/// (<c>PdfFontSubsetter</c>), and the size assertions below are what tell a real subset from a renamed full font.</para>
///
/// <para>That matters because the behaviour is a <em>default</em>, not a decision this codebase makes. A Skia
/// upgrade, a different <c>SKDocument</c> creation path or a stray option could turn it off, and the failure is
/// invisible on the machine that produced the file: the PDF only misrenders on a reader that lacks the font.
/// For an invoice or a contract that is document alteration, discovered by the recipient.</para>
///
/// <para>Embedding is also a hard prerequisite of PDF/A, so this guard is the floor that work stands on.</para>
/// </remarks>
public class FontEmbeddingTests
{
    /// <summary>Counts non-overlapping occurrences of a PDF token in the raw file bytes.</summary>
    private static int CountToken(byte[] pdf, string token)
    {
        var raw = Encoding.Latin1.GetString(pdf);
        int n = 0, i = 0;
        while ((i = raw.IndexOf(token, i, StringComparison.Ordinal)) >= 0)
        {
            n++;
            i += token.Length;
        }
        return n;
    }

    [Fact]
    public async Task Exported_pdf_embeds_its_font_programs()
    {
        var report = await Sample01_VendasPorCliente.Build().PaginateAsync();
        var bytes = new SkiaPdfExporter().ExportToBytes(report);

        // /FontFile2 is the stream holding an embedded TrueType program. Without at least one, the reader has
        // to substitute, and the document renders with different metrics than the author saw.
        CountToken(bytes, "/FontFile2").Should().BeGreaterThan(0,
            "a PDF whose text depends on fonts installed on the reader's machine is not a faithful document");

        // One embedded program plus three substituted fonts would satisfy the assertion above while leaving
        // most of the document at risk, so compare against how many fonts the document actually uses.
        // Counting raw "/FontDescriptor" tokens would NOT work: in the Type0/CID structure Skia emits, the
        // token appears twice per font — once as the key in the descendant dictionary and once as the
        // /Type of the descriptor object itself.
        using var pdf = PdfDocument.Open(bytes);
        int distinctFonts = pdf.GetPages()
            .SelectMany(p => p.Letters)
            .Select(l => l.FontName)
            .Distinct()
            .Count();

        CountToken(bytes, "/FontFile2").Should().Be(distinctFonts,
            "every font the document uses must carry its program, not just the first");
    }

    [Fact]
    public async Task Embedded_fonts_are_subsetted()
    {
        var report = await Sample01_VendasPorCliente.Build().PaginateAsync();
        var bytes = new SkiaPdfExporter().ExportToBytes(report);

        using var pdf = PdfDocument.Open(bytes);
        var fontNames = pdf.GetPages()
            .SelectMany(p => p.Letters)
            .Select(l => l.FontName)
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct()
            .ToList();

        fontNames.Should().NotBeEmpty();

        // PDF names a subsetted font "ABCDEF+Real-Name" — six uppercase letters and a plus. The prefix is
        // necessary but NOT sufficient: Skia writes it even when it embeds the whole font, which is why
        // Embedded_font_programs_are_a_fraction_of_their_source_files checks the program size itself.
        foreach (var name in fontNames)
        {
            name.Should().MatchRegex("^[A-Z]{6}\\+.+",
                $"'{name}' should be a subset — only the glyphs actually used belong in the file");
        }
    }

    /// <summary>Reads the <c>/Length1</c> of every embedded TrueType program — the size of the font file
    /// BEFORE stream compression, which is what tells a subset apart from the whole font.</summary>
    private static List<long> EmbeddedProgramSizes(byte[] pdf)
        => Regex.Matches(Encoding.Latin1.GetString(pdf), @"/Length1 (\d+)")
            .Select(m => long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
            .ToList();

    /// <summary>Size of each font file the report's text resolves to on this machine — what the PDF would
    /// carry if it embedded the fonts whole. Resolved the way the renderer does, so on Linux it measures the
    /// font that actually substitutes "Arial", not a file that is not there.</summary>
    private static List<long> SourceFontFileSizes(RenderedReport report)
    {
        var sizes = new List<long>();
        foreach (var font in report.Pages.SelectMany(p => p.Primitives).OfType<DrawTextPrimitive>()
                     .Select(t => (t.Style.Font.Family, t.Style.Font.Style)).Distinct())
        {
            var style = new SKFontStyle(
                (font.Style & FontStyle.Bold) != 0 ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                (font.Style & FontStyle.Italic) != 0 ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
            using var typeface = SKTypeface.FromFamilyName(font.Family, style) ?? SKTypeface.Default;
            using var stream = typeface.OpenStream(out _);
            stream.Should().NotBeNull($"the font file behind '{font.Family}' must be readable to compare against");
            sizes.Add(stream!.Length);
        }
        return sizes;
    }

    [Fact]
    public async Task Embedded_font_programs_are_a_fraction_of_their_source_files()
    {
        var report = await Sample01_VendasPorCliente.Build().PaginateAsync();
        var bytes = new SkiaPdfExporter().ExportToBytes(report);

        var embedded = EmbeddedProgramSizes(bytes);
        var sources = SourceFontFileSizes(report);
        embedded.Should().NotBeEmpty();
        sources.Should().NotBeEmpty();

        // The "ABCDEF+" prefix asserted above is only a name: Skia writes it even when its subsetter is
        // compiled out and the whole program goes in. The program size is the actual guarantee. A report this
        // size uses well under a hundred glyphs of fonts that carry thousands, so each embedded program must be
        // a small fraction of the smallest file it could have come from — a whole font fails by an order of
        // magnitude, not by a margin a font update could cross.
        long smallestSource = sources.Min();
        foreach (var size in embedded)
        {
            size.Should().BeLessThan(smallestSource / 5,
                $"an embedded program of {size:N0} bytes against a {smallestSource:N0}-byte source file is the whole font, not the glyphs the page uses");
        }
    }

    [Fact]
    public async Task One_page_report_does_not_weigh_megabytes()
    {
        var report = await Sample01_VendasPorCliente.Build().PaginateAsync();
        report.PageCount.Should().Be(1, "the budget below is for a single page");

        var bytes = new SkiaPdfExporter().ExportToBytes(report);

        // Measured before the fix: 1,111,607 bytes, ~94% of it the two Arial programs. A one-page tabular report
        // is text, a few lines and two subsetted fonts — 100 KB is a generous ceiling for that.
        bytes.Length.Should().BeLessThan(100 * 1024,
            "a one-page report that weighs over a megabyte is carrying whole font files, which turns every e-mailed invoice into an attachment problem");
    }

    /// <summary>A page that exercises what subsetting could get wrong: three styles and three sizes of one family
    /// (one subset per style, checked at every size), accents and symbols beyond Latin-1, a character the primary
    /// font lacks (so it falls back to another typeface, which is subsetted too), wrapping and alignment (both
    /// computed with the substituted font) and a CR/LF line break.</summary>
    private static RenderedReport MixedFontsReport()
    {
        static DrawTextPrimitive Text(string text, Font font, double top, HorizontalAlignment align) => new()
        {
            Text = text,
            Bounds = new Rectangle(15.Mm(), top.Mm(), 70.Mm(), 30.Mm()),
            Style = new TextStyle(font, Color.Black, align),
        };
        return new RenderedReport("Fontes mistas", new EquatableArray<RenderedPage>(
        [
            new RenderedPage(1, PageSetup.A4Portrait, new EquatableArray<LayoutPrimitive>(
            [
                Text("Relatório de Vendas — ação, café, coração", new Font("Arial", 16, FontStyle.Bold), 15, HorizontalAlignment.Left),
                Text("Preço unitário € 12,50 · Ω · ±0,5% — texto longo que precisa quebrar em mais de uma linha",
                    new Font("Arial", 10), 50, HorizontalAlignment.Center),
                Text("Observação em itálico\r\nsegunda linha alinhada à direita", new Font("Arial", 7, FontStyle.Italic), 85, HorizontalAlignment.Right),
                Text("Conferido ✓ 漢字", new Font("Arial", 10), 120, HorizontalAlignment.Left),
            ])),
        ]));
    }

    public static TheoryData<string> ReportsToCompare() => ["sample-01", "mixed-fonts"];

    [Theory]
    [MemberData(nameof(ReportsToCompare))]
    public async Task Subsetting_does_not_move_a_single_glyph(string which)
    {
        var report = which == "sample-01"
            ? await Sample01_VendasPorCliente.Build().PaginateAsync()
            : MixedFontsReport();
        var fixedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var subsetted = new SkiaPdfExporter(new PdfExportOptions { CreationDate = fixedDate }).ExportToBytes(report);
        var whole = new SkiaPdfExporter(new PdfExportOptions { CreationDate = fixedDate, SubsetFonts = false }).ExportToBytes(report);

        // The subsets are drawn in place of the system typefaces — wrapping, alignment and glyph positions are all
        // recomputed with them. The layout engine measured with the originals, so anything but an exact match would
        // put text somewhere other than where pagination decided it fits. Start and end of each glyph's baseline pin
        // both its position and its advance.
        //
        // BoundingBox is deliberately NOT compared: PdfPig derives it from the embedded program, and the whole-font
        // export cannot be read there when the fallback font lives in a collection (Windows' CJK fallback, Yu Gothic,
        // is a .ttc) — Skia embedded the entire collection as /FontFile2, which is not a valid TrueType program. The
        // subset is a single face, so it is the only side of this comparison whose outlines are readable at all.
        using var expected = PdfDocument.Open(whole);
        using var actual = PdfDocument.Open(subsetted);
        actual.NumberOfPages.Should().Be(expected.NumberOfPages);
        for (int number = 1; number <= expected.NumberOfPages; number++)
        {
            actual.GetPage(number).Letters.Select(l => new { l.Value, l.StartBaseLine, l.EndBaseLine, l.FontName, l.FontSize })
                .Should().Equal(expected.GetPage(number).Letters.Select(l => new { l.Value, l.StartBaseLine, l.EndBaseLine, l.FontName, l.FontSize }),
                    $"page {number} must look the same whether its fonts are embedded whole or subsetted");
        }
        subsetted.Length.Should().BeLessThan(whole.Length / 5, "otherwise the subsets were not used");
    }

    [Fact]
    public async Task Text_stays_extractable_after_embedding()
    {
        var report = await Sample01_VendasPorCliente.Build().PaginateAsync();
        var bytes = new SkiaPdfExporter().ExportToBytes(report);

        using var pdf = PdfDocument.Open(bytes);
        var text = string.Join(" ", pdf.GetPages().Select(p => p.Text));

        // Subsetting re-maps glyph ids, so a broken ToUnicode map yields a PDF that looks right and copies out
        // as mojibake. Accented text is where that shows first.
        text.Should().Contain("Relatório de Vendas");
    }
}
