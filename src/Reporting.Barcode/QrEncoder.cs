using System.Text;

namespace Reporting.Barcode;

/// <summary>QR error-correction level — higher = bigger symbol, more damage tolerance.</summary>
public enum QrErrorCorrection
{
    /// <summary>~7% recovery. Smallest symbol; OK for clean printing.</summary>
    Low,
    /// <summary>~15% recovery. Default — balance between size and resilience.</summary>
    Medium,
    /// <summary>~25% recovery. Good for printed receipts that get crumpled.</summary>
    Quartile,
    /// <summary>~30% recovery. Use when embedding a logo or for severely
    /// damaged printing surfaces.</summary>
    High,
}

/// <summary>
/// QR Code (model 2) encoder — versions 1 through 40, ECC levels L/M/Q/H, byte mode.
/// Outputs a <c>bool[,]</c> module matrix (true = dark, false = light) plus a helper to
/// turn it into <see cref="BarcodeGeometry"/>.
/// </summary>
/// <remarks>
/// <para>No external dependencies; pure managed math. Implements ISO/IEC 18004 byte mode
/// only — sufficient for UTF-8 strings and arbitrary bytes (URLs, vCards, PIX payloads,
/// NF-e access keys, etc.).</para>
/// <para>Algorithm ported from
/// <see href="https://github.com/radzenhq/radzen-blazor">Radzen.Blazor</see>
/// (<c>RadzenQREncoder</c>, MIT). See <c>ATTRIBUTION.md</c>.</para>
/// </remarks>
public static class QrEncoder
{
    /// <summary>Encodes a UTF-8 string into a QR module matrix. Picks the smallest
    /// version that fits the payload at the chosen ECC level.</summary>
    public static bool[,] EncodeUtf8(string value, QrErrorCorrection ecc = QrErrorCorrection.Medium,
        int minVersion = 1, int maxVersion = 40)
    {
        var data = Encoding.UTF8.GetBytes(value ?? string.Empty);
        return EncodeBytes(data, ecc, minVersion, maxVersion);
    }

    /// <summary>Encodes raw bytes into a QR module matrix.</summary>
    public static bool[,] EncodeBytes(byte[] data, QrErrorCorrection ecc = QrErrorCorrection.Medium,
        int minVersion = 1, int maxVersion = 40)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (minVersion is < 1 or > 40 || maxVersion is < 1 or > 40 || minVersion > maxVersion)
            throw new ArgumentOutOfRangeException(nameof(minVersion), "Version range must be within 1..40.");

        for (int ver = minVersion; ver <= maxVersion; ver++)
        {
            var (dataCw, ecPerBlock, g1, g1dcw, g2, g2dcw) = EcParams(ver, ecc);
            int capacityBits = dataCw * 8;

            int charCountBits = ver <= 9 ? 8 : 16;
            var bb = new BitBuffer();
            bb.AppendBits(0b0100, 4);                     // BYTE mode marker
            bb.AppendBits(data.Length, charCountBits);    // length
            foreach (var b in data) bb.AppendBits(b, 8);  // payload

            int baseBits = bb.Length;
            if (baseBits > capacityBits) continue;

            int needed = baseBits + Math.Min(4, capacityBits - baseBits); // terminator (up to 4 bits)
            needed += (8 - (needed % 8)) % 8;                              // pad to byte
            if (needed > capacityBits) continue;

            var dataCwBytes = BuildDataCodewords(bb, dataCw);
            var blocks = BuildBlocks(dataCwBytes, ecPerBlock, g1, g1dcw, g2, g2dcw);
            var final = Interleave(blocks);

            var (m, reserved) = BuildBaseMatrix(ver);
            PlaceData(m, reserved, final);

            int bestMask = ChooseBestMask(m, reserved);
            ApplyMask(m, reserved, bestMask);
            WriteFormatInfo(m, reserved, ecc, bestMask);
            if (ver >= 7) WriteVersionInfo(m, reserved, ver);

            return m;
        }
        throw new ArgumentException($"Data too long for versions {minVersion}..{maxVersion} at ECC={ecc}.");
    }

    /// <summary>Renders a module matrix as <see cref="BarcodeGeometry"/> with a 4-module
    /// quiet zone (the QR standard's minimum). One <see cref="BarcodeRect"/> per dark
    /// module — the renderer can compose them into a single SKPath for vector output.</summary>
    public static BarcodeGeometry ToGeometry(bool[,] modules, int quietZoneModules = 4)
    {
        ArgumentNullException.ThrowIfNull(modules);
        int n = modules.GetLength(0);
        var bars = new List<BarcodeRect>(n * n / 2);
        var quiet = Math.Max(0, quietZoneModules);
        for (int r = 0; r < n; r++)
            for (int c = 0; c < n; c++)
                if (modules[r, c])
                    bars.Add(new BarcodeRect(quiet + c, quiet + r, 1, 1));
        var dim = n + 2 * quiet;
        return new BarcodeGeometry(bars, dim, dim, string.Empty);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Below: full QR encoder implementation. See ATTRIBUTION.md for source. The
    // algorithm matches ISO/IEC 18004 model-2 (the standard square QR you've seen
    // everywhere). Heavy on table lookups; light on logic.
    // ──────────────────────────────────────────────────────────────────────────

    private static (bool[,] m, bool[,] res) BuildBaseMatrix(int ver)
    {
        int n = 21 + 4 * (ver - 1);
        var m = new bool[n, n];
        var res = new bool[n, n];

        DrawFinder(m, res, 0, 0);
        DrawFinder(m, res, n - 7, 0);
        DrawFinder(m, res, 0, n - 7);

        for (int i = 8; i < n - 8; i++)
        {
            m[6, i] = (i % 2) == 0; res[6, i] = true;
            m[i, 6] = (i % 2) == 0; res[i, 6] = true;
        }

        var ap = AlignmentPos[ver];
        foreach (int y in ap)
            foreach (int x in ap)
            {
                bool corner = (x < 9 && y < 9) || (x > n - 9 && y < 9) || (x < 9 && y > n - 9);
                if (!corner) DrawAlignment(m, res, x, y);
            }

        m[4 * ver + 9, 8] = true; res[4 * ver + 9, 8] = true;

        ReserveFormat(res);
        if (ver >= 7) ReserveVersion(res);
        return (m, res);
    }

    private static void DrawFinder(bool[,] m, bool[,] res, int x, int y)
    {
        for (int r = -1; r <= 7; r++)
            for (int c = -1; c <= 7; c++)
            {
                int rr = y + r, cc = x + c;
                if (rr < 0 || cc < 0 || rr >= m.GetLength(0) || cc >= m.GetLength(1)) continue;
                bool in7 = r >= 0 && r < 7 && c >= 0 && c < 7;
                if (in7)
                {
                    bool on = r == 0 || r == 6 || c == 0 || c == 6 || (r >= 2 && r <= 4 && c >= 2 && c <= 4);
                    m[rr, cc] = on; res[rr, cc] = true;
                }
                else { res[rr, cc] = true; }
            }
    }

    private static void DrawAlignment(bool[,] m, bool[,] res, int cx, int cy)
    {
        for (int r = -2; r <= 2; r++)
            for (int c = -2; c <= 2; c++)
            {
                int rr = cy + r, cc = cx + c;
                m[rr, cc] = Math.Max(Math.Abs(r), Math.Abs(c)) != 1;
                res[rr, cc] = true;
            }
    }

    private static void ReserveFormat(bool[,] res)
    {
        int n = res.GetLength(0);
        for (int c = 0; c <= 5; c++) res[8, c] = true;
        res[8, 7] = true; res[8, 8] = true;
        for (int r = 0; r <= 5; r++) res[r, 8] = true;
        res[7, 8] = true;
        for (int i = 0; i < 7; i++) res[n - 1 - i, 8] = true;
        for (int i = 0; i < 8; i++) res[8, n - 8 + i] = true;
    }

    private static void ReserveVersion(bool[,] res)
    {
        int n = res.GetLength(0);
        for (int r = n - 11; r <= n - 9; r++)
            for (int c = 0; c <= 5; c++) res[r, c] = true;
        for (int r = 0; r <= 5; r++)
            for (int c = n - 11; c <= n - 9; c++) res[r, c] = true;
    }

    private static void WriteVersionInfo(bool[,] m, bool[,] res, int ver)
    {
        int bits = BchEncode(ver, 0x1F25, 18, 6);
        int n = m.GetLength(0);
        for (int col = 0; col < 6; col++)
            for (int row = 0; row < 3; row++)
            {
                int bitIndex = row + col * 3;
                bool bit = ((bits >> bitIndex) & 1) != 0;
                Set(m, res, n - 11 + row, col, bit);
            }
        for (int row = 0; row < 6; row++)
            for (int col = 0; col < 3; col++)
            {
                int bitIndex = row * 3 + col;
                bool bit = ((bits >> bitIndex) & 1) != 0;
                Set(m, res, row, n - 11 + col, bit);
            }
    }

    private static byte[] BuildDataCodewords(BitBuffer bb, int dataCw)
    {
        int totalBits = dataCw * 8;
        int remaining = totalBits - bb.Length;
        bb.AppendBits(0, Math.Min(4, remaining));
        while (bb.Length % 8 != 0) bb.AppendBits(0, 1);
        var bytes = new List<byte>(dataCw);
        for (int i = 0; i < bb.Length; i += 8)
        {
            if (bytes.Count == dataCw) break;
            int b = 0;
            for (int j = 0; j < 8; j++) b = (b << 1) | bb[i + j];
            bytes.Add((byte)b);
        }
        byte[] pads = { 0xEC, 0x11 }; int p = 0;
        while (bytes.Count < dataCw) bytes.Add(pads[p++ & 1]);
        return bytes.ToArray();
    }

    private sealed record Block(byte[] Data, byte[] Ec);

    private static List<Block> BuildBlocks(byte[] dataCwBytes, int ecPerBlock,
        int g1, int g1dcw, int g2, int g2dcw)
    {
        var blocks = new List<Block>(g1 + g2);
        int idx = 0;
        for (int i = 0; i < g1; i++)
        {
            var data = new byte[g1dcw];
            Array.Copy(dataCwBytes, idx, data, 0, g1dcw);
            idx += g1dcw;
            blocks.Add(new Block(data, ReedSolomon(data, ecPerBlock)));
        }
        for (int i = 0; i < g2; i++)
        {
            var data = new byte[g2dcw];
            Array.Copy(dataCwBytes, idx, data, 0, g2dcw);
            idx += g2dcw;
            blocks.Add(new Block(data, ReedSolomon(data, ecPerBlock)));
        }
        return blocks;
    }

    private static byte[] Interleave(List<Block> blocks)
    {
        int total = 0, maxData = 0, maxEc = 0;
        foreach (var b in blocks)
        {
            total += b.Data.Length + b.Ec.Length;
            if (b.Data.Length > maxData) maxData = b.Data.Length;
            if (b.Ec.Length > maxEc) maxEc = b.Ec.Length;
        }
        var r = new byte[total];
        int k = 0;
        for (int i = 0; i < maxData; i++)
            foreach (var b in blocks) if (i < b.Data.Length) r[k++] = b.Data[i];
        for (int i = 0; i < maxEc; i++)
            foreach (var b in blocks) if (i < b.Ec.Length) r[k++] = b.Ec[i];
        return r;
    }

    private static byte[] ReedSolomon(byte[] data, int ecCount)
    {
        var gen = RsGenerator(ecCount);
        var msg = new byte[data.Length + ecCount];
        Array.Copy(data, msg, data.Length);
        for (int i = 0; i < data.Length; i++)
        {
            int factor = msg[i];
            if (factor == 0) continue;
            for (int j = 0; j < gen.Length; j++)
                msg[i + j] ^= GfMul((byte)factor, gen[j]);
        }
        var ec = new byte[ecCount];
        Array.Copy(msg, data.Length, ec, 0, ecCount);
        return ec;
    }

    private static byte[] RsGenerator(int ecCount)
    {
        var gen = new List<byte> { 1 };
        for (int i = 0; i < ecCount; i++)
            gen = PolyMul(gen, new List<byte> { 1, GfPow(2, i) });
        return gen.ToArray();
    }

    private static List<byte> PolyMul(List<byte> a, List<byte> b)
    {
        var r = new byte[a.Count + b.Count - 1];
        for (int i = 0; i < a.Count; i++)
            for (int j = 0; j < b.Count; j++)
                r[i + j] ^= GfMul(a[i], b[j]);
        return new List<byte>(r);
    }

    private const int GfPoly = 0x11D;
    private static byte GfMul(byte x, byte y)
    {
        int r = 0;
        while (y > 0)
        {
            if ((y & 1) != 0) r ^= x;
            y >>= 1;
            x = (byte)((x << 1) ^ ((x & 0x80) != 0 ? GfPoly : 0));
        }
        return (byte)(r & 0xFF);
    }

    private static byte GfPow(byte a, int e)
    {
        byte r = 1;
        for (int i = 0; i < e; i++) r = GfMul(r, a);
        return r;
    }

    private static void PlaceData(bool[,] m, bool[,] res, byte[] codewords)
    {
        int n = m.GetLength(0);
        int totalBits = codewords.Length * 8;
        int bitIndex = 0;
        int dir = -1;
        for (int col = n - 1; col > 0; col -= 2)
        {
            if (col == 6) col--;
            int rowStart = dir < 0 ? n - 1 : 0;
            for (int i = 0; i < n; i++)
            {
                int r = rowStart + i * dir;
                for (int c = 0; c < 2; c++)
                {
                    int cc = col - c;
                    if (res[r, cc]) continue;
                    if (bitIndex < totalBits)
                    {
                        m[r, cc] = ((codewords[bitIndex >> 3] >> (7 - (bitIndex & 7))) & 1) != 0;
                        bitIndex++;
                    }
                }
            }
            dir *= -1;
        }
    }

    private static int ChooseBestMask(bool[,] m, bool[,] reserved)
    {
        int bestMask = 0, bestPenalty = int.MaxValue;
        for (int mask = 0; mask < 8; mask++)
        {
            var masked = CloneAndMask(m, reserved, mask);
            int pen = Penalty(masked);
            if (pen < bestPenalty) { bestPenalty = pen; bestMask = mask; }
        }
        return bestMask;
    }

    private static bool[,] CloneAndMask(bool[,] src, bool[,] res, int mask)
    {
        int n = src.GetLength(0);
        var dst = new bool[n, n];
        for (int r = 0; r < n; r++)
            for (int c = 0; c < n; c++)
            {
                bool v = src[r, c];
                if (!res[r, c] && Mask(mask, r, c)) v = !v;
                dst[r, c] = v;
            }
        return dst;
    }

    private static void ApplyMask(bool[,] m, bool[,] res, int mask)
    {
        int n = m.GetLength(0);
        for (int r = 0; r < n; r++)
            for (int c = 0; c < n; c++)
                if (!res[r, c] && Mask(mask, r, c)) m[r, c] = !m[r, c];
    }

    private static bool Mask(int mask, int r, int c) => mask switch
    {
        0 => ((r + c) & 1) == 0,
        1 => (r & 1) == 0,
        2 => (c % 3) == 0,
        3 => ((r + c) % 3) == 0,
        4 => (((r / 2) + (c / 3)) & 1) == 0,
        5 => ((r * c) % 2 + (r * c) % 3) == 0,
        6 => ((((r * c) % 2) + ((r * c) % 3)) & 1) == 0,
        7 => ((((r + c) % 2) + ((r * c) % 3)) & 1) == 0,
        _ => false,
    };

    private static int Penalty(bool[,] m)
    {
        int n = m.GetLength(0);
        int total = 0;
        // N1: 5+ runs
        for (int r = 0; r < n; r++)
        {
            int run = 1;
            for (int c = 1; c < n; c++)
            {
                if (m[r, c] == m[r, c - 1]) run++;
                else { if (run >= 5) total += 3 + (run - 5); run = 1; }
            }
            if (run >= 5) total += 3 + (run - 5);
        }
        for (int c = 0; c < n; c++)
        {
            int run = 1;
            for (int r = 1; r < n; r++)
            {
                if (m[r, c] == m[r - 1, c]) run++;
                else { if (run >= 5) total += 3 + (run - 5); run = 1; }
            }
            if (run >= 5) total += 3 + (run - 5);
        }
        // N2: 2x2 blocks
        for (int r = 0; r < n - 1; r++)
            for (int c = 0; c < n - 1; c++)
                if (m[r, c] == m[r, c + 1] && m[r, c] == m[r + 1, c] && m[r, c] == m[r + 1, c + 1])
                    total += 3;
        // N3: finder-like
        int[] p1 = { 1, 0, 1, 1, 1, 0, 1, 0, 0, 0, 0 };
        int[] p2 = { 0, 0, 0, 0, 1, 0, 1, 1, 1, 0, 1 };
        for (int r = 0; r < n; r++)
            for (int c = 0; c <= n - 11; c++)
                if (MatchRow(m, r, c, p1) || MatchRow(m, r, c, p2)) total += 40;
        for (int c = 0; c < n; c++)
            for (int r = 0; r <= n - 11; r++)
                if (MatchCol(m, r, c, p1) || MatchCol(m, r, c, p2)) total += 40;
        // N4: balance
        int dark = 0;
        for (int r = 0; r < n; r++) for (int c = 0; c < n; c++) if (m[r, c]) dark++;
        int percent = (dark * 100 + n * n / 2) / (n * n);
        total += Math.Abs(percent - 50) / 5 * 10;
        return total;
    }

    private static bool MatchRow(bool[,] m, int r, int c, int[] pat)
    {
        for (int k = 0; k < pat.Length; k++) if ((m[r, c + k] ? 1 : 0) != pat[k]) return false;
        return true;
    }

    private static bool MatchCol(bool[,] m, int r, int c, int[] pat)
    {
        for (int k = 0; k < pat.Length; k++) if ((m[r + k, c] ? 1 : 0) != pat[k]) return false;
        return true;
    }

    private static void WriteFormatInfo(bool[,] m, bool[,] res, QrErrorCorrection ecc, int mask)
    {
        int ecl = ecc switch
        {
            QrErrorCorrection.Low => 1, QrErrorCorrection.Medium => 0,
            QrErrorCorrection.Quartile => 3, QrErrorCorrection.High => 2,
            _ => 0,
        };
        int data = (ecl << 3) | (mask & 7);
        int fmt = BchEncode(data, 0x537, 15, 5) ^ 0x5412;
        bool GetBit(int i) => ((fmt >> (14 - i)) & 1) != 0;
        int n = m.GetLength(0);
        for (int i = 0; i <= 5; i++) Set(m, res, 8, i, GetBit(i));
        Set(m, res, 8, 7, GetBit(6));
        Set(m, res, 8, 8, GetBit(7));
        Set(m, res, 7, 8, GetBit(8));
        for (int i = 9; i <= 14; i++) Set(m, res, 14 - i, 8, GetBit(i));
        for (int i = 0; i <= 6; i++) Set(m, res, n - 1 - i, 8, GetBit(i));
        for (int i = 7; i <= 14; i++) Set(m, res, 8, n - 15 + i, GetBit(i));
    }

    private static int BchEncode(int data, int gen, int totalBits, int dataBits)
    {
        int d = data << (totalBits - dataBits);
        for (int i = totalBits - 1; i >= totalBits - dataBits; i--)
            if (((d >> i) & 1) != 0) d ^= gen << (i - (totalBits - dataBits));
        return (data << (totalBits - dataBits)) | (d & ((1 << (totalBits - dataBits)) - 1));
    }

    private static void Set(bool[,] m, bool[,] res, int y, int x, bool v)
    {
        m[y, x] = v; res[y, x] = true;
    }

    private sealed class BitBuffer : List<int>
    {
        public int Length => Count;
        public void AppendBits(int val, int len)
        {
            for (int i = len - 1; i >= 0; i--) Add((val >> i) & 1);
        }
    }

    // ── Alignment pattern positions per version (ISO/IEC 18004). ───────────────
    private static readonly int[][] AlignmentPos = new int[][]
    {
        Array.Empty<int>(), Array.Empty<int>(),
        new[]{6,18}, new[]{6,22}, new[]{6,26}, new[]{6,30}, new[]{6,34},
        new[]{6,22,38}, new[]{6,24,42}, new[]{6,26,46}, new[]{6,28,50}, new[]{6,30,54},
        new[]{6,32,58}, new[]{6,34,62}, new[]{6,26,46,66}, new[]{6,26,48,70}, new[]{6,26,50,74},
        new[]{6,30,54,78}, new[]{6,30,56,82}, new[]{6,30,58,86}, new[]{6,34,62,90},
        new[]{6,28,50,72,94}, new[]{6,26,50,74,98}, new[]{6,30,54,78,102}, new[]{6,28,54,80,106},
        new[]{6,32,58,84,110}, new[]{6,30,58,86,114}, new[]{6,34,62,90,118},
        new[]{6,26,50,74,98,122}, new[]{6,30,54,78,102,126}, new[]{6,26,52,78,104,130},
        new[]{6,30,56,82,108,134}, new[]{6,34,60,86,112,138}, new[]{6,30,58,86,114,142},
        new[]{6,34,62,90,118,146}, new[]{6,30,54,78,102,126,150}, new[]{6,24,50,76,102,128,154},
        new[]{6,28,54,80,106,132,158}, new[]{6,32,58,84,110,136,162}, new[]{6,26,54,82,110,138,166},
        new[]{6,30,58,86,114,142,170},
    };

    // ── EC parameters per (version, ECC level): (ecPerBlock, g1Blocks, g1DataCw, g2Blocks, g2DataCw) ──
    // Indexed by [version-1][ecc-as-int].  ecc-as-int: 0=L, 1=M, 2=Q, 3=H.
    private static readonly int[][][] EcTable = new int[][][]
    {
        new[]{ new[]{7,1,19,0,0},   new[]{10,1,16,0,0},  new[]{13,1,13,0,0},  new[]{17,1,9,0,0} }, // V1
        new[]{ new[]{10,1,34,0,0},  new[]{16,1,28,0,0},  new[]{22,1,22,0,0},  new[]{28,1,16,0,0} }, // V2
        new[]{ new[]{15,1,55,0,0},  new[]{26,1,44,0,0},  new[]{18,2,17,0,0},  new[]{22,2,13,0,0} }, // V3
        new[]{ new[]{20,1,80,0,0},  new[]{18,2,32,0,0},  new[]{26,2,24,0,0},  new[]{16,4,9,0,0} },  // V4
        new[]{ new[]{26,1,108,0,0}, new[]{24,2,43,0,0},  new[]{18,2,15,2,16}, new[]{22,2,11,2,12} },// V5
        new[]{ new[]{18,2,68,0,0},  new[]{16,4,27,0,0},  new[]{24,4,19,0,0},  new[]{28,4,15,0,0} }, // V6
        new[]{ new[]{20,2,78,0,0},  new[]{18,4,31,0,0},  new[]{18,2,14,4,15}, new[]{26,4,13,1,14} },// V7
        new[]{ new[]{24,2,97,0,0},  new[]{22,2,38,2,39}, new[]{22,4,18,2,19}, new[]{26,4,14,2,15} },// V8
        new[]{ new[]{30,2,116,0,0}, new[]{22,3,36,2,37}, new[]{20,4,16,4,17}, new[]{24,4,12,4,13} },// V9
        new[]{ new[]{18,2,68,2,69}, new[]{26,4,43,1,44}, new[]{24,6,19,2,20}, new[]{28,6,15,2,16} },// V10
        new[]{ new[]{20,4,81,0,0},  new[]{30,1,50,4,51}, new[]{28,4,22,4,23}, new[]{24,3,12,8,13} },// V11
        new[]{ new[]{24,2,92,2,93}, new[]{22,6,36,2,37}, new[]{26,4,20,6,21}, new[]{28,7,14,4,15} },// V12
        new[]{ new[]{26,4,107,0,0}, new[]{22,8,37,1,38}, new[]{24,8,20,4,21}, new[]{22,12,11,4,12}},// V13
        new[]{ new[]{30,3,115,1,116}, new[]{24,4,40,5,41}, new[]{20,11,16,5,17}, new[]{24,11,12,5,13} },// V14
        new[]{ new[]{22,5,87,1,88}, new[]{24,5,41,5,42}, new[]{30,5,24,7,25}, new[]{24,11,12,7,13} },// V15
        new[]{ new[]{24,5,98,1,99}, new[]{28,7,45,3,46}, new[]{24,15,19,2,20}, new[]{30,3,15,13,16} },// V16
        new[]{ new[]{28,1,107,5,108}, new[]{28,10,46,1,47}, new[]{28,1,22,15,23}, new[]{28,2,14,17,15} },// V17
        new[]{ new[]{30,5,120,1,121}, new[]{26,9,43,4,44}, new[]{28,17,22,1,23}, new[]{28,2,14,19,15} },// V18
        new[]{ new[]{28,3,113,4,114}, new[]{26,3,44,11,45}, new[]{26,17,21,4,22}, new[]{26,9,13,16,14} },// V19
        new[]{ new[]{28,3,107,5,108}, new[]{26,3,41,13,42}, new[]{30,15,24,5,25}, new[]{28,15,15,10,16} },// V20
        new[]{ new[]{28,4,116,4,117}, new[]{26,17,42,0,0}, new[]{28,17,22,6,23}, new[]{30,19,16,6,17} },// V21
        new[]{ new[]{28,2,111,7,112}, new[]{28,17,46,0,0}, new[]{30,7,24,16,25}, new[]{24,34,13,0,0} },// V22
        new[]{ new[]{30,4,121,5,122}, new[]{28,4,47,14,48}, new[]{30,11,24,14,25}, new[]{30,16,15,14,16} },// V23
        new[]{ new[]{30,6,117,4,118}, new[]{28,6,45,14,46}, new[]{30,11,24,16,25}, new[]{30,30,16,2,17} },// V24
        new[]{ new[]{26,8,106,4,107}, new[]{28,8,47,13,48}, new[]{30,7,24,22,25}, new[]{30,22,15,13,16} },// V25
        new[]{ new[]{28,10,114,2,115}, new[]{28,19,46,4,47}, new[]{28,28,22,6,23}, new[]{30,33,16,4,17} },// V26
        new[]{ new[]{30,8,122,4,123}, new[]{28,22,45,3,46}, new[]{30,8,23,26,24}, new[]{30,12,15,28,16} },// V27
        new[]{ new[]{30,3,117,10,118}, new[]{28,3,45,23,46}, new[]{30,4,24,31,25}, new[]{30,11,15,31,16} },// V28
        new[]{ new[]{30,7,116,7,117}, new[]{28,21,45,7,46}, new[]{30,1,23,37,24}, new[]{30,19,15,26,16} },// V29
        new[]{ new[]{30,5,115,10,116}, new[]{28,19,47,10,48}, new[]{30,15,24,25,25}, new[]{30,23,15,25,16} },// V30
        new[]{ new[]{30,13,115,3,116}, new[]{28,2,46,29,47}, new[]{30,42,24,1,25}, new[]{30,23,15,28,16} },// V31
        new[]{ new[]{30,17,115,0,0}, new[]{28,10,46,23,47}, new[]{30,10,24,35,25}, new[]{30,19,15,35,16} },// V32
        new[]{ new[]{30,17,115,1,116}, new[]{28,14,46,21,47}, new[]{30,29,24,19,25}, new[]{30,11,15,46,16} },// V33
        new[]{ new[]{30,13,115,6,116}, new[]{28,14,46,23,47}, new[]{30,44,24,7,25}, new[]{30,59,16,1,17} },// V34
        new[]{ new[]{30,12,121,7,122}, new[]{28,12,47,26,48}, new[]{30,39,24,14,25}, new[]{30,22,15,41,16} },// V35
        new[]{ new[]{30,6,121,14,122}, new[]{28,6,47,34,48}, new[]{30,46,24,10,25}, new[]{30,2,15,64,16} },// V36
        new[]{ new[]{30,17,122,4,123}, new[]{28,29,46,14,47}, new[]{30,49,24,10,25}, new[]{30,24,15,46,16} },// V37
        new[]{ new[]{30,4,122,18,123}, new[]{28,13,46,32,47}, new[]{30,48,24,14,25}, new[]{30,42,15,32,16} },// V38
        new[]{ new[]{30,20,117,4,118}, new[]{28,40,47,7,48}, new[]{30,43,24,22,25}, new[]{30,10,15,67,16} },// V39
        new[]{ new[]{30,19,118,6,119}, new[]{28,18,47,31,48}, new[]{30,34,24,34,25}, new[]{30,20,15,61,16} },// V40
    };

    private static (int totalDataCw, int ecPerBlock, int g1Blocks, int g1DataCw, int g2Blocks, int g2DataCw)
        EcParams(int ver, QrErrorCorrection ecc)
    {
        if (ver is < 1 or > 40) throw new ArgumentOutOfRangeException(nameof(ver), "Version must be 1..40.");
        int eccIndex = ecc switch
        {
            QrErrorCorrection.Low => 0, QrErrorCorrection.Medium => 1,
            QrErrorCorrection.Quartile => 2, QrErrorCorrection.High => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(ecc)),
        };
        var row = EcTable[ver - 1][eccIndex];
        int totalDataCw = row[1] * row[2] + row[3] * row[4];
        return (totalDataCw, row[0], row[1], row[2], row[3], row[4]);
    }
}
