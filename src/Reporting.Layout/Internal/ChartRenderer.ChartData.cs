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

    private sealed class ChartData
    {
        public List<string> Categories { get; } = [];
        public List<SeriesData> Series { get; } = [];
        public double MaxValue { get; set; }
        public double MinValue { get; set; }
    }
}
