using System.Collections.Concurrent;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Expressions;
using Reporting.Geometry;
using Reporting.Layout.Primitives;
using Reporting.Rendering;
using Reporting.Styling;

namespace Reporting.Layout.Internal;

internal readonly record struct BandLayout(List<LayoutPrimitive> Primitives, Unit Height);
