using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Reporting.Output.Pdf;

namespace Reporting.Designer.Blazor.Services;

/// <summary>One-liner DI helpers so hosts get sensible printing defaults without
/// having to know which interfaces / lifetimes apply.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="BrowserPrintService"/> as the default
    /// <see cref="IDesignerPrintService"/>. Works for every host (Blazor Server,
    /// Blazor WebAssembly, MAUI Blazor Hybrid) because it goes through the browser/WebView
    /// <c>window.print()</c> dialog — the same flow every web designer uses.
    /// </summary>
    /// <remarks>
    /// <para>Call this once in your host's <c>Program.cs</c> / <c>MauiProgram.cs</c>:</para>
    /// <code>
    /// builder.Services.AddOmniReportDesignerPrinting();
    /// </code>
    ///
    /// <para>If you ALSO want direct-to-spooler printing (no dialog), follow it with the
    /// <c>WithNative*</c> override matching your platform. The order doesn't matter —
    /// later registrations win for the <see cref="IDesignerPrintService"/> contract.</para>
    /// </remarks>
    public static IServiceCollection AddOmniReportDesignerPrinting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        // Lifetime = Scoped because BrowserPrintService depends on IJSRuntime, which is
        // scoped to the Blazor circuit (one per connected user). Singleton would fail DI
        // validation with "Cannot consume scoped service 'IJSRuntime'". Per-circuit also
        // matches reality: there's no reason to share the service across users — it's
        // stateless on the .NET side, the JS interop is naturally per-circuit.
        services.TryAddScoped<BrowserPrintService>();
        services.TryAddScoped<IDesignerPrintService>(sp => sp.GetRequiredService<BrowserPrintService>());
        // Registry is Singleton (stateless once built — DI containers may inject scoped
        // IReportExporter instances if the host wants per-circuit options, but the
        // wrapper itself doesn't need to be per-user).
        services.TryAddSingleton<IExporterRegistry, ExporterRegistry>();
        return services;
    }

    /// <summary>
    /// Registers the seven first-party exporters (PDF, XLSX, HTML+SVG, SVG, CSV, JSON,
    /// Markdown) as <see cref="IReportExporter"/> singletons. The Designer's
    /// <see cref="IExporterRegistry"/> auto-discovers them and renders one button per
    /// format in the PreviewMode toolbar — no hard-coded list.
    /// </summary>
    /// <remarks>
    /// <para>Hosts that want a subset register manually instead of calling this — every
    /// exporter is just <c>services.AddSingleton&lt;IReportExporter, TExporter&gt;()</c>.</para>
    ///
    /// <para>Hosts that want custom labels / icons / sort order for a format register an
    /// <see cref="ExporterDescriptor"/> explicitly — the registry deduplicates by
    /// <c>Format</c> and explicit descriptors win over the auto-wrapped ones.</para>
    /// </remarks>
    public static IServiceCollection AddDefaultExporters(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IReportExporter>(_ => new Reporting.Output.Pdf.SkiaPdfExporter());
        services.AddSingleton<IReportExporter>(_ => new Reporting.Output.Excel.ExcelExporter());
        services.AddSingleton<IReportExporter>(_ => new Reporting.Output.Docx.DocxExporter());
        services.AddSingleton<IReportExporter>(_ => new Reporting.Output.Html.SvgHtmlExporter());
        services.AddSingleton<IReportExporter>(_ => new Reporting.Output.Svg.SvgExporter());
        services.AddSingleton<IReportExporter>(_ => new Reporting.Output.Csv.CsvExporter());
        services.AddSingleton<IReportExporter>(_ => new Reporting.Output.Json.JsonExporter());
        services.AddSingleton<IReportExporter>(_ => new Reporting.Output.Xml.XmlExporter());
        services.AddSingleton<IReportExporter>(_ => new Reporting.Output.Markdown.MarkdownExporter());
        return services;
    }

    /// <summary>
    /// Upgrades the print pipeline to use a native <c>IReportPrinter</c> (Windows Spooler,
    /// ESC/POS, Android Print) when the user picks <see cref="PrintOutputMode.SystemSpooler"/>
    /// in the dialog. Other modes (BrowserDialog, SaveAsPdf) still go through the browser
    /// fallback — best of both worlds.
    /// </summary>
    /// <typeparam name="TPrinter">Concrete printer driver — typically
    /// <c>WindowsSpoolerPrinter</c> on Windows or <c>AndroidPrintFrameworkPrinter</c> on
    /// Android. Must be registered separately as an <c>IReportPrinter</c> implementation
    /// (or registered here when not already in DI).</typeparam>
    /// <remarks>
    /// <para>Typical wiring for a MAUI Windows host:</para>
    /// <code>
    /// builder.Services.AddOmniReportDesignerPrinting()
    ///     .WithNativePrinter&lt;WindowsSpoolerPrinter&gt;();
    /// </code>
    /// </remarks>
    public static IServiceCollection WithNativePrinter<TPrinter>(this IServiceCollection services)
        where TPrinter : class, Reporting.Printing.IReportPrinter
    {
        ArgumentNullException.ThrowIfNull(services);
        // TPrinter itself can be Singleton (stateless across users in our drivers) —
        // but the WRAPPING service must be Scoped so it can depend on the also-Scoped
        // BrowserPrintService which transitively pulls IJSRuntime.
        services.TryAddSingleton<TPrinter>();
        services.TryAddSingleton<Reporting.Printing.IReportPrinter>(sp => sp.GetRequiredService<TPrinter>());
        services.TryAddScoped<BrowserPrintService>();
        // Replace whatever IDesignerPrintService was registered with the adapter.
        services.RemoveAll<IDesignerPrintService>();
        services.AddScoped<IDesignerPrintService>(sp => new NativePrinterAdapter(
            sp.GetRequiredService<Reporting.Printing.IReportPrinter>(),
            sp.GetRequiredService<BrowserPrintService>()));
        return services;
    }
}
