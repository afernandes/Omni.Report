using System.Runtime.InteropServices;

namespace Reporting.Output.Pdf;

/// <summary>
/// Cuts a font file down to the characters a document uses, through HarfBuzz's <c>hb-subset</c>.
/// </summary>
/// <remarks>
/// <para>The managed HarfBuzzSharp wrapper does not expose <c>hb-subset</c>, but its native library exports the C API
/// (verified on win-x64, linux-x64, linux-arm64 and osx for the pinned version), so this calls it directly. Every
/// signature below is taken from <c>hb-blob.h</c>, <c>hb-face.h</c>, <c>hb-set.h</c> and <c>hb-subset.h</c>, and
/// uses only blittable types.</para>
/// <para>Subsetting is an optimisation, never a requirement: if the native library or an entry point is missing on
/// some platform, <see cref="Subset"/> answers <see langword="null"/> and the caller embeds the font whole, as the
/// exporter always did.</para>
/// </remarks>
internal static class HarfBuzzSubsetter
{
    private const string Library = "libHarfBuzzSharp";

    /// <summary><c>HB_MEMORY_MODE_DUPLICATE</c>: HarfBuzz copies the bytes, so the caller's buffer only has to
    /// outlive the <c>hb_blob_create</c> call itself.</summary>
    private const int MemoryModeDuplicate = 0;

    /// <summary>Returns a standalone font file holding only <paramref name="codepoints"/> (plus the glyphs HarfBuzz
    /// must keep, like <c>.notdef</c> and composite-glyph components), or <see langword="null"/> when subsetting is
    /// not possible.</summary>
    /// <param name="font">Start of the font file in memory — a TrueType/OpenType file or a collection.</param>
    /// <param name="length">Length of the font file in bytes.</param>
    /// <param name="faceIndex">Index of the face inside a collection (<c>.ttc</c>); 0 for a single font.</param>
    /// <param name="codepoints">Unicode codepoints to keep.</param>
    public static byte[]? Subset(IntPtr font, long length, int faceIndex, IEnumerable<int> codepoints)
    {
        ArgumentNullException.ThrowIfNull(codepoints);
        if (font == IntPtr.Zero || length <= 0 || length > uint.MaxValue || faceIndex < 0)
        {
            return null;
        }
        try
        {
            return SubsetCore(font, (uint)length, (uint)faceIndex, codepoints);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            return null;
        }
    }

    private static byte[]? SubsetCore(IntPtr font, uint length, uint faceIndex, IEnumerable<int> codepoints)
    {
        IntPtr blob = IntPtr.Zero, face = IntPtr.Zero, input = IntPtr.Zero, subset = IntPtr.Zero, result = IntPtr.Zero;
        try
        {
            blob = hb_blob_create(font, length, MemoryModeDuplicate, IntPtr.Zero, IntPtr.Zero);
            face = hb_face_create(blob, faceIndex);
            input = hb_subset_input_create_or_fail();
            if (input == IntPtr.Zero)
            {
                return null;
            }
            var unicodes = hb_subset_input_unicode_set(input);
            foreach (var codepoint in codepoints)
            {
                hb_set_add(unicodes, (uint)codepoint);
            }

            subset = hb_subset_or_fail(face, input);
            if (subset == IntPtr.Zero)
            {
                return null;
            }
            result = hb_face_reference_blob(subset);
            var data = hb_blob_get_data(result, out uint size);
            if (data == IntPtr.Zero || size == 0)
            {
                return null;
            }
            var bytes = new byte[size];
            Marshal.Copy(data, bytes, 0, bytes.Length);
            return bytes;
        }
        finally
        {
            // hb_*_destroy is null-safe; each object holds its own references, so release order does not matter.
            hb_blob_destroy(result);
            hb_face_destroy(subset);
            hb_subset_input_destroy(input);
            hb_face_destroy(face);
            hb_blob_destroy(blob);
        }
    }

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr hb_blob_create(IntPtr data, uint length, int mode, IntPtr userData, IntPtr destroy);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr hb_blob_get_data(IntPtr blob, out uint length);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void hb_blob_destroy(IntPtr blob);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr hb_face_create(IntPtr blob, uint index);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr hb_face_reference_blob(IntPtr face);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void hb_face_destroy(IntPtr face);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr hb_subset_input_create_or_fail();

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr hb_subset_input_unicode_set(IntPtr input);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void hb_subset_input_destroy(IntPtr input);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void hb_set_add(IntPtr set, uint codepoint);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr hb_subset_or_fail(IntPtr source, IntPtr input);
}
