namespace Reporting.Output;

/// <summary>
/// Turns an exporter whose writer is synchronous into a real <see cref="IReportExporter.ExportAsync"/>: the
/// destination stream only ever receives asynchronous writes (ASP.NET Core refuses synchronous I/O on a response
/// body), and cancellation is observed between pages, so a client that gives up stops the work.
/// </summary>
public static class AsyncExport
{
    /// <summary>
    /// Runs a writer that yields at its checkpoints — after each page, or every few hundred rows. The writer writes
    /// synchronously into a memory buffer; at each yield the buffer is flushed to <paramref name="output"/>
    /// asynchronously and the token is checked, so memory stays bounded by what one step produces.
    /// </summary>
    /// <param name="output">Destination stream.</param>
    /// <param name="steps">Writes to the stream it receives and yields at each checkpoint. Any writer it wraps
    /// (e.g. a <see cref="StreamWriter"/>) must be flushed before yielding, or that step's bytes are flushed with
    /// the next one.</param>
    /// <param name="cancellationToken">Checked before the first step and at every checkpoint.</param>
    public static async Task WriteInStepsAsync(Stream output, Func<Stream, IEnumerable<int>> steps,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(steps);
        cancellationToken.ThrowIfCancellationRequested();
        using var buffer = new MemoryStream();
        foreach (var _ in steps(buffer))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await DrainAsync(buffer, output, cancellationToken).ConfigureAwait(false);
        }
        // Whatever the writer flushed when it was disposed, at the end of the steps.
        await DrainAsync(buffer, output, cancellationToken).ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs a writer that needs the whole document before it can emit it — a ZIP package (DOCX, XLSX) or a library
    /// that writes everything at once — into memory, then copies it to <paramref name="output"/> asynchronously.
    /// </summary>
    /// <param name="output">Destination stream.</param>
    /// <param name="write">Writes the document; it receives the token to check between pages.</param>
    /// <param name="cancellationToken">Checked before writing, by <paramref name="write"/>, and during the copy.</param>
    public static async Task WriteBufferedAsync(Stream output, Action<Stream, CancellationToken> write,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(write);
        cancellationToken.ThrowIfCancellationRequested();
        using var buffer = new MemoryStream();
        write(buffer, cancellationToken);
        buffer.Position = 0;
        await buffer.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task DrainAsync(MemoryStream buffer, Stream output, CancellationToken cancellationToken)
    {
        if (buffer.Length == 0)
        {
            return;
        }
        await output.WriteAsync(buffer.GetBuffer().AsMemory(0, (int)buffer.Length), cancellationToken).ConfigureAwait(false);
        buffer.SetLength(0);
    }
}
