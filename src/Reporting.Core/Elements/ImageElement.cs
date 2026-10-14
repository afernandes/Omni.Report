using Reporting.Common;

namespace Reporting.Elements;

/// <summary>RDL <c>Image</c> — a raster picture sourced inline, from a path, or from an expression (<see cref="Source"/>),
/// scaled to its bounds per <see cref="Sizing"/>.</summary>
public sealed record ImageElement : ReportElement
{
    /// <summary>Which of the three source properties below actually supplies the bytes.</summary>
    public ImageSourceKind Source { get; init; } = ImageSourceKind.Path;

    /// <summary>File path or URL, used when <see cref="Source"/> is <see cref="ImageSourceKind.Path"/>.</summary>
    /// <remarks>A URL makes the renderer perform an outbound request to an address that comes from the
    /// report definition. If your definitions are not fully trusted, that is a server-side request forgery
    /// vector: restrict egress at the network layer rather than relying on validation here.</remarks>
    public string? Path { get; init; }

    /// <summary>Expression resolving to bytes or a path, used when <see cref="Source"/> is
    /// <see cref="ImageSourceKind.Expression"/>. This is how an image column from the database is bound.</summary>
    public string? Expression { get; init; }

    /// <summary>The picture bytes themselves, used when <see cref="Source"/> is
    /// <see cref="ImageSourceKind.Inline"/>. They travel inside the report file.</summary>
    public EquatableArray<byte> InlineData { get; init; } = EquatableArray<byte>.Empty;

    /// <summary>How the picture is scaled into its bounds — fit (letterbox), fill (crop), stretch or native.</summary>
    public ImageSizing Sizing { get; init; } = ImageSizing.Fit;
}
