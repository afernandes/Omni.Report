namespace Reporting.Output.Svg;

/// <summary>One page rendered as a self-contained SVG fragment, ready for embedding.</summary>
/// <param name="WidthPt">Page width in PostScript points (1pt = 1/72 inch).</param>
/// <param name="HeightPt">Page height in PostScript points.</param>
/// <param name="SvgMarkup">The full <c>&lt;svg …&gt;…&lt;/svg&gt;</c> markup. Already includes an
/// explicit <c>viewBox</c> and CSS-friendly <c>width="100%" height="100%"</c> so the consumer
/// only needs to size its container.</param>
public sealed record SvgPageFragment(double WidthPt, double HeightPt, string SvgMarkup);
