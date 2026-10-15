using FluentAssertions;
using Xunit;

namespace Reporting.Output.Pdf.Tests;

public sealed class HarfBuzzSubsetterTests
{
    /// <summary>A libHarfBuzzSharp built with the core API but without hb-subset: the blob and face calls work,
    /// every hb_subset_* entry point is missing — including its destroy function.</summary>
    private sealed class LibraryWithoutSubset : HarfBuzzSubsetter.INativeApi
    {
        private static readonly IntPtr Blob = 0x10, Face = 0x20;

        public List<IntPtr> DestroyedBlobs { get; } = [];
        public List<IntPtr> DestroyedFaces { get; } = [];

        public IntPtr BlobCreate(IntPtr data, uint length) => Blob;
        public IntPtr FaceCreate(IntPtr blob, uint index) => Face;
        public void BlobDestroy(IntPtr blob) => DestroyedBlobs.Add(blob);
        public void FaceDestroy(IntPtr face) => DestroyedFaces.Add(face);

        public IntPtr SubsetInputCreate() => throw new EntryPointNotFoundException("hb_subset_input_create_or_fail");
        public void SubsetInputDestroy(IntPtr input) => throw new EntryPointNotFoundException("hb_subset_input_destroy");
        public void SubsetInputSetFlags(IntPtr input, uint flags) => throw new EntryPointNotFoundException("hb_subset_input_set_flags");
        public IntPtr SubsetInputUnicodeSet(IntPtr input) => throw new EntryPointNotFoundException("hb_subset_input_unicode_set");
        public IntPtr SubsetOrFail(IntPtr source, IntPtr input) => throw new EntryPointNotFoundException("hb_subset_or_fail");

        public IntPtr BlobGetData(IntPtr blob, out uint length) => throw new InvalidOperationException("not reached");
        public IntPtr FaceReferenceBlob(IntPtr face) => throw new InvalidOperationException("not reached");
        public void SetAdd(IntPtr set, uint codepoint) => throw new InvalidOperationException("not reached");
    }

    [Fact]
    public void Subset_LibraryWithoutSubsetEntryPoints_ReleasesTheBlobAndFaceItAllocated()
    {
        var library = new LibraryWithoutSubset();

        var result = HarfBuzzSubsetter.Subset(library, 0x1000, 128, 0, [65, 66]);

        // Falling back to the whole font is fine; leaking the copy HarfBuzz made of it is not — it happens on every
        // export, and the copy is the size of the font file. Cleanup must only touch handles that were acquired: the
        // destroy function of the missing API is missing too, and calling it would skip the releases after it.
        result.Should().BeNull("a library without hb-subset means embedding the font whole");
        library.DestroyedFaces.Should().Equal([(IntPtr)0x20], "the face was created and must be released exactly once");
        library.DestroyedBlobs.Should().Equal([(IntPtr)0x10], "the duplicated font buffer must be released exactly once");
    }
}
