using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Reporting.Diagnostics;

/// <summary>Opt-in diagnostics using standard .NET listeners. No report names, definitions, queries,
/// field values or parameters are attached. Instruments use the source name <c>OmniReport</c>.</summary>
public static class ReportTelemetry
{
    internal static readonly ActivitySource Source = new("OmniReport");
    private static readonly Meter Meter = new("OmniReport");
    internal static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("omnireport.phase.duration", "s");
    private static readonly Counter<long> Rows = Meter.CreateCounter<long>("omnireport.rows.read", "{row}");
    private static readonly Counter<long> Pages = Meter.CreateCounter<long>("omnireport.pages.produced", "{page}");

    /// <summary>Starts a data, layout, render or export operation. Call Complete on successful completion;
    /// otherwise disposal records error, or cancellation if the supplied token was cancelled.</summary>
    public static ReportOperation Start(string phase, CancellationToken cancellationToken = default)
    {
        if (phase is not ("data" or "layout" or "render" or "export"))
            throw new ArgumentOutOfRangeException(nameof(phase));
        return new ReportOperation(phase, cancellationToken);
    }

    /// <summary>Records provider rows consumed. Includes all providers in an execution.</summary>
    public static void RecordRows(long count) => Rows.Add(count);

    /// <summary>Records completed pages, including subreports and counting passes.</summary>
    public static void RecordPage() => Pages.Add(1);
}
