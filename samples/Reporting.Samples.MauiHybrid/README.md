# Reporting.Samples.MauiHybrid

MAUI Blazor Hybrid sample que hospeda o `ReportDesigner` e o `ReportViewer` da OmniReport
em um único binário, rodando como app **Windows desktop** ou **Android nativo**.

## Setup

Requer o workload **MAUI** instalado:

```powershell
dotnet workload install maui maui-windows
# Para Android (opcional):
dotnet workload install android
```

## Rodar no Windows

```powershell
dotnet build -f net10.0-windows10.0.19041.0
dotnet run -f net10.0-windows10.0.19041.0 --project samples\Reporting.Samples.MauiHybrid
```

A app abre uma janela WinUI 3 (1400×900) com o `BlazorWebView` hospedando os componentes.

## Rodar no Android

O target Android **fica condicionalmente ativo** apenas quando a variável
`OMNIREPORT_BUILD_ANDROID=true` está definida — evita falhar o build em CI Linux/Windows
quando o workload Android não está presente ou as licenças do SDK não foram aceitas.

```powershell
$env:OMNIREPORT_BUILD_ANDROID = 'true'
dotnet workload install android
# Aceitar licenças do Android SDK:
dotnet android sdk install --accept-licenses

dotnet build -f net10.0-android samples\Reporting.Samples.MauiHybrid
# Depois deploy ao emulador / device via Android Studio ou:
dotnet build -f net10.0-android -t:Run samples\Reporting.Samples.MauiHybrid
```

## Driver de impressão por plataforma

`MauiProgram.cs` registra o `IReportPrinter` apropriado via DI:

| Plataforma | Driver |
|---|---|
| Windows  | `WindowsSpoolerPrinter` (System.Drawing.Printing → spooler nativo) |
| Android  | `AndroidPrintFrameworkPrinter` (PrintManager → fluxo de impressão do sistema) |
| iOS / macCatalyst | não registrado nesta versão |

O Designer / Viewer descobrem o driver via DI e o botão "Imprimir" usa o caminho nativo
da plataforma.

## Estrutura

```
Reporting.Samples.MauiHybrid/
├── MauiProgram.cs            — DI + UseMauiApp + per-platform printer registration
├── App.xaml(.cs)             — MAUI Application; cria Window com MainPage
├── MainPage.xaml(.cs)        — ContentPage com BlazorWebView root=Main
├── Main.razor                — Router → Layout → Pages
├── Layout/MainLayout.razor   — sidebar dark + nav (Home / Designer / Viewer)
├── Pages/
│   ├── Home.razor            — landing
│   ├── DesignerPage.razor    — <ReportDesigner />
│   └── ViewerPage.razor      — <ReportViewer /> com 4 samples
├── wwwroot/
│   ├── index.html            — HTML host page (carrega tokens.css + reporting-viewer.js)
│   └── app.css               — chrome do host
└── Resources/
    ├── AppIcon/appicon.svg   — ícone (R sobre fundo ferrugem)
    └── Splash/splash.svg     — splash screen
```
