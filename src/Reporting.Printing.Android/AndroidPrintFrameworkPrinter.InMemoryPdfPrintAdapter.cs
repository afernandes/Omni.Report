using Reporting.Common;
using Reporting.Layout;
using Reporting.Paper;

namespace Reporting.Printing.Android;

#if !OMNIREPORT_ANDROID_STUB


using global::Android.Content;
using global::Android.OS;
using global::Android.Print;
using PrinterInfo = Reporting.Printing.PrinterInfo;
using global::Android.App;
using global::Java.IO;

public sealed partial class AndroidPrintFrameworkPrinter {

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
                callback?.OnWriteFinished(pages ?? [PageRange.AllPages ?? throw new InvalidOperationException("Android page range is unavailable.")]);
            }
            catch (global::Java.Lang.Exception ex)
            {
                callback?.OnWriteFailed(ex.Message);
            }
        }
    }
}

#endif
