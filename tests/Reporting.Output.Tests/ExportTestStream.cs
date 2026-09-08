namespace Reporting.Output.Tests;

internal sealed class ExportTestStream : Stream
{
    internal MemoryStream Buffer { get; } = new();
    internal bool Disposed { get; private set; }
    internal bool FailWrites { get; init; }
    internal Action? OnWrite { get; init; }
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => !Disposed;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);
        OnWrite?.Invoke();
        if (FailWrites) throw new IOException("Falha simulada no destino.");
        Buffer.Write(buffer);
    }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    protected override void Dispose(bool disposing)
    {
        Disposed = true;
        if (disposing) Buffer.Dispose();
        base.Dispose(disposing);
    }
}
