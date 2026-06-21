using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using Reporting.Layout.Primitives;

namespace Reporting.Output.Docx;

/// <summary>Embeds a <see cref="DrawImagePrimitive"/> into a Word document as an inline <c>w:drawing</c>.
/// The image bytes are whatever the source <c>ImageElement</c> carried (the renderer does NOT re-encode), so
/// the real format is sniffed from the byte signature and the matching <c>ImagePart</c> content type is used —
/// Word embeds the bytes raw and trusts that label. Kept separate from <see cref="DocxExporter"/> because the
/// OpenXML drawing markup (with its strict child ordering) is verbose and self-contained.</summary>
internal static class DocxImageWriter
{
    private const long EmuPerPoint = 12700;     // 914400 EMU per inch / 72 points
    private const long MaxEmu = 6_858_000;      // ~7.5in — keep an oversized image from overflowing the page

    /// <summary>Appends <paramref name="image"/> to <paramref name="body"/> as a standalone inline-image
    /// paragraph. Returns <c>false</c> (emitting nothing) when the bytes are empty or in a format Word can't
    /// host raw (e.g. WebP) — better to drop one image than to write a corrupt part Word refuses to open.
    /// <paramref name="drawingId"/> must be unique per document (CT_DocProperties Id).</summary>
    public static bool AppendInlineImage(MainDocumentPart main, Body body, DrawImagePrimitive image, uint drawingId)
    {
        ArgumentNullException.ThrowIfNull(main);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(image);

        var bytes = ToArray(image.Data);
        if (bytes.Length == 0 || DetectFormat(bytes) is not { } partType)
        {
            return false;
        }

        var imagePart = main.AddImagePart(partType);
        using (var ms = new MemoryStream(bytes))
        {
            imagePart.FeedData(ms);
        }
        var relId = main.GetIdOfPart(imagePart);

        long cx = Clamp(ToEmu(image.Bounds.Width.ToPoints()));
        long cy = Clamp(ToEmu(image.Bounds.Height.ToPoints()));

        // CT_Inline child order is FIXED by the schema: Extent, EffectExtent, DocProperties, Graphic.
        var inline = new DW.Inline(
            new DW.Extent { Cx = cx, Cy = cy },
            new DW.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
            new DW.DocProperties { Id = drawingId, Name = "Image" + drawingId },
            new A.Graphic(
                new A.GraphicData(
                    new PIC.Picture(
                        new PIC.NonVisualPictureProperties(
                            new PIC.NonVisualDrawingProperties { Id = drawingId, Name = "Image" + drawingId },
                            new PIC.NonVisualPictureDrawingProperties()),
                        new PIC.BlipFill(
                            new A.Blip { Embed = relId },
                            new A.Stretch(new A.FillRectangle())),
                        new PIC.ShapeProperties(
                            new A.Transform2D(
                                new A.Offset { X = 0, Y = 0 },
                                new A.Extents { Cx = cx, Cy = cy }),
                            new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
        {
            DistanceFromTop = 0,
            DistanceFromBottom = 0,
            DistanceFromLeft = 0,
            DistanceFromRight = 0,
        };

        body.AppendChild(new Paragraph(new Run(new Drawing(inline))));
        return true;
    }

    /// <summary>Maps the leading magic bytes to the OpenXML image part type. Returns null for formats Word
    /// can't embed raw (so the caller skips rather than mislabels) — re-encoding those is a follow-up.</summary>
    private static PartTypeInfo? DetectFormat(byte[] b)
    {
        if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xD8)
        {
            return ImagePartType.Jpeg;
        }
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47)
        {
            return ImagePartType.Png;
        }
        if (b.Length >= 3 && b[0] == 0x47 && b[1] == 0x49 && b[2] == 0x46) // GIF
        {
            return ImagePartType.Gif;
        }
        if (b.Length >= 2 && b[0] == 0x42 && b[1] == 0x4D) // BMP
        {
            return ImagePartType.Bmp;
        }
        if (b.Length >= 4 && ((b[0] == 0x49 && b[1] == 0x49 && b[2] == 0x2A) // TIFF little-endian
                           || (b[0] == 0x4D && b[1] == 0x4D && b[3] == 0x2A))) // TIFF big-endian
        {
            return ImagePartType.Tiff;
        }
        return null; // unknown / unsupported (e.g. WebP) — skip rather than write a corrupt png-labelled part
    }

    private static long ToEmu(double points) => (long)Math.Round(points * EmuPerPoint);

    private static long Clamp(long emu) => emu <= 0 ? 1 : Math.Min(emu, MaxEmu);

    private static byte[] ToArray(Reporting.Common.EquatableArray<byte> data)
    {
        var copy = new byte[data.Count];
        for (int i = 0; i < copy.Length; i++)
        {
            copy[i] = data[i];
        }
        return copy;
    }
}
