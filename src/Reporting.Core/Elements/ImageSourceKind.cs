using Reporting.Common;

namespace Reporting.Elements;

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
