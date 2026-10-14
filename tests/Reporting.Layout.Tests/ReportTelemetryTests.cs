using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using FluentAssertions;
using Xunit;

namespace Reporting.Layout.Tests;

public sealed class ReportTelemetryTests
{
    [Fact]
    public async Task PaginateAsync_ListenersAtivos_PublicaFasesSemDadosDoRelatorio()
    {
        using var parent = new Activity("validation").Start();
        var phases = new ConcurrentQueue<string>();
        var tags = new ConcurrentQueue<string>();
        using var activities = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "OmniReport",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                if (activity.TraceId != parent.TraceId) return;
                phases.Enqueue(activity.OperationName);
                foreach (var pair in activity.TagObjects) tags.Enqueue(pair.Key);
            }
        };
        ActivitySource.AddActivityListener(activities);
        var measurements = new ConcurrentQueue<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, owner) =>
        {
            if (instrument.Meter.Name == "OmniReport") owner.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, _, _) =>
        {
            if (Activity.Current?.TraceId == parent.TraceId) measurements.Enqueue(instrument.Name);
        });
        listener.Start();
        await new ReportPaginator().PaginateAsync(TestData.BuildRequest(TestData.GroupedReport(), TestData.ThreeRows()));
        phases.Should().Contain(["report.data", "report.layout"]);
        tags.Should().OnlyContain(tag => tag == "report.outcome");
        measurements.Should().Contain(["omnireport.rows.read", "omnireport.pages.produced"]);
    }
}
