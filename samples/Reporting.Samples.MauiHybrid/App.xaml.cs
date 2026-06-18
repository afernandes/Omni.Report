namespace Reporting.Samples.MauiHybrid;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage())
        {
            Title = "OmniReport — MAUI Hybrid",
        };
        // On desktop, give the app a reasonable initial footprint.
        if (DeviceInfo.Platform == DevicePlatform.WinUI)
        {
            window.Width = 1400;
            window.Height = 900;
        }
        return window;
    }
}
