namespace Reporting.Styling;

/// <summary>A background image painted behind an element's content (RDL <c>&lt;Style&gt;&lt;BackgroundImage&gt;</c>).
/// Phase B supports the <b>External</b> source — a static <see cref="Path"/> (file/URL) or a per-row
/// <see cref="Expression"/> yielding bytes/path — stretched to the element bounds. Embedded images, tiling
/// (<c>BackgroundRepeat</c>) and max-size are a follow-up (phase C).</summary>
public sealed record BackgroundImage(string? Path = null, string? Expression = null)
{
    /// <summary>True when the source is a per-row expression rather than a static path.</summary>
    public bool IsExpression => !string.IsNullOrEmpty(Expression);
}
