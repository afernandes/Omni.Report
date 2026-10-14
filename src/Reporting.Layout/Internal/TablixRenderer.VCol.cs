using Reporting.Elements;
using Reporting.Expressions;
using Reporting.Geometry;
using Reporting.Layout.Primitives;
using Reporting.Rendering;
using Reporting.Styling;

namespace Reporting.Layout.Internal;

internal static partial class TablixRenderer
{

    /// <summary>A rendered matrix column: either a single leaf data column (<c>Leaf</c> ≥ 0, summing just
    /// that column-leaf), or a subtotal/grand-total column (<see cref="IsSubtotal"/>, summing the column-leaf
    /// block <c>[Start..End]</c> per row). Built once so headers, data rows and total rows all iterate the
    /// same column list.</summary>
    private readonly partial record struct VCol(int Leaf, int Start, int End, string Label, bool IsSubtotal)
    {
        public static VCol LeafCol(int j) => new(j, j, j, string.Empty, false);
        public static VCol Subtotal(int start, int end, string label) => new(-1, start, end, label, true);
    }
}
