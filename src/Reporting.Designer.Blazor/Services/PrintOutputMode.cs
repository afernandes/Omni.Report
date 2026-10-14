using Reporting.Geometry;
using Reporting.Paper;

namespace Reporting.Designer.Blazor.Services;

/// <summary>How we actually push the bytes to a printer.</summary>
public enum PrintOutputMode
{
    /// <summary>Open the rendered PDF in a new browser tab/window and call
    /// <c>window.print()</c>. The user picks printer + driver options in the
    /// browser/OS dialog. Universal — works in Server, WebAssembly and MAUI Blazor.</summary>
    BrowserDialog,
    /// <summary>Send directly to a named printer via <c>IReportPrinter</c> (Windows spooler,
    /// ESC/POS, Android Print). Requires a native print service registered in DI; the
    /// dialog hides this option when no such service is available.</summary>
    SystemSpooler,
    /// <summary>Generate the PDF and trigger a file download. Useful when the user wants
    /// to email/keep it rather than send to a physical device.</summary>
    SaveAsPdf,
}
