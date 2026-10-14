using System.Reflection;

namespace Reporting.Designer.Blazor.ViewModels;

public static partial class ToolboxCatalog
{

    public sealed partial record Item(
        DesignerElementKind Kind, string Label, string Icon, string Description, string? Hotkey, bool Draggable,
        double DefaultWidthMm, double DefaultHeightMm, string? DefaultText, string? DefaultExpression, string? PreviewLabel);
}
