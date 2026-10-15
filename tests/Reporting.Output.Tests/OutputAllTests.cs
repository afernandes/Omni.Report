using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Reporting.Output.Tests;

/// <summary>Output.All is the one-call way to get every first-party format; the Designer and the Viewer only see
/// what is registered, so a format missing here is a format missing from their menus.</summary>
public sealed class OutputAllTests
{
    [Fact]
    public void AddOmniReportExporters_RegistraOsNoveFormatosUmaVezCada()
    {
        var services = new ServiceCollection().AddOmniReportExporters();

        // The registrations are factories that take nothing from the container, so an empty provider builds them.
        var formats = services.Where(d => d.ServiceType == typeof(IReportExporter))
            .Select(d => ((IReportExporter)d.ImplementationFactory!(EmptyProvider.Instance)).Format)
            .ToList();

        formats.Should().BeEquivalentTo(["pdf", "xlsx", "docx", "html", "svg", "csv", "json", "xml", "markdown"]);
        formats.Should().OnlyHaveUniqueItems();
    }

    private sealed class EmptyProvider : IServiceProvider
    {
        public static readonly EmptyProvider Instance = new();
        public object? GetService(Type serviceType) => null;
    }
}
