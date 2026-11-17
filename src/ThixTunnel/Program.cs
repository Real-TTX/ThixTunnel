using Avalonia;
using Avalonia.Controls;

namespace ThixTunnel;

internal static class Program
{
    /// <summary>
    /// One program for everything (App decides): the application, the SSH_ASKPASS dialog and - on Windows - the setup.
    /// The application ends only through "Beenden" (tray / menu bar), not when its last window closes.
    /// </summary>
    [STAThread]
    public static int Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);

    /// <summary>Also used by the XAML previewer.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new MacOSPlatformOptions { ShowInDock = true })
            .LogToTrace();
}
