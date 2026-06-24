using Reporting.Geometry;

namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>
/// Positional align / distribute for a multi-selection — the classic design-tool operation that moves the
/// selected elements relative to each other (NOT text alignment). Pure geometry over the elements' bounds, so it
/// unit-tests directly; the UI applies the returned positions to each element's <see cref="Rectangle.X"/>/Y.
/// </summary>
public static class Arrange
{
    public enum Op
    {
        AlignLeft, AlignHCenter, AlignRight,
        AlignTop, AlignVCenter, AlignBottom,
        DistributeH, DistributeV,
    }

    /// <summary>Computes the new top-left (X, Y) for each input element (same order). Elements with fewer than two
    /// items, or unchanged axes, keep their current position.</summary>
    public static IReadOnlyList<(Unit X, Unit Y)> Compute(Op op, IReadOnlyList<Rectangle> bounds)
    {
        var n = bounds.Count;
        var res = new (Unit X, Unit Y)[n];
        for (var i = 0; i < n; i++)
        {
            res[i] = (bounds[i].X, bounds[i].Y);
        }
        if (n < 2)
        {
            return res;
        }

        int Left(int i) => bounds[i].X.Mils;
        int Right(int i) => bounds[i].X.Mils + bounds[i].Width.Mils;
        int Top(int i) => bounds[i].Y.Mils;
        int Bottom(int i) => bounds[i].Y.Mils + bounds[i].Height.Mils;
        var all = Enumerable.Range(0, n).ToList();

        switch (op)
        {
            case Op.AlignLeft:
                var minL = all.Min(Left);
                for (var i = 0; i < n; i++) res[i] = (new Unit(minL), bounds[i].Y);
                break;
            case Op.AlignRight:
                var maxR = all.Max(Right);
                for (var i = 0; i < n; i++) res[i] = (new Unit(maxR - bounds[i].Width.Mils), bounds[i].Y);
                break;
            case Op.AlignHCenter:
                var cx = (all.Min(Left) + all.Max(Right)) / 2;
                for (var i = 0; i < n; i++) res[i] = (new Unit(cx - bounds[i].Width.Mils / 2), bounds[i].Y);
                break;
            case Op.AlignTop:
                var minT = all.Min(Top);
                for (var i = 0; i < n; i++) res[i] = (bounds[i].X, new Unit(minT));
                break;
            case Op.AlignBottom:
                var maxB = all.Max(Bottom);
                for (var i = 0; i < n; i++) res[i] = (bounds[i].X, new Unit(maxB - bounds[i].Height.Mils));
                break;
            case Op.AlignVCenter:
                var cy = (all.Min(Top) + all.Max(Bottom)) / 2;
                for (var i = 0; i < n; i++) res[i] = (bounds[i].X, new Unit(cy - bounds[i].Height.Mils / 2));
                break;
            case Op.DistributeH:
                // Keep the leftmost/rightmost fixed; spread the rest so the edge-to-edge gaps are equal.
                var ordH = all.OrderBy(Left).ThenBy(i => i).ToList();
                var spanH = Right(ordH[^1]) - Left(ordH[0]);
                var widths = ordH.Sum(i => bounds[i].Width.Mils);
                var gapH = (spanH - widths) / (n - 1);
                var xH = Left(ordH[0]);
                foreach (var i in ordH)
                {
                    res[i] = (new Unit(xH), bounds[i].Y);
                    xH += bounds[i].Width.Mils + gapH;
                }
                break;
            case Op.DistributeV:
                var ordV = all.OrderBy(Top).ThenBy(i => i).ToList();
                var spanV = Bottom(ordV[^1]) - Top(ordV[0]);
                var heights = ordV.Sum(i => bounds[i].Height.Mils);
                var gapV = (spanV - heights) / (n - 1);
                var yV = Top(ordV[0]);
                foreach (var i in ordV)
                {
                    res[i] = (bounds[i].X, new Unit(yV));
                    yV += bounds[i].Height.Mils + gapV;
                }
                break;
        }
        return res;
    }
}
