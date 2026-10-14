using System.IO.Ports;
using System.Net.Sockets;

namespace Reporting.Printing.EscPos;

/// <summary>TCP socket transport (e.g. Ethernet thermal printer on port 9100, the de-facto
/// Raw printer port). Auto-disposes the connection on <see cref="DisposeAsync"/>.</summary>
public sealed class TcpEscPosTransport : IEscPosTransport
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;

    public TcpEscPosTransport(string host, int port = 9100)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        _client = new TcpClient();
        _client.Connect(host, port);
        _stream = _client.GetStream();
        Name = $"tcp://{host}:{port}";
    }

    public string Name { get; }

    public async Task SendAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
    {
        await _stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await _stream.DisposeAsync().ConfigureAwait(false);
        _client.Dispose();
    }
}
