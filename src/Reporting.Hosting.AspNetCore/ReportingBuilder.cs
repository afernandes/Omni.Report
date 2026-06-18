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

/// <summary>
/// Fluent configurator for OmniReport. Returned by
/// <see cref="ServiceCollectionExtensions.AddReporting"/>. Consumers chain
/// <c>UseXyz()</c> / <c>AddDataSource&lt;T&gt;()</c> to register the components their host
/// actually needs.
/// </summary>
public sealed class ReportingBuilder
{
    public ReportingBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        Services = services;
        DataSources = new DataSourceRegistry();
        Services.AddSingleton(DataSources);
        Services.AddSingleton<IReportPaginator, ReportPaginator>();
        Services.AddSingleton<RepxSerializer>();
        Services.AddSingleton<RepJsonSerializer>();
        Services.AddSingleton<IReportSerializer>(sp => sp.GetRequiredService<RepxSerializer>());
    }

    public IServiceCollection Services { get; }

    /// <summary>Shared registry the host can pre-populate before serving requests.</summary>
    public DataSourceRegistry DataSources { get; }

    /// <summary>Registers <see cref="SkiaRenderingContext"/> as the default
    /// <see cref="IRenderingContext"/> / <see cref="ITextMeasurer"/> (cross-platform).</summary>
    public ReportingBuilder UseSkiaRendering(float dpi = 96f)
    {
        Services.AddTransient<IRenderingContext>(_ => new SkiaRenderingContext(dpi));
        Services.AddTransient<ITextMeasurer>(_ => new SkiaRenderingContext(dpi));
        return this;
    }

    /// <summary>Registers <see cref="SkiaPdfExporter"/> for vector-native PDF export.</summary>
    public ReportingBuilder UsePdfOutput(PdfExportOptions? options = null)
    {
        Services.AddSingleton(options ?? PdfExportOptions.Default);
        Services.AddSingleton<SkiaPdfExporter>(sp =>
            new SkiaPdfExporter(sp.GetRequiredService<PdfExportOptions>()));
        Services.AddSingleton<IReportExporter>(sp => sp.GetRequiredService<SkiaPdfExporter>());
        return this;
    }

    /// <summary>Registers the ClosedXML-backed <see cref="ExcelExporter"/>.</summary>
    public ReportingBuilder UseExcelOutput(ExcelExportOptions? options = null)
    {
        Services.AddSingleton(options ?? ExcelExportOptions.Default);
        Services.AddSingleton<ExcelExporter>(sp =>
            new ExcelExporter(sp.GetRequiredService<ExcelExportOptions>()));
        return this;
    }

    /// <summary>Registers a concrete <see cref="IReportPrinter"/> instance. The Windows
    /// spooler / ESC/POS / Android implementations are in their dedicated packages; the
    /// host picks whichever fits its target platform.</summary>
    public ReportingBuilder UsePrinter(IReportPrinter printer)
    {
        ArgumentNullException.ThrowIfNull(printer);
        Services.AddSingleton(printer);
        return this;
    }

    /// <summary>Registers an <see cref="IReportPrinter"/> resolved from DI by type.</summary>
    public ReportingBuilder UsePrinter<TPrinter>() where TPrinter : class, IReportPrinter
    {
        Services.AddSingleton<IReportPrinter, TPrinter>();
        return this;
    }

    /// <summary>Adds a strongly-typed in-memory data source.</summary>
    public ReportingBuilder AddDataSource<T>(string name, IEnumerable<T> items)
    {
        DataSources.Register(new Reporting.DataSources.Enumerable.EnumerableDataSource<T>(name, items));
        return this;
    }

    /// <summary>Adds an arbitrary <see cref="IReportDataSource"/> implementation.</summary>
    public ReportingBuilder AddDataSource(IReportDataSource dataSource)
    {
        DataSources.Register(dataSource);
        return this;
    }
}

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
