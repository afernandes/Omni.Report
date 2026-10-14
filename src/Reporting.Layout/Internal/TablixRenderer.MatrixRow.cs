using Reporting.Elements;
using Reporting.Expressions;
using Reporting.Geometry;
using Reporting.Layout.Primitives;
using Reporting.Rendering;
using Reporting.Styling;

namespace Reporting.Layout.Internal;

internal static partial class TablixRenderer
{

    /// <summary>One planned visual row of a matrix body: a data row (<c>IsTotal=false</c>, <c>Leaf</c> = row-leaf
    /// index) or a subtotal/grand-total row (<c>IsTotal=true</c>, summing leaf block <c>[Start..End]</c>).
    /// <c>ResetLevel</c> is the row-group level whose merged-look headers redraw after a subtotal.</summary>
    private readonly partial record struct MatrixRow(bool IsTotal, int Leaf, string Label, int Start, int End, int ResetLevel);
}
