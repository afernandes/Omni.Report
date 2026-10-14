using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Reporting;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Paper;
using Reporting.Styling;
using Reporting.Designer.Blazor;
using Reporting.Designer.Blazor.Components;
using Reporting.Designer.Blazor.Icons;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Output.Pdf;
using Reporting.Output.Excel;
using Reporting.Printing;
namespace Reporting.Designer.Blazor.Components;

public partial class ContextMenu
{

    public sealed class ContextMenuItem
    {
        public string Label { get; init; } = string.Empty;
        public string? Shortcut { get; init; }
        public bool IsSeparator { get; init; }
        public bool IsDisabled { get; init; }
        public bool IsDanger { get; init; }
        public EventCallback? OnClick { get; init; }

        public static ContextMenuItem Separator() => new() { IsSeparator = true };
    }
}
