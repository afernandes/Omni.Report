using FluentAssertions;
using Reporting.Barcode;
using Xunit;

namespace Reporting.Barcode.Tests;

/// <summary>Smoke tests for the 1D encoders: encoding-not-crashing + geometry sanity
/// (positive viewbox, monotonically-increasing X, expected checksum digits where
/// applicable). We don't snapshot the full bar list — the algorithms are deterministic
/// and porting-faithful; what matters is the public contract holds.</summary>
public class Barcode1DEncoderTests
{
    [Theory]
    [InlineData(BarcodeSymbology.Code128, "Hello, World!")]
    [InlineData(BarcodeSymbology.Code39,  "OMNI-REPORT")]
    [InlineData(BarcodeSymbology.Codabar, "1234567")]
    [InlineData(BarcodeSymbology.Itf,     "1234567890")]
    public void Symbol_textual_inputs_encode_into_increasing_bars(BarcodeSymbology s, string value)
    {
        var g = Barcode1DEncoder.Encode(s, value);

        g.Bars.Should().NotBeEmpty("the symbology must produce at least the start + stop bars");
        g.ViewBoxWidth.Should().BePositive();
        g.ViewBoxHeight.Should().BePositive();

        // Bars are emitted left-to-right; X must never go backwards.
        double prevX = -1;
        foreach (var bar in g.Bars)
        {
            bar.X.Should().BeGreaterThanOrEqualTo(prevX, "bar X coordinates must be monotonic");
            bar.Width.Should().BePositive();
            prevX = bar.X;
        }
    }

    [Fact]
    public void Ean13_computes_check_digit_when_12_digits_supplied()
    {
        // Famous test value 590123412345 — standard EAN-13 check digit = 7.
        var g = Barcode1DEncoder.Encode(BarcodeSymbology.Ean13, "590123412345");
        g.Checksum.Should().Be("7");
    }

    [Fact]
    public void Ean13_validates_check_digit_when_13_digits_supplied()
    {
        // 5901234123457 is the same number with the correct check appended.
        var g = Barcode1DEncoder.Encode(BarcodeSymbology.Ean13, "5901234123457");
        g.Checksum.Should().Be("7");
    }

    [Fact]
    public void Ean13_throws_on_wrong_check_digit()
    {
        // 8 is wrong (correct check is 7).
        var act = () => Barcode1DEncoder.Encode(BarcodeSymbology.Ean13, "5901234123458");
        act.Should().Throw<ArgumentException>().WithMessage("*check digit*");
    }

    [Fact]
    public void UpcA_computes_check_digit_when_11_digits_supplied()
    {
        // 03600029145 → check digit 2 (Coca-Cola classic example).
        var g = Barcode1DEncoder.Encode(BarcodeSymbology.UpcA, "03600029145");
        g.Checksum.Should().Be("2");
    }

    [Fact]
    public void Isbn10_is_repacked_as_Ean13_with_978_prefix()
    {
        // ISBN-10 "0-306-40615-2" → EAN-13 "9780306406157", check = 7.
        var g = Barcode1DEncoder.Encode(BarcodeSymbology.Isbn, "0306406152");
        g.Checksum.Should().Be("7");
        // ISBN encoded as EAN-13 → same number of bars as any EAN-13 (30 bars in a typical
        // black-bar count, but the exact number depends on consecutive-1 runs).
        g.Bars.Should().NotBeEmpty();
    }

    [Fact]
    public void Code128_rejects_non_ascii_characters()
    {
        var act = () => Barcode1DEncoder.Encode(BarcodeSymbology.Code128, "Olá");
        act.Should().Throw<ArgumentException>().WithMessage("*ASCII 32..127*");
    }

    [Fact]
    public void Itf_pads_odd_length_with_leading_zero()
    {
        // Odd-length input is auto-prefixed with 0 before encoding (standard behavior).
        var oddGeom = Barcode1DEncoder.Encode(BarcodeSymbology.Itf, "12345");
        var evenGeom = Barcode1DEncoder.Encode(BarcodeSymbology.Itf, "012345");
        oddGeom.Bars.Count.Should().Be(evenGeom.Bars.Count,
            "ITF pads odd-length input with a leading zero, so geometry must match");
    }
}
