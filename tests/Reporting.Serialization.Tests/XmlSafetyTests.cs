using System.Text;
using FluentAssertions;
using Reporting;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Paper;
using Xunit;

namespace Reporting.Serialization.Tests;

/// <summary>
/// XML safety regression coverage — content that would normally break a naive XML writer
/// (special characters in expressions/labels, malformed input, empty payload) must either
/// round-trip cleanly or surface a clear error. Bugs in this area silently corrupt user
/// reports on save and only show up at next load.
/// </summary>
public class XmlSafetyTests
{
    private static readonly RepxSerializer Serializer = new();

    [Theory]
    [InlineData("Tag <bold> & \"strong\"")]
    [InlineData("Cliente <test@example.com> 'aspas'")]
    [InlineData("AT&T & Cia. <NF-e>")]
    [InlineData("a < b && c > d")]
    [InlineData("Ñoño · Maçã · ñ &amp; ©")]
    public void Xml_special_chars_in_label_text_round_trip(string text)
    {
        var def = MakeLabelOnlyReport(text);
        var bytes = Serializer.SaveToBytes(def);
        var reloaded = Serializer.LoadFromBytes(bytes);

        var label = (LabelElement)reloaded.Detail.Elements[0];
        label.Text.Should().Be(text, "XML writer must escape special chars and reader must unescape");
    }

    [Theory]
    [InlineData("{Fields.Total} > 1000 ? 'alto' : 'baixo'")]
    [InlineData("IIF(Fields.X < 10 && Fields.Y > 0, 'A', 'B')")]
    [InlineData("'<script>alert(1)</script>'")]
    [InlineData("Sum(Fields.Total) & ' total'")]
    public void Xml_special_chars_in_textbox_expression_round_trip(string expression)
    {
        var def = MakeTextBoxOnlyReport(expression);
        var bytes = Serializer.SaveToBytes(def);
        var reloaded = Serializer.LoadFromBytes(bytes);

        var tb = (TextBoxElement)reloaded.Detail.Elements[0];
        tb.Expression.Should().Be(expression);
    }

    [Fact]
    public void Loading_empty_byte_array_throws_clean_exception_not_NRE()
    {
        // Empty file should NOT crash with NullReferenceException — it should raise a clear
        // load-time error so the host UI can show a user-friendly message.
        var thrown = FluentActions.Invoking(() => Serializer.LoadFromBytes(Array.Empty<byte>()))
            .Should().Throw<Exception>().Which;
        thrown.Should().NotBeOfType<NullReferenceException>(
            "deserialization failure must be a typed exception, not an NRE");
    }

    [Fact]
    public void Loading_truncated_xml_throws_clean_exception()
    {
        var truncated = Encoding.UTF8.GetBytes(
            "<?xml version=\"1.0\"?><Report Name=\"test\"><PageSetup");
        FluentActions.Invoking(() => Serializer.LoadFromBytes(truncated))
            .Should().Throw<Exception>()
            .Where(ex => ex.GetType() != typeof(NullReferenceException));
    }

    [Fact]
    public void Loading_random_bytes_throws_clean_exception()
    {
        var garbage = new byte[] { 0xFF, 0xFE, 0x00, 0x42, 0x99 };
        FluentActions.Invoking(() => Serializer.LoadFromBytes(garbage))
            .Should().Throw<Exception>()
            .Where(ex => ex.GetType() != typeof(NullReferenceException));
    }

    [Fact]
    public void Loading_xml_that_is_not_a_report_throws_clean_exception()
    {
        var notAReport = Encoding.UTF8.GetBytes(
            "<?xml version=\"1.0\"?><SomethingElse><Child /></SomethingElse>");
        FluentActions.Invoking(() => Serializer.LoadFromBytes(notAReport))
            .Should().Throw<Exception>()
            .Where(ex => ex.GetType() != typeof(NullReferenceException));
    }

    [Fact]
    public void Unicode_combining_chars_round_trip()
    {
        // Brazilian portuguese loves combining accents — make sure NFC/NFD don't get
        // mangled by the XML pipeline.
        var def = MakeLabelOnlyReport("Açaí · São João · CRT-1 — ✓");
        var bytes = Serializer.SaveToBytes(def);
        var reloaded = Serializer.LoadFromBytes(bytes);
        ((LabelElement)reloaded.Detail.Elements[0]).Text
            .Should().Be("Açaí · São João · CRT-1 — ✓");
    }

    [Fact]
    public void Long_text_with_newlines_round_trips_with_xml_normalization()
    {
        // XML 1.0 §2.11 normalizes \r\n → \n on parse. We can't expect byte-exact, but the
        // surrounding content and special chars MUST be preserved.
        var multi = "Linha 1\nLinha 2\r\nLinha 3 com & < > \"";
        var def = MakeLabelOnlyReport(multi);
        var bytes = Serializer.SaveToBytes(def);
        var reloaded = Serializer.LoadFromBytes(bytes);
        var back = ((LabelElement)reloaded.Detail.Elements[0]).Text;
        back.Should().Contain("Linha 1");
        back.Should().Contain("Linha 2");
        back.Should().Contain("Linha 3 com & < > \"");
        back.Split('\n').Length.Should().BeGreaterOrEqualTo(3, "newlines preserved as separators");
    }

    // ── helpers ─────────────────────────────────────────────────────────────────

    private static ReportDefinition MakeLabelOnlyReport(string text)
    {
        var detail = new DetailBand(Unit.FromMm(8),
            EquatableArray.Create<ReportElement>(
                new LabelElement
                {
                    Id = "lbl1",
                    Bounds = new Rectangle(0.Mm(), 0.Mm(), 100.Mm(), 6.Mm()),
                    Text = text,
                }));
        return new ReportDefinition("XmlSafety", PageSetup.A4Portrait, detail);
    }

    private static ReportDefinition MakeTextBoxOnlyReport(string expression)
    {
        var detail = new DetailBand(Unit.FromMm(8),
            EquatableArray.Create<ReportElement>(
                new TextBoxElement
                {
                    Id = "tb1",
                    Bounds = new Rectangle(0.Mm(), 0.Mm(), 100.Mm(), 6.Mm()),
                    Expression = expression,
                }));
        return new ReportDefinition("XmlSafety", PageSetup.A4Portrait, detail);
    }
}
