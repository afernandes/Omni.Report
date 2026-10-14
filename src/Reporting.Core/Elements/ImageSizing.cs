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
