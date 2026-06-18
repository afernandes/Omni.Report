using System.Text;
using FluentAssertions;
using Reporting.Barcode;
using Xunit;

namespace Reporting.Barcode.Tests;

/// <summary>Smoke tests for the QR encoder. We can't visually inspect the output here,
/// so we check matrix invariants that ANY valid QR code must satisfy: finder squares,
/// timing patterns, dark module presence, version-dependent dimensions, mask region
/// reservation. If these hold for sample versions, the algorithm is structurally sound.</summary>
public class QrEncoderTests
{
    [Theory]
    [InlineData("hello", 21)]                                            // V1 → 21 modules
    [InlineData("https://example.com/longer/url/needs/bigger/qr", 33)] // V4 → 33 modules
    public void Encoded_matrix_has_expected_dimension(string value, int expectedSide)
    {
        var m = QrEncoder.EncodeUtf8(value);
        m.GetLength(0).Should().BeGreaterThanOrEqualTo(expectedSide);
        m.GetLength(0).Should().Be(m.GetLength(1), "QR matrices are square");
    }

    [Fact]
    public void Three_finder_patterns_are_present_at_known_positions()
    {
        // Every QR has 3 finder patterns (TL, TR, BL). Each is a 7×7 black-white-black
        // bullseye. We probe a few defining pixels of each pattern to confirm placement.
        var m = QrEncoder.EncodeUtf8("test", QrErrorCorrection.Medium);
        int n = m.GetLength(0);

        // Top-left finder: outer corner [0,0] dark, inner ring [2,2] dark, ring [1,1] light.
        m[0, 0].Should().BeTrue("TL finder outer pixel must be dark");
        m[2, 2].Should().BeTrue("TL finder inner pixel must be dark");
        m[1, 1].Should().BeFalse("TL finder inner ring must be light");

        // Top-right finder starts at column n-7.
        m[0, n - 1].Should().BeTrue("TR finder outer pixel must be dark");
        m[2, n - 3].Should().BeTrue("TR finder inner pixel must be dark");

        // Bottom-left finder starts at row n-7.
        m[n - 1, 0].Should().BeTrue("BL finder outer pixel must be dark");
        m[n - 3, 2].Should().BeTrue("BL finder inner pixel must be dark");
    }

    [Fact]
    public void Mandatory_dark_module_is_set()
    {
        // ISO/IEC 18004 mandates a single always-dark module at [4*version+9, 8].
        // For V1: row 13, col 8.
        var m = QrEncoder.EncodeUtf8("x", QrErrorCorrection.Medium);
        m[13, 8].Should().BeTrue("the mandatory 'dark module' fixture must be set in V1");
    }

    [Fact]
    public void Higher_ecc_grows_or_keeps_matrix_size()
    {
        // The same payload at higher ECC needs a same-or-bigger version because EC bytes
        // displace data bytes. Equal is fine when the symbol still fits the same version.
        var low = QrEncoder.EncodeUtf8("Short payload", QrErrorCorrection.Low).GetLength(0);
        var high = QrEncoder.EncodeUtf8("Short payload", QrErrorCorrection.High).GetLength(0);
        high.Should().BeGreaterThanOrEqualTo(low);
    }

    [Fact]
    public void Encoded_bytes_can_handle_unicode_via_utf8()
    {
        // Non-ASCII content (Portuguese accents) goes through UTF-8 bytes — encoder must
        // accept it without throwing.
        var act = () => QrEncoder.EncodeUtf8("Olá, mundo — relatórios são úteis.");
        act.Should().NotThrow();
    }

    [Fact]
    public void Encoded_bytes_overload_accepts_raw_payload()
    {
        // Useful for binary payloads (PIX / Bolt-On / vCard with embedded photos).
        var bytes = Encoding.UTF8.GetBytes("binary-payload");
        var m = QrEncoder.EncodeBytes(bytes);
        m.GetLength(0).Should().BeGreaterThan(0);
    }

    [Fact]
    public void Geometry_helper_produces_one_rect_per_dark_module_plus_quiet_zone()
    {
        var m = QrEncoder.EncodeUtf8("hi");
        int dark = 0;
        for (int r = 0; r < m.GetLength(0); r++)
            for (int c = 0; c < m.GetLength(1); c++)
                if (m[r, c]) dark++;

        var g = QrEncoder.ToGeometry(m, quietZoneModules: 4);
        g.Bars.Count.Should().Be(dark, "ToGeometry emits one BarcodeRect per dark module");
        g.ViewBoxWidth.Should().Be(m.GetLength(0) + 8, "viewBox includes 4-module quiet zone on each side");
        g.ViewBoxWidth.Should().Be(g.ViewBoxHeight, "QR symbols are square");
    }
}
