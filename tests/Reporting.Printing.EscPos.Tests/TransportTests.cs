using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Reporting.Printing.EscPos;
using Xunit;

namespace Reporting.Printing.EscPos.Tests;

public class StreamTransportTests
{
    [Fact]
    public async Task Stream_transport_writes_bytes_to_underlying_stream()
    {
        using var ms = new MemoryStream();
        var transport = new StreamEscPosTransport(ms, leaveOpen: true);
        await transport.SendAsync(new byte[] { 1, 2, 3 });
        ms.ToArray().Should().Equal([1, 2, 3]);
    }

    [Fact]
    public async Task Stream_transport_uses_default_name_when_unset()
    {
        await using var transport = new StreamEscPosTransport(new MemoryStream());
        transport.Name.Should().Be("stream");
    }

    [Fact]
    public async Task Stream_transport_propagates_caller_provided_name()
    {
        await using var transport = new StreamEscPosTransport(new MemoryStream(), name: "demo");
        transport.Name.Should().Be("demo");
    }

    [Fact]
    public async Task Stream_transport_dispose_closes_when_not_leave_open()
    {
        var ms = new MemoryStream();
        var transport = new StreamEscPosTransport(ms);
        await transport.DisposeAsync();
        Action act = () => ms.WriteByte(1);
        act.Should().Throw<ObjectDisposedException>();
    }
}

public class TcpTransportTests
{
    [Fact]
    public async Task Tcp_transport_sends_bytes_to_loopback_listener()
    {
        using var listener = new TcpListener(IPAddress.Loopback, port: 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        // Accept in background.
        var acceptTask = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var stream = client.GetStream();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            return buffer.ToArray();
        });

        await using (var transport = new TcpEscPosTransport("127.0.0.1", port))
        {
            transport.Name.Should().Be($"tcp://127.0.0.1:{port}");
            await transport.SendAsync(new byte[] { 0x1B, (byte)'@', 0x0A });
        }

        var received = await acceptTask;
        received.Should().Equal([0x1B, (byte)'@', 0x0A]);
    }

    [Fact]
    public void Tcp_transport_rejects_empty_host()
    {
        Action act = () => new TcpEscPosTransport(" ");
        act.Should().Throw<ArgumentException>();
    }
}
