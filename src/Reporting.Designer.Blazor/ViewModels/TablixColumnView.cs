using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Reflection;
using Reporting.Common;
using Reporting.Designer.Blazor.Services;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Metadata;
using Reporting.Styling;

namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>One Tablix column in the designer editor — a header label + a per-row detail
/// expression + an optional relative width (weight). A width of <c>0</c> means "equal share";
/// when at least one column has a positive weight, the renderer distributes width proportionally.</summary>
public sealed record TablixColumnView(string Header, string Expression, double Width = 0);
