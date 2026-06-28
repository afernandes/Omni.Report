using Reporting.Common;

namespace Reporting.Elements;

/// <summary>How an <see cref="ImageElement"/> is scaled to fit its bounds — stretch, fit (letterbox), fill (crop), or native size.</summary>
public enum ImageSizing
{
    /// <summary>Stretch the image to fit the bounds (may distort aspect ratio).</summary>
    Stretch,

    /// <summary>Preserve aspect ratio; pad with empty space (letterbox).</summary>
    Fit,

    /// <summary>Preserve aspect ratio; crop to fill bounds.</summary>
    Fill,

    /// <summary>Render at native size, anchored to top-left.</summary>
    Native,
}

/// <summary>Where an <see cref="ImageElement"/> gets its bytes from — embedded inline, a file path, or a runtime expression.</summary>
public enum ImageSourceKind
{
    /// <summary>Image bytes are embedded inline (<see cref="ImageElement.InlineData"/>).</summary>
    Inline,

    /// <summary>Absolute or relative file path (<see cref="ImageElement.Path"/>).</summary>
    Path,

    /// <summary>Expression that resolves at runtime to bytes or a path
    /// (<see cref="ImageElement.Expression"/>).</summary>
    Expression,
}

/// <summary>RDL <c>Image</c> — a raster picture sourced inline, from a path, or from an expression (<see cref="Source"/>),
/// scaled to its bounds per <see cref="Sizing"/>.</summary>
public sealed record ImageElement : ReportElement
{
    public ImageSourceKind Source { get; init; } = ImageSourceKind.Path;
    public string? Path { get; init; }
    public string? Expression { get; init; }
    public EquatableArray<byte> InlineData { get; init; } = EquatableArray<byte>.Empty;
    public ImageSizing Sizing { get; init; } = ImageSizing.Fit;
}
