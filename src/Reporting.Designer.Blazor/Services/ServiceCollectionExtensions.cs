using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Reporting.Output;

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

    /// <summary>
    /// Configures where the expression and SQL editors load Monaco from. Not needed for the default: the Designer
    /// ships its own copy of Monaco and uses it when this is never called.
    /// </summary>
    /// <remarks>
    /// <para>Serving a different Monaco build from the host:</para>
    /// <code>
    /// builder.Services.AddOmniReportMonaco(o => o.BaseUrl = "/lib/monaco/vs");
    /// </code>
    /// </remarks>
    /// <exception cref="ArgumentException"><see cref="MonacoOptions.BaseUrl"/> was set to an empty string — leave it
    /// <see langword="null"/> to use the packaged copy.</exception>
    public static IServiceCollection AddOmniReportMonaco(this IServiceCollection services, Action<MonacoOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        var options = new MonacoOptions();
        configure(options);
        if (options.BaseUrl is { } baseUrl && string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new ArgumentException(
                "MonacoOptions.BaseUrl está vazio. Informe a URL da pasta min/vs do Monaco ou deixe null para usar a " +
                "cópia que acompanha o Designer.", nameof(configure));
        }
        services.RemoveAll<MonacoOptions>();
        services.AddSingleton(options);
        return services;
    }
}
