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
/// some platform, <see cref="Subset(IntPtr, long, int, IEnumerable{int})"/> answers <see langword="null"/> and the
/// caller embeds the font whole, as the exporter always did.</para>
/// </remarks>
internal static class HarfBuzzSubsetter
{
    /// <summary><c>HB_SUBSET_FLAGS_NOTDEF_OUTLINE</c> (<c>hb-subset.h</c>): keep the outline of glyph 0.</summary>
    private const uint NotdefOutlineFlag = 0x00000040u;

    /// <summary>Returns a standalone font file holding only <paramref name="codepoints"/> (plus the glyphs HarfBuzz
    /// must keep, like <c>.notdef</c> and composite-glyph components), or <see langword="null"/> when subsetting is
    /// not possible.</summary>
    /// <param name="font">Start of the font file in memory — a TrueType/OpenType file or a collection.</param>
    /// <param name="length">Length of the font file in bytes.</param>
    /// <param name="faceIndex">Index of the face inside a collection (<c>.ttc</c>); 0 for a single font.</param>
    /// <param name="codepoints">Unicode codepoints to keep.</param>
    public static byte[]? Subset(IntPtr font, long length, int faceIndex, IEnumerable<int> codepoints)
        => Subset(NativeApi.Instance, font, length, faceIndex, codepoints);

    /// <summary>Same as the public overload, against <paramref name="api"/> instead of the native library — the seam
    /// that lets a test stand in a partially exported library.</summary>
    internal static byte[]? Subset(INativeApi api, IntPtr font, long length, int faceIndex, IEnumerable<int> codepoints)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(codepoints);
        if (font == IntPtr.Zero || length <= 0 || length > uint.MaxValue || faceIndex < 0)
        {
            return null;
        }
        try
        {
            return SubsetCore(api, font, (uint)length, (uint)faceIndex, codepoints);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            return null;
        }
    }

    private static byte[]? SubsetCore(INativeApi api, IntPtr font, uint length, uint faceIndex, IEnumerable<int> codepoints)
    {
        IntPtr blob = IntPtr.Zero, face = IntPtr.Zero, input = IntPtr.Zero, subset = IntPtr.Zero, result = IntPtr.Zero;
        try
        {
            blob = api.BlobCreate(font, length);
            face = api.FaceCreate(blob, faceIndex);
            input = api.SubsetInputCreate();
            if (input == IntPtr.Zero)
            {
                return null;
            }
            // By default hb-subset keeps .notdef's advance but empties its outline. A character no installed font
            // covers is drawn as the primary font's .notdef, so without this the missing-glyph box becomes blank space.
            api.SubsetInputSetFlags(input, NotdefOutlineFlag);
            var unicodes = api.SubsetInputUnicodeSet(input);
            foreach (var codepoint in codepoints)
            {
                api.SetAdd(unicodes, (uint)codepoint);
            }

            subset = api.SubsetOrFail(face, input);
            if (subset == IntPtr.Zero)
            {
                return null;
            }
            result = api.FaceReferenceBlob(subset);
            var data = api.BlobGetData(result, out uint size);
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
            // Only release what was acquired, even though hb_*_destroy accepts null: on a library that exports the
            // core API but not hb-subset, hb_subset_input_destroy is missing as well — calling it would throw and skip
            // the releases below it, leaking HarfBuzz's copy of the font on every export. Each object holds its own
            // references, so the order between them does not matter.
            if (result != IntPtr.Zero)
            {
                api.BlobDestroy(result);
            }
            if (subset != IntPtr.Zero)
            {
                api.FaceDestroy(subset);
            }
            if (input != IntPtr.Zero)
            {
                api.SubsetInputDestroy(input);
            }
            if (face != IntPtr.Zero)
            {
                api.FaceDestroy(face);
            }
            if (blob != IntPtr.Zero)
            {
                api.BlobDestroy(blob);
            }
        }
    }

    /// <summary>The slice of the HarfBuzz C API the subsetter calls.</summary>
    internal interface INativeApi
    {
        /// <summary><c>hb_blob_create</c> in <c>HB_MEMORY_MODE_DUPLICATE</c>.</summary>
        IntPtr BlobCreate(IntPtr data, uint length);
        IntPtr BlobGetData(IntPtr blob, out uint length);
        void BlobDestroy(IntPtr blob);
        IntPtr FaceCreate(IntPtr blob, uint index);
        IntPtr FaceReferenceBlob(IntPtr face);
        void FaceDestroy(IntPtr face);
        IntPtr SubsetInputCreate();
        void SubsetInputSetFlags(IntPtr input, uint flags);
        IntPtr SubsetInputUnicodeSet(IntPtr input);
        void SubsetInputDestroy(IntPtr input);
        void SetAdd(IntPtr set, uint codepoint);
        IntPtr SubsetOrFail(IntPtr source, IntPtr input);
    }

    private sealed class NativeApi : INativeApi
    {
        private const string Library = "libHarfBuzzSharp";

        /// <summary><c>HB_MEMORY_MODE_DUPLICATE</c>: HarfBuzz copies the bytes, so the caller's buffer only has to
        /// outlive the <c>hb_blob_create</c> call itself.</summary>
        private const int MemoryModeDuplicate = 0;

        public static readonly NativeApi Instance = new();

        public IntPtr BlobCreate(IntPtr data, uint length) => hb_blob_create(data, length, MemoryModeDuplicate, IntPtr.Zero, IntPtr.Zero);
        public IntPtr BlobGetData(IntPtr blob, out uint length) => hb_blob_get_data(blob, out length);
        public void BlobDestroy(IntPtr blob) => hb_blob_destroy(blob);
        public IntPtr FaceCreate(IntPtr blob, uint index) => hb_face_create(blob, index);
        public IntPtr FaceReferenceBlob(IntPtr face) => hb_face_reference_blob(face);
        public void FaceDestroy(IntPtr face) => hb_face_destroy(face);
        public IntPtr SubsetInputCreate() => hb_subset_input_create_or_fail();
        public void SubsetInputSetFlags(IntPtr input, uint flags) => hb_subset_input_set_flags(input, flags);
        public IntPtr SubsetInputUnicodeSet(IntPtr input) => hb_subset_input_unicode_set(input);
        public void SubsetInputDestroy(IntPtr input) => hb_subset_input_destroy(input);
        public void SetAdd(IntPtr set, uint codepoint) => hb_set_add(set, codepoint);
        public IntPtr SubsetOrFail(IntPtr source, IntPtr input) => hb_subset_or_fail(source, input);

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
        private static extern void hb_subset_input_set_flags(IntPtr input, uint value);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr hb_subset_input_unicode_set(IntPtr input);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern void hb_subset_input_destroy(IntPtr input);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern void hb_set_add(IntPtr set, uint codepoint);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr hb_subset_or_fail(IntPtr source, IntPtr input);
    }
}
