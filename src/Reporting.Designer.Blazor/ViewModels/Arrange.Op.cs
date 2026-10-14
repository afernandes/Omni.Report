using Reporting.Geometry;

namespace Reporting.Designer.Blazor.ViewModels;

public static partial class Arrange
{
    public enum Op
    {
        AlignLeft, AlignHCenter, AlignRight,
        AlignTop, AlignVCenter, AlignBottom,
        DistributeH, DistributeV,
    }
}
