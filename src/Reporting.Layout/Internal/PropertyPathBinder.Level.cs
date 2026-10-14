using Reporting.Common;
using System.Globalization;
using System.Reflection;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Styling;

namespace Reporting.Layout.Internal;

internal static partial class PropertyPathBinder
{
    // One level of the path. The owner at this level is rebuilt immutably with <see cref="Prop"/> changed:
    // a record CLASS via its synthesized <c>&lt;Clone&gt;$</c> + init-only set; a record STRUCT (Rectangle,
    // Thickness, …) — which has no <c>&lt;Clone&gt;$</c> — by re-invoking its positional constructor with
    // every component copied except the one being navigated. That makes struct-segment paths such as
    // <c>"Bounds.Width"</c> and <c>"Style.Padding.Left"</c> actually bind instead of silently no-op'ing.
    private sealed partial record Level(PropertyInfo Prop, MethodInfo? CloneFn, ConstructorInfo? Ctor, PropertyInfo[]? CtorProps);
}
