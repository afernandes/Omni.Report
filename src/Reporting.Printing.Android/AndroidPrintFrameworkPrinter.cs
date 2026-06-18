using Reporting.Common;
using Reporting.Layout;
using Reporting.Output.Pdf;
using Reporting.Paper;

namespace Reporting.Printing.Android;

#if OMNIREPORT_ANDROID_STUB

/// <summary>
/// <see cref="IReportPrinter"/> for the Android Print Framework.
/// </summary>
/// <remarks>
/// This is the <em>non-Android</em> stub — when the project is compiled without the
/// <c>android</c> workload (which is the default on CI Linux/Windows), all operations
/// throw <see cref="PlatformNotSupportedException"/>. The real implementation, gated on
/// <c>OMNIREPORT_BUILD_ANDROID=true</c>, sits below this <c>#if</c> branch.
/// </remarks>
public sealed class AndroidPrintFrameworkPrinter : IReportPrinter
{
    public string Driver => "android-print";

    public Task<IReadOnlyList<PrinterInfo>> ListPrintersAsync(CancellationToken cancellationToken = default)
        => throw new PlatformNotSupportedException(
            "Reporting.Printing.Android requires the .NET Android workload. " +
            "Build with OMNIREPORT_BUILD_ANDROID=true after `dotnet workload install android`.");

    public Task<PrinterCapabilities> GetCapabilitiesAsync(string printerName, CancellationToken cancellationToken = default)
        => throw new PlatformNotSupportedException(
            "Reporting.Printing.Android requires the .NET Android workload.");

    public Task<PrintResult> PrintAsync(RenderedReport report, PrintOptions options, CancellationToken cancellationToken = default)
        => Task.FromResult(new PrintResult(
            Succeeded: false,
            PagesPrinted: 0,
            ErrorMessage: "Reporting.Printing.Android stub (build without the android workload). " +
                          "Enable OMNIREPORT_BUILD_ANDROID=true to compile the real driver."));
}

#else

using Android.Content;
using Android.OS;
using Android.Print;
using Android.App;
using Java.IO;

/// <summary>
/// <see cref="IReportPrinter"/> driving the Android Print Framework
/// (<see cref="PrintManager"/> + <see cref="PrintDocumentAdapter"/>). The OmniReport
/// <see cref="RenderedReport"/> is converted to a vector-native PDF in-memory via
/// <see cref="SkiaPdfExporter"/>, then handed off to the system Print Manager — which in
/// turn dispatches to any user-selected target (Google Cloud Print, Mopria, PDF, etc.).
/// </summary>
public sealed class AndroidPrintFrameworkPrinter : IReportPrinter
{
    private readonly Context _context;

    public AndroidPrintFrameworkPrinter(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public string Driver => "android-print";

    public Task<IReadOnlyList<PrinterInfo>> ListPrintersAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PrinterInfo>>(
            [new PrinterInfo("Android Print Service", IsDefault: true, Driver: Driver)]);

    public Task<PrinterCapabilities> GetCapabilitiesAsync(string printerName, CancellationToken cancellationToken = default)
        => Task.FromResult(new PrinterCapabilities(
            PrinterName: printerName,
            SupportedPapers: EquatableArray.Create(PaperSize.A4, PaperSize.Letter, PaperSize.A5),
            PaperBins: EquatableArray<string>.Empty,
            SupportsDuplex: true,
            SupportsColor: true));

    public Task<PrintResult> PrintAsync(RenderedReport report, PrintOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(options);

        var pdfBytes = new SkiaPdfExporter(new PdfExportOptions { Title = report.Name }).ExportToBytes(report);
        var jobName = options.DocumentName ?? report.Name;

        var printManager = (PrintManager?)_context.GetSystemService(Context.PrintService);
        if (printManager is null)
        {
            return Task.FromResult(new PrintResult(false, 0, ErrorMessage: "PrintManager is not available on this Android device."));
        }

        var adapter = new InMemoryPdfPrintAdapter(pdfBytes, jobName);
        var attrs = new PrintAttributes.Builder()
            .SetMediaSize(PrintAttributes.MediaSize.IsoA4)
            .SetColorMode(PrintColorMode.Color)
            .Build();
        printManager.Print(jobName, adapter, attrs);

        return Task.FromResult(new PrintResult(Succeeded: true, PagesPrinted: report.Pages.Count));
    }

    private sealed class InMemoryPdfPrintAdapter : PrintDocumentAdapter
    {
        private readonly byte[] _pdfBytes;
        private readonly string _jobName;

        public InMemoryPdfPrintAdapter(byte[] pdfBytes, string jobName)
        {
            _pdfBytes = pdfBytes;
            _jobName = jobName;
        }

        public override void OnLayout(PrintAttributes? oldAttributes,
                                      PrintAttributes? newAttributes,
                                      CancellationSignal? cancellationSignal,
                                      LayoutResultCallback? callback,
                                      Bundle? extras)
        {
            if (cancellationSignal?.IsCanceled == true)
            {
                callback?.OnLayoutCancelled();
                return;
            }
            var info = new PrintDocumentInfo.Builder(_jobName)
                .SetContentType(PrintContentType.Document)
                .Build();
            callback?.OnLayoutFinished(info, changed: !ReferenceEquals(oldAttributes, newAttributes));
        }

        public override void OnWrite(PageRange[]? pages,
                                     ParcelFileDescriptor? destination,
                                     CancellationSignal? cancellationSignal,
                                     WriteResultCallback? callback)
        {
            if (destination is null)
            {
                callback?.OnWriteFailed("destination is null");
                return;
            }
            try
            {
                using var stream = new FileOutputStream(destination.FileDescriptor);
                stream.Write(_pdfBytes);
                stream.Flush();
                callback?.OnWriteFinished(pages ?? [PageRange.AllPages]);
            }
            catch (Java.Lang.Exception ex)
            {
                callback?.OnWriteFailed(ex.Message);
            }
        }
    }
}

#endif
