using System.Diagnostics;
using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;

namespace Reporting.Printing.WindowsSpooler.Tests;

internal static class PdfPrintOutput
{
    internal static async Task<PdfDocument> OpenCompletedAsync(string path)
    {
        var started = Stopwatch.GetTimestamp();
        Exception? lastError = null;
        while (Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(15))
        {
            try
            {
                // PrintDocument.Print completes submission; the spooler may still be writing.
                var bytes = await File.ReadAllBytesAsync(path);
                if (bytes.Length >= 5 && Encoding.ASCII.GetString(bytes, 0, 5) == "%PDF-" &&
                    Encoding.ASCII.GetString(bytes, Math.Max(0, bytes.Length - 1024), Math.Min(1024, bytes.Length)).Contains("%%EOF", StringComparison.Ordinal))
                {
                    return PdfDocument.Open(bytes);
                }
            }
            catch (IOException error) { lastError = error; }
            catch (PdfDocumentFormatException error) { lastError = error; }
            await Task.Delay(50);
        }
        throw new TimeoutException($"The print spooler did not finish a valid PDF at '{path}' within 15 seconds.", lastError);
    }
}
