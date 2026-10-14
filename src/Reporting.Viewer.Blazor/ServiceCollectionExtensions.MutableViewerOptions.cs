using Microsoft.Extensions.DependencyInjection;

namespace Reporting.Viewer.Blazor;

public static partial class ServiceCollectionExtensions
{

    private sealed class MutableViewerOptions
    {
        private ReportViewerOptions _current;
        public MutableViewerOptions(ReportViewerOptions initial) => _current = initial;
        public ReportViewerOptions Snapshot() => _current;
    }
}
