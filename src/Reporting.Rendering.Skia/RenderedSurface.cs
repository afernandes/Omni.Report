using Reporting.Paper;

namespace Reporting.Rendering.Skia;

/// <summary>One rasterized page produced by <see cref="SkiaRenderingContext"/>.</summary>
public sealed record RenderedSurface(PageSetup PageSetup, int WidthPx, int HeightPx, byte[] PngBytes);
