using System.IO.Ports;
using System.Net.Sockets;

namespace Reporting.Printing.EscPos;

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
