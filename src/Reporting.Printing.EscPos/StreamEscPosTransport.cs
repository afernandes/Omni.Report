using System.IO.Ports;
using System.Net.Sockets;

namespace Reporting.Printing.EscPos;

/// <summary>Writes ESC/POS bytes to any <see cref="Stream"/>. Useful for tests and "dump
/// to file" workflows.</summary>
public sealed class StreamEscPosTransport : IEscPosTransport
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;

    public StreamEscPosTransport(Stream stream, string? name = null, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _stream = stream;
        _leaveOpen = leaveOpen;
        Name = name ?? "stream";
    }

    public string Name { get; }

    public async Task SendAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
        => await _stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);

    public async ValueTask DisposeAsync()
    {
        if (!_leaveOpen)
        {
            await _stream.DisposeAsync().ConfigureAwait(false);
        }
    }
}
