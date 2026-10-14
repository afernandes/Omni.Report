using System.Globalization;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Expressions;
using Reporting.Geometry;
using Reporting.Layout.Primitives;
using Reporting.Rendering;
using Reporting.Styling;

namespace Reporting.Layout.Internal;

internal static partial class ChartRenderer
{

    // ─── Data ──────────────────────────────────────────────────────────────────────

    private sealed partial class SeriesData
    {
        public required string Name { get; init; }
        public required Color Color { get; init; }
        public List<double> Values { get; } = []; // aligned to ChartData.Categories
        public List<double> Sizes { get; } = [];  // bubble radius input (summed per category)
        public List<double> Highs { get; } = [];  // stock high (max per category)
        public List<double> Lows { get; } = [];   // stock low (min per category)
    }
}
