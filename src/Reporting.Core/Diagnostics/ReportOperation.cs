using System.Diagnostics;

namespace Reporting.Diagnostics;

/// <summary>Lifetime of one diagnostic phase. Dispose on every exit path.</summary>
public sealed class ReportOperation : IDisposable
{
    private readonly string _phase;
    private readonly CancellationToken _token;
    private readonly long _started = Stopwatch.GetTimestamp();
    private readonly Activity? _activity;
    private bool _completed;
    private bool _disposed;

    internal ReportOperation(string phase, CancellationToken token)
    {
        _phase = phase;
        _token = token;
        _activity = ReportTelemetry.Source.StartActivity($"report.{phase}");
    }

    /// <summary>Marks a successful phase, including operations that produced no rows or pages.</summary>
    public void Complete() => _completed = true;

    /// <summary>Records duration and success, error or cancelled outcome and closes the activity.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        string outcome = _completed ? "success" : _token.IsCancellationRequested ? "cancelled" : "error";
        _activity?.SetTag("report.outcome", outcome);
        _activity?.SetStatus(_completed ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
        try
        {
            ReportTelemetry.Duration.Record(Stopwatch.GetElapsedTime(_started).TotalSeconds,
                new KeyValuePair<string, object?>("report.phase", _phase),
                new KeyValuePair<string, object?>("report.outcome", outcome));
        }
        finally
        {
            _activity?.Dispose();
        }
    }
}
