using Microsoft.Extensions.DependencyInjection;

namespace Reporting.Viewer.Blazor;

/// <summary>DI helpers for hosts that embed the <see cref="ReportViewer"/>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers the default <see cref="ReportViewerOptions"/> in the container.
    /// Hosts may override with <c>services.Configure&lt;ReportViewerOptions&gt;(...)</c>.</summary>
    public static IServiceCollection AddOmniReportViewer(
        this IServiceCollection services,
        Action<ReportViewerOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton(_ =>
        {
            var options = ReportViewerOptions.Default;
            if (configure is not null)
            {
                var mutable = new MutableViewerOptions(options);
                configure(mutable.Snapshot());
                options = mutable.Snapshot();
            }
            return options;
        });
        return services;
    }

    private sealed class MutableViewerOptions
    {
        private ReportViewerOptions _current;
        public MutableViewerOptions(ReportViewerOptions initial) => _current = initial;
        public ReportViewerOptions Snapshot() => _current;
    }
}
