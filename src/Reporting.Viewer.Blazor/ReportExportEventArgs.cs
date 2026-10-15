using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Reporting.Layout;
using Reporting.Output.Excel;
using Reporting.Printing;
using Reporting.Rendering.Skia;

namespace Reporting.Viewer.Blazor;

/// <summary>Arguments raised by <see cref="ReportViewer.OnExported"/>.</summary>
public sealed record ReportExportEventArgs(string Extension, string FileName, byte[] Bytes);
