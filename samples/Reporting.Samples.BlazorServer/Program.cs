using System.Globalization;
using Reporting.Designer.Blazor.DataConnect;
using Reporting.Designer.Blazor.Services;
using Reporting.Samples.BlazorServer.Components;
using Reporting.Viewer.Blazor;

var builder = WebApplication.CreateBuilder(args);

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("pt-BR");

// Register the bundled vector map shapes so the "14 · Mapa" sample resolves ShapeSet("brazil")
// and shows the Brazil basemap in the designer preview.
Reporting.Maps.MapShapes.RegisterBuiltIns();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddOmniReportViewer();

// Designer live-DB mode: registers IDesignerDataConnect + ISecretResolver so the
// data source editor's Test/Schema/Get-fields/Preview/SP-discovery actions can talk
// to real databases (SQLite, PostgreSQL, SQL Server, MySQL).
builder.Services.AddOmniReportDesignerDataConnect();

// Designer printing: registers BrowserPrintService (the universal path that works in
// Server, WebAssembly, MAUI). For Windows desktop hosts that want silent direct-to-spooler
// printing, chain `.WithNativePrinter<WindowsSpoolerPrinter>()`.
builder.Services.AddOmniReportDesignerPrinting();

// Designer exporters: registers PDF, XLSX, HTML+SVG, SVG, CSV, JSON, Markdown as
// IReportExporter instances. The Designer's preview toolbar auto-discovers them via
// IExporterRegistry and renders one button per format. Register fewer or add custom
// formats by replacing this call with manual `services.AddSingleton<IReportExporter, ...>()`
// for the subset you want.
builder.Services.AddDefaultExporters();

// HttpClient for the WebService data source samples. We register a singleton because
// the providers themselves are short-lived (built per report run), and we want to
// reuse the underlying socket pool across requests. Production hosts will likely
// wire IHttpClientFactory + Polly retries; this is the minimum needed for the
// demo .repx files in /wwwroot/reports to function.
builder.Services.AddHttpClient();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

app.Run();
