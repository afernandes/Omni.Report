using Microsoft.Extensions.DependencyInjection;
using Reporting.DataSources;
using Reporting.Layout;
using Reporting.Output.Pdf;
using Reporting.Output.Excel;
using Reporting.Printing;
using Reporting.Rendering;
using Reporting.Rendering.Skia;
using Reporting.Serialization;

namespace Reporting.Hosting;

/// <summary>DI entry point. Usage:
/// <code>
/// services.AddReporting(opts =&gt; opts
///     .UseSkiaRendering()
///     .UsePdfOutput()
///     .UseExcelOutput()
///     .UsePrinter&lt;WindowsSpoolerPrinter&gt;()
///     .AddDataSource("Vendas", vendasList));
/// </code></summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddReporting(
        this IServiceCollection services,
        Action<ReportingBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var builder = new ReportingBuilder(services);
        // Sensible defaults: Skia rendering + PDF/XLSX exporters.
        builder.UseSkiaRendering();
        builder.UsePdfOutput();
        builder.UseExcelOutput();
        configure?.Invoke(builder);
        return services;
    }
}
