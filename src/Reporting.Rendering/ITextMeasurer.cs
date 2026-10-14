using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Paper;

namespace Reporting.Rendering;

/// <summary>Pure text-measurement surface — exposed to the layout engine for measuring
/// without owning a full rendering context (e.g. during pagination).</summary>
public interface ITextMeasurer
{
    /// <summary>Measures the space <paramref name="text"/> would occupy in <paramref name="style"/>,
    /// wrapping within <paramref name="maxWidth"/> when one is given.</summary>
    Size Measure(string text, TextStyle style, Unit? maxWidth = null);
}
