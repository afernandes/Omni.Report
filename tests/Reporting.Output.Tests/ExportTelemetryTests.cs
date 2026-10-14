using System.Collections.Concurrent;
using System.Diagnostics;
using FluentAssertions;
using Reporting.Common;
using Reporting.Layout;
using Reporting.Output.Image;
using Xunit;

namespace Reporting.Output.Tests;

public sealed class ExportTelemetryTests
{
    [Theory]
    [InlineData("success")]
    [InlineData("error")]
    [InlineData("cancelled")]
    public void ExportPages_ResultadoDaOperacao_PublicaResultadoEFechaActivity(string outcome)
    {
        using var parent = new Activity("validation").Start();
        var recorded = new ConcurrentQueue<string?>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "OmniReport",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                if (activity.TraceId == parent.TraceId && activity.OperationName == "report.export")
                {
                    recorded.Enqueue(activity.GetTagItem("report.outcome") as string);
                }
            }
        };
        ActivitySource.AddActivityListener(listener);
        using var cancellation = new CancellationTokenSource();
        if (outcome == "cancelled")
        {
            cancellation.Cancel();
        }
        var report = new RenderedReport("private-name", EquatableArray<RenderedPage>.Empty);
        Action export = () => new PngImageExporter().ExportPages(
            outcome == "error" ? null! : report, _ => new MemoryStream(), cancellation.Token);
        if (outcome == "error")
        {
            export.Should().Throw<ArgumentNullException>();
        }
        else if (outcome == "cancelled")
        {
            export.Should().Throw<OperationCanceledException>();
        }
        else
        {
            export.Should().NotThrow();
        }
        recorded.Should().Equal(outcome);
        Activity.Current.Should().BeSameAs(parent);
    }
}
