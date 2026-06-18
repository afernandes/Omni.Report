using System.IO.Ports;
using System.Net.Sockets;

namespace Reporting.Printing.EscPos;

/// <summary>
/// Sink for ESC/POS byte sequences. Decouples the printer driver from the wire — the same
/// <see cref="EscPosPrinter"/> works against a TCP socket (Ethernet thermal printers), a
/// serial port (COM/USB-CDC), an arbitrary <see cref="Stream"/>, or an in-memory buffer
/// (tests).
/// </summary>
public interface IEscPosTransport : IAsyncDisposable
{
    string Name { get; }
    Task SendAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default);
}

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

/// <summary>Serial port transport (USB-CDC / RS-232). Note: requires
/// <c>System.IO.Ports</c> package on non-Windows platforms.</summary>
public sealed class SerialEscPosTransport : IEscPosTransport
{
    private readonly SerialPort _port;

    public SerialEscPosTransport(string portName, int baudRate = 9600)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(portName);
        _port = new SerialPort(portName, baudRate)
        {
            Handshake = Handshake.None,
            ReadTimeout = 2000,
            WriteTimeout = 2000,
        };
        _port.Open();
        Name = $"serial:{portName}@{baudRate}";
    }

    public string Name { get; }

    public Task SendAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _port.Write(bytes.ToArray(), 0, bytes.Length);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _port.Close();
        _port.Dispose();
        return ValueTask.CompletedTask;
    }
}
