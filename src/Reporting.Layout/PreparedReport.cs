using System.Text.Json;
using Reporting.Common;
using Reporting.Layout.Internal;

namespace Reporting.Layout;

/// <summary>A paginated document backed by private temporary storage. Only the index and a requested
/// page need remain in managed memory. Dispose to close and delete the owned temporary file.</summary>
public sealed class PreparedReport : IDisposable
{
    private readonly FileStream _storage;
    private readonly long _maxBytes;
    private readonly List<(long Offset, int Length)> _pages = [];
    private readonly object _gate = new();
    private bool _disposed;

    internal PreparedReport(string name, PreparedReportOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxStorageBytes);
        Name = name;
        _maxBytes = options.MaxStorageBytes;
        var directory = options.TemporaryDirectory ?? Path.GetTempPath();
        Directory.CreateDirectory(directory);
        var streamOptions = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.ReadWrite,
            Share = FileShare.None,
            BufferSize = 4096,
            Options = FileOptions.DeleteOnClose | FileOptions.RandomAccess
        };
        if (!OperatingSystem.IsWindows())
        {
            streamOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }
        _storage = new FileStream(Path.Combine(directory, $"omnireport-{Guid.NewGuid():N}.tmp"), streamOptions);
    }

    /// <summary>Report name.</summary>
    public string Name { get; }

    /// <summary>Number of prepared pages.</summary>
    public int PageCount => _pages.Count;

    /// <summary>Serialized bytes retained on disk.</summary>
    public long StorageBytes { get { lock (_gate) { ThrowIfDisposed(); return _storage.Length; } } }

    /// <summary>Definition limitations identified by the paginator.</summary>
    public EquatableArray<PaginationDiagnostic> Diagnostics { get; internal set; }

    internal void Append(RenderedPage page)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(page, PreparedPageSerializer.Options);
        lock (_gate)
        {
            ThrowIfDisposed();
            if (bytes.LongLength > _maxBytes - _storage.Length)
                throw new PaginationLimitExceededException(nameof(PreparedReportOptions.MaxStorageBytes), _maxBytes);
            long offset = _storage.Length;
            _storage.Position = offset;
            _storage.Write(bytes);
            _pages.Add((offset, bytes.Length));
        }
    }

    /// <summary>Loads one zero-based page. The caller owns the returned immutable page reference.
    /// Cancellation is checked before reading and after deserialization.</summary>
    public RenderedPage GetPage(int index, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            ThrowIfDisposed();
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _pages.Count);
            var (offset, length) = _pages[index];
            _storage.Position = offset;
            var bytes = new byte[length];
            _storage.ReadExactly(bytes);
            var page = JsonSerializer.Deserialize<RenderedPage>(bytes, PreparedPageSerializer.Options)
                ?? throw new InvalidDataException("The prepared page was empty.");
            cancellationToken.ThrowIfCancellationRequested();
            return page;
        }
    }

    /// <summary>Releases temporary storage. Safe to call repeatedly.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _pages.Clear();
            _storage.Dispose();
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
