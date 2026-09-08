using System.Diagnostics;
using System.Text;
using System.Text.Json;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;

namespace Reporting.Printing.WindowsSpooler.Tests;

internal static class PdfPrintOutput
{
    internal static async Task<PdfDocument> OpenCompletedAsync(string path)
    {
        var started = Stopwatch.GetTimestamp();
        Exception? lastError = null;
        byte[] lastSnapshot = [];
        while (Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(30))
        {
            try
            {
                // PrintDocument.Print completes submission; the spooler may still be writing.
                using var snapshot = new MemoryStream();
                await using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous))
                {
                    await input.CopyToAsync(snapshot);
                }
                var bytes = snapshot.ToArray();
                lastSnapshot = bytes;
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
        var diagnosticsRoot = Path.Combine(Environment.GetEnvironmentVariable("GITHUB_WORKSPACE") ?? AppContext.BaseDirectory, "TestResults", "printing");
        Directory.CreateDirectory(diagnosticsRoot);
        var name = Path.GetFileNameWithoutExtension(path);
        await File.WriteAllBytesAsync(Path.Combine(diagnosticsRoot, name + ".pdf"), lastSnapshot);
        await File.WriteAllTextAsync(Path.Combine(diagnosticsRoot, name + ".json"), JsonSerializer.Serialize(new
        {
            Path = path, CapturedUtc = DateTimeOffset.UtcNow, Bytes = lastSnapshot.Length,
            Error = lastError?.ToString(), Elapsed = Stopwatch.GetElapsedTime(started).TotalSeconds,
        }));
        throw new TimeoutException($"The print spooler did not finish a valid PDF at '{path}' within 30 seconds. Last snapshot: {lastSnapshot.Length} bytes. Diagnostics: {diagnosticsRoot}.", lastError);
    }
}
