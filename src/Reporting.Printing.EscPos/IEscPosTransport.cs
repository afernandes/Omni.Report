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
