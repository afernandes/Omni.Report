using System.Text;
using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Output.Pdf;
using Reporting.Samples.CodeFirst.Reports;
using UglyToad.PdfPig;
using Xunit;

namespace Reporting.Output.Pdf.Tests;

/// <summary>
/// Locks the guarantee that an exported PDF carries its own fonts.
/// </summary>
/// <remarks>
/// <para><b>Why this is a guard and not a feature.</b> The roadmap listed font embedding as missing, inferring
/// it from <c>EmbedFont</c>/<c>FontSubset</c> having no occurrences in <c>src/</c>. Inspecting an actual export
/// showed the opposite: Skia's PDF backend already embeds and subsets by default, so the PDFs were correct all
/// along. What was genuinely missing is what these tests add — nothing verified it.</para>
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

        // PDF names a subsetted font "ABCDEF+Real-Name" — six uppercase letters and a plus. Losing the prefix
        // means the whole font got embedded, which for a CJK or a large corporate family turns a small report
        // into a multi-megabyte file.
        foreach (var name in fontNames)
        {
            name.Should().MatchRegex("^[A-Z]{6}\\+.+",
                $"'{name}' should be a subset — only the glyphs actually used belong in the file");
        }
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
