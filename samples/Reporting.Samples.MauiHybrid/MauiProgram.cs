using System.Globalization;
using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.Logging;
using Reporting.Designer.Blazor.DataConnect;
using Reporting.Printing;
using Reporting.Viewer.Blazor;

namespace Reporting.Samples.MauiHybrid;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // Brazilian Portuguese throughout the host app — drives currency / date formatting.
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("pt-BR");

        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        // Custom fonts can be added via .ConfigureFonts(fonts => fonts.AddFont(...)).
        // The Blazor host page already loads IBM Plex from the Designer.Blazor token CSS.

        builder.Services.AddMauiBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        // OmniReport viewer defaults.
        builder.Services.AddOmniReportViewer();

        // Designer live-DB mode: enables Test connection / Schema explorer / Get fields /
        // Preview / Stored-procedure signature discovery inside the data source editor.
        // Connection strings can use {secret:NAME} placeholders — resolved from env vars
        // by default. Swap ISecretResolver before this call to plug Azure Key Vault, Vault,
        // AWS Secrets Manager, etc.
        builder.Services.AddOmniReportDesignerDataConnect();

        // Register the platform-appropriate IReportPrinter. The Designer / Viewer pick it
        // up automatically via DI when the user hits "Imprimir".
#if WINDOWS
        builder.Services.AddSingleton<IReportPrinter,
            Reporting.Printing.WindowsSpooler.WindowsSpoolerPrinter>();
#elif ANDROID
        builder.Services.AddSingleton<IReportPrinter>(sp =>
            new Reporting.Printing.Android.AndroidPrintFrameworkPrinter(
                global::Android.App.Application.Context));
#endif

        return builder.Build();
    }
}
