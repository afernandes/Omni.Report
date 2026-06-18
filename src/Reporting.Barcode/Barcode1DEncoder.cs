using System.Globalization;
using System.Text;

namespace Reporting.Barcode;

/// <summary>
/// 1D barcode encoder — produces <see cref="BarcodeGeometry"/> for the symbologies in
/// <see cref="BarcodeSymbology"/>. No external dependencies; pure managed math.
/// </summary>
/// <remarks>
/// <para>Module-unit output: every rectangle is in "module widths" (the smallest bar
/// unit). The renderer multiplies by a physical module size at draw time — that's how
/// the same encoding renders at any DPI without re-encoding.</para>
/// <para>Algorithms ported from
/// <see href="https://github.com/radzenhq/radzen-blazor">Radzen.Blazor</see>
/// (<c>RadzenBarcodeEncoder</c>, MIT). See <c>ATTRIBUTION.md</c> for details.</para>
/// </remarks>
public static class Barcode1DEncoder
{
    /// <summary>Encodes <paramref name="value"/> using <paramref name="symbology"/>.
    /// Returns the vector geometry (bars + viewBox + checksum).</summary>
    /// <param name="symbology">Which symbology to use.</param>
    /// <param name="value">The data to encode. Validity depends on the symbology
    /// (Code39 is uppercase + a few symbols, EAN/UPC are digits, etc.).</param>
    /// <param name="barHeight">Height of every bar in module units. Use 50 for a
    /// reasonable 1D symbol; thermal printers usually want 25–40.</param>
    /// <param name="quietZoneModules">Mandatory blank margin on either side. 10 modules
    /// is the minimum recommended by most standards; EAN/UPC require 9 (left) + 7 (right)
    /// — passing 10 is always safe.</param>
    public static BarcodeGeometry Encode(
        BarcodeSymbology symbology,
        string value,
        double barHeight = 50,
        int quietZoneModules = 10)
    {
        ArgumentNullException.ThrowIfNull(value);
        return symbology switch
        {
            BarcodeSymbology.Code128 => FromWidths(EncodeCode128B(value, out _), barHeight, quietZoneModules, string.Empty),
            BarcodeSymbology.Code39  => FromWidths(EncodeCode39(value), barHeight, quietZoneModules, string.Empty),
            BarcodeSymbology.Codabar => FromWidths(EncodeCodabar(value), barHeight, quietZoneModules, string.Empty),
            BarcodeSymbology.Itf     => FromWidths(EncodeItf(value), barHeight, quietZoneModules, string.Empty),
            BarcodeSymbology.Ean13   => FromBits(EncodeEan13(value, out var c13), barHeight, quietZoneModules, c13),
            BarcodeSymbology.Ean8    => FromBits(EncodeEan8(value, out var c8), barHeight, quietZoneModules, c8),
            BarcodeSymbology.UpcA    => FromBits(EncodeUpcA(value, out var cUp), barHeight, quietZoneModules, cUp),
            BarcodeSymbology.Isbn    => FromBits(EncodeIsbnAsEan13(value, out var cIs), barHeight, quietZoneModules, cIs),
            BarcodeSymbology.Issn    => FromBits(EncodeIssnAsEan13(value, out var cSs), barHeight, quietZoneModules, cSs),
            _ => throw new ArgumentOutOfRangeException(nameof(symbology), symbology, "Unsupported symbology."),
        };
    }

    // ── Code 128 (subset B: ASCII 32..127) ────────────────────────────────────

    // Each entry is module widths "bar/space/bar/space/bar/space" (6 digits, 7 for stop).
    private static readonly string[] Code128Patterns = new[]
    {
        "212222","222122","222221","121223","121322","131222","122213","122312","132212","221213",
        "221312","231212","112232","122132","122231","113222","123122","123221","223211","221132",
        "221231","213212","223112","312131","311222","321122","321221","312212","322112","322211",
        "212123","212321","232121","111323","131123","131321","112313","132113","132311","211313",
        "231113","231311","112133","112331","132131","113123","113321","133121","313121","211331",
        "231131","213113","213311","213131","311123","311321","331121","312113","312311","332111",
        "314111","221411","431111","111224","111422","121124","121421","141122","141221","112214",
        "112412","122114","122411","142112","142211","241211","221114","413111","241112","134111",
        "111242","121142","121241","114212","124112","124211","411212","421112","421211","212141",
        "214121","412121","111143","111341","131141","114113","114311","411113","411311","113141",
        "114131","311141","411131","211412","211214","211232","2331112"
    };

    private static IReadOnlyList<int> EncodeCode128B(string value, out int checksum)
    {
        var codes = new List<int>(value.Length + 3);
        const int startB = 104, stop = 106;
        codes.Add(startB);
        for (int i = 0; i < value.Length; i++)
        {
            int ascii = value[i];
            if (ascii is < 32 or > 127)
                throw new ArgumentException($"Code128B supports ASCII 32..127. Invalid character: U+{ascii:X4}.");
            codes.Add(ascii - 32);
        }
        int chk = codes[0];
        for (int i = 1; i < codes.Count; i++) chk += codes[i] * i;
        chk %= 103;
        checksum = chk;
        codes.Add(chk);
        codes.Add(stop);

        var modules = new List<int>(codes.Count * 6);
        foreach (var code in codes)
        {
            var p = Code128Patterns[code];
            for (int i = 0; i < p.Length; i++) modules.Add(p[i] - '0');
        }
        if (modules.Count > 0) modules[^1] += 2; // termination bar
        return modules;
    }

    // ── Code 39 — uppercase A-Z + 0-9 + a few symbols ─────────────────────────

    private static readonly Dictionary<char, string> Code39Map = new()
    {
        ['0']="nnnwwnwnn",['1']="wnnwnnnnw",['2']="nnwwnnnnw",['3']="wnwwnnnnn",['4']="nnnwwnnnw",
        ['5']="wnnwwnnnn",['6']="nnwwwnnnn",['7']="nnnwnnwnw",['8']="wnnwnnwnn",['9']="nnwwnnwnn",
        ['A']="wnnnnwnnw",['B']="nnwnnwnnw",['C']="wnwnnwnnn",['D']="nnnnwwnnw",['E']="wnnnwwnnn",
        ['F']="nnwnwwnnn",['G']="nnnnnwwnw",['H']="wnnnnwwnn",['I']="nnwnnwwnn",['J']="nnnnwwwnn",
        ['K']="wnnnnnnww",['L']="nnwnnnnww",['M']="wnwnnnnwn",['N']="nnnnwnnww",['O']="wnnnwnnwn",
        ['P']="nnwnwnnwn",['Q']="nnnnnnwww",['R']="wnnnnnwwn",['S']="nnwnnnwwn",['T']="nnnnwnwwn",
        ['U']="wwnnnnnnw",['V']="nwwnnnnnw",['W']="wwwnnnnnn",['X']="nwnnwnnnw",['Y']="wwnnwnnnn",
        ['Z']="nwwnwnnnn",['-']="nwnnnnwnw",['.']="wwnnnnwnn",[' ']="nwwnnnwnn",['$']="nwnwnwnnn",
        ['/']="nwnwnnnwn",['+']="nwnnnwnwn",['%']="nnnwnwnwn",['*']="nwnnwnwnn", // *=start/stop
    };

    private static IReadOnlyList<int> EncodeCode39(string value)
    {
        var text = value.ToUpperInvariant();
        foreach (var ch in text)
            if (!Code39Map.ContainsKey(ch))
                throw new ArgumentException($"Code39 does not support character '{ch}'.");
        var full = "*" + text + "*";
        var modules = new List<int>(full.Length * 10);
        for (int idx = 0; idx < full.Length; idx++)
        {
            var pat = Code39Map[full[idx]];
            for (int i = 0; i < pat.Length; i++) modules.Add(pat[i] == 'w' ? 2 : 1);
            if (idx != full.Length - 1) modules.Add(1); // inter-character narrow space
        }
        return modules;
    }

    // ── Codabar (start/stop = A/B/C/D — added when absent) ────────────────────

    private static IReadOnlyList<int> EncodeCodabar(string value)
    {
        var raw = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (raw.Length == 0) throw new ArgumentException("Codabar requires a non-empty value.");
        bool StartStop(string s) => s.Length >= 2
            && (s[0] is 'A' or 'B' or 'C' or 'D')
            && (s[^1] is 'A' or 'B' or 'C' or 'D');
        var text = StartStop(raw) ? raw : $"A{raw}B";

        static (string sp, string br) Map(char ch) => ch switch
        {
            '0' => ("001", "0001"), '1' => ("001", "0010"), '2' => ("010", "0001"),
            '3' => ("100", "1000"), '4' => ("001", "0100"), '5' => ("001", "1000"),
            '6' => ("100", "0001"), '7' => ("100", "0010"), '8' => ("100", "0100"),
            '9' => ("010", "1000"), '-' => ("010", "0010"), '$' => ("010", "0100"),
            '.' => ("000", "0001"), '/' => ("000", "0010"), ':' => ("000", "0100"),
            '+' => ("000", "1000"), 'A' => ("011", "0100"), 'B' => ("110", "0001"),
            'C' => ("011", "0001"), 'D' => ("011", "0010"),
            _ => throw new ArgumentException($"Codabar does not support character '{ch}'."),
        };
        const int narrow = 1, wide = 3;
        var widths = new List<int>(text.Length * 8);
        for (int idx = 0; idx < text.Length; idx++)
        {
            var (sp, br) = Map(text[idx]);
            int W(int pos) => br[pos] == '1' ? wide : narrow;
            int S(int pos) => sp[pos] == '0' ? wide : narrow;
            widths.Add(W(0)); widths.Add(S(0));
            widths.Add(W(1)); widths.Add(S(1));
            widths.Add(W(2)); widths.Add(S(2));
            widths.Add(W(3));
            if (idx != text.Length - 1) widths.Add(narrow);
        }
        return widths;
    }

    // ── ITF (Interleaved 2 of 5) — digits only, even-length ───────────────────

    private static IReadOnlyList<int> EncodeItf(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length == 0) throw new ArgumentException("ITF requires numeric input.");
        if (digits.Length % 2 != 0) digits = "0" + digits;
        const int narrow = 1, wide = 3;
        static string Pat(char d) => d switch
        {
            '0' => "nnwwn", '1' => "wnnnw", '2' => "nwnnw", '3' => "wwnnn", '4' => "nnwnw",
            '5' => "wnwnn", '6' => "nwwnn", '7' => "nnnww", '8' => "wnnwn", '9' => "nwnwn",
            _ => throw new ArgumentException("ITF requires numeric input."),
        };
        var widths = new List<int>(digits.Length * 10 + 16);
        // Start: 1010 (narrow bar, narrow space, narrow bar, narrow space)
        widths.Add(narrow); widths.Add(narrow); widths.Add(narrow); widths.Add(narrow);
        for (int i = 0; i < digits.Length; i += 2)
        {
            var a = Pat(digits[i]); var b = Pat(digits[i + 1]);
            for (int j = 0; j < 5; j++)
            {
                widths.Add(a[j] == 'w' ? wide : narrow); // bar
                widths.Add(b[j] == 'w' ? wide : narrow); // space
            }
        }
        // Stop: 1101
        widths.Add(wide); widths.Add(narrow); widths.Add(narrow);
        return widths;
    }

    // ── EAN-13 / EAN-8 / UPC-A / ISBN / ISSN — share bit-pattern infrastructure ─

    private static readonly string[] EanL = new[]
    { "0001101","0011001","0010011","0111101","0100011","0110001","0101111","0111011","0110111","0001011" };
    private static readonly string[] EanG = new[]
    { "0100111","0110011","0011011","0100001","0011101","0111001","0000101","0010001","0001001","0010111" };
    private static readonly string[] EanR = new[]
    { "1110010","1100110","1101100","1000010","1011100","1001110","1010000","1000100","1001000","1110100" };
    private static readonly string[] Ean13Parity = new[]
    { "LLLLLL","LLGLGG","LLGGLG","LLGGGL","LGLLGG","LGGLLG","LGGGLL","LGLGLG","LGLGGL","LGGLGL" };

    private static int ComputeEanCheckDigit(string digitsWithoutCheck)
    {
        int sum = 0;
        bool weight3 = true;
        for (int i = digitsWithoutCheck.Length - 1; i >= 0; i--)
        {
            int d = digitsWithoutCheck[i] - '0';
            sum += weight3 ? d * 3 : d;
            weight3 = !weight3;
        }
        return (10 - (sum % 10)) % 10;
    }

    private static string EncodeEan13(string value, out string checksumText)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length is not (12 or 13))
            throw new ArgumentException("EAN-13 requires 12 or 13 digits.");
        if (digits.Length == 12)
            digits += ComputeEanCheckDigit(digits).ToString(CultureInfo.InvariantCulture);
        else if (digits[12] - '0' != ComputeEanCheckDigit(digits[..12]))
            throw new ArgumentException("Invalid EAN-13 check digit.");
        checksumText = digits[^1].ToString();

        int first = digits[0] - '0';
        var parity = Ean13Parity[first];
        var sb = new StringBuilder(95);
        sb.Append("101");
        for (int i = 1; i <= 6; i++)
        {
            int d = digits[i] - '0';
            sb.Append(parity[i - 1] == 'G' ? EanG[d] : EanL[d]);
        }
        sb.Append("01010");
        for (int i = 7; i <= 12; i++) sb.Append(EanR[digits[i] - '0']);
        sb.Append("101");
        return sb.ToString();
    }

    private static string EncodeUpcA(string value, out string checksumText)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length is not (11 or 12))
            throw new ArgumentException("UPC-A requires 11 or 12 digits.");
        if (digits.Length == 11)
            digits += ComputeEanCheckDigit(digits).ToString(CultureInfo.InvariantCulture);
        else if (digits[11] - '0' != ComputeEanCheckDigit(digits[..11]))
            throw new ArgumentException("Invalid UPC-A check digit.");
        checksumText = digits[^1].ToString();

        var sb = new StringBuilder(95);
        sb.Append("101");
        for (int i = 0; i < 6; i++) sb.Append(EanL[digits[i] - '0']);
        sb.Append("01010");
        for (int i = 6; i < 12; i++) sb.Append(EanR[digits[i] - '0']);
        sb.Append("101");
        return sb.ToString();
    }

    private static string EncodeEan8(string value, out string checksumText)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length is not (7 or 8))
            throw new ArgumentException("EAN-8 requires 7 or 8 digits.");
        if (digits.Length == 7)
            digits += ComputeEanCheckDigit(digits).ToString(CultureInfo.InvariantCulture);
        else if (digits[7] - '0' != ComputeEanCheckDigit(digits[..7]))
            throw new ArgumentException("Invalid EAN-8 check digit.");
        checksumText = digits[^1].ToString();

        var sb = new StringBuilder(67);
        sb.Append("101");
        for (int i = 0; i < 4; i++) sb.Append(EanL[digits[i] - '0']);
        sb.Append("01010");
        for (int i = 4; i < 8; i++) sb.Append(EanR[digits[i] - '0']);
        sb.Append("101");
        return sb.ToString();
    }

    private static string EncodeIsbnAsEan13(string value, out string checksumText)
    {
        var raw = new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (raw.Length == 10)
        {
            var core = raw[..9];
            if (!core.All(char.IsDigit)) throw new ArgumentException("Invalid ISBN-10.");
            return EncodeEan13("978" + core, out checksumText);
        }
        if (raw.Length == 13)
        {
            if (!raw.All(char.IsDigit)) throw new ArgumentException("Invalid ISBN-13.");
            return EncodeEan13(raw, out checksumText);
        }
        throw new ArgumentException("ISBN requires 10 or 13 characters.");
    }

    private static string EncodeIssnAsEan13(string value, out string checksumText)
    {
        var raw = new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (raw.Length != 8) throw new ArgumentException("ISSN requires 8 characters.");
        var core = raw[..7];
        if (!core.All(char.IsDigit)) throw new ArgumentException("Invalid ISSN.");
        return EncodeEan13("977" + core + "00", out checksumText);
    }

    // ── Width / bit pattern → geometry ───────────────────────────────────────

    private static BarcodeGeometry FromWidths(IReadOnlyList<int> widths, double barHeight, int quietZone, string checksum)
    {
        var rects = new List<BarcodeRect>(widths.Count);
        double x = Math.Max(0, quietZone);
        bool isBar = true;
        for (int i = 0; i < widths.Count; i++)
        {
            int w = widths[i];
            if (isBar && w > 0) rects.Add(new BarcodeRect(x, 0, w, barHeight));
            x += w;
            isBar = !isBar;
        }
        var vbW = x + Math.Max(0, quietZone);
        return new BarcodeGeometry(rects, vbW <= 0 ? 1 : vbW, barHeight, checksum);
    }

    private static BarcodeGeometry FromBits(string bits, double barHeight, int quietZone, string checksum)
    {
        var rects = new List<BarcodeRect>();
        var quiet = Math.Max(0, quietZone);
        for (int i = 0; i < bits.Length;)
        {
            if (bits[i] != '1') { i++; continue; }
            int j = i + 1;
            while (j < bits.Length && bits[j] == '1') j++;
            rects.Add(new BarcodeRect(quiet + i, 0, j - i, barHeight));
            i = j;
        }
        var vbW = quiet + bits.Length + quiet;
        return new BarcodeGeometry(rects, vbW <= 0 ? 1 : vbW, barHeight, checksum);
    }
}
