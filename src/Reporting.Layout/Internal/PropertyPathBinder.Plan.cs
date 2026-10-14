using Reporting.Common;
using System.Globalization;
using System.Reflection;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Styling;

namespace Reporting.Layout.Internal;

internal static partial class PropertyPathBinder
{

    private sealed record Plan(Level[] Levels, Type LeafType);
}
