using Avalonia;
using Avalonia.Controls;

namespace ThixTunnel;

internal static class Program
{
    /// <summary>
    /// One program for everything (App decides): the application, the SSH_ASKPASS dialog and - on Windows - the setup.
    /// The application ends only through "Beenden" (tray / menu bar), not when its last window closes (DESK-06).
    /// </summary>
    [STAThread]
    public static int Main(string[] args)
    {
        // The snapshots draw the windows headless, in a fresh data folder of their own (before anything reads the data
        // folder): no screen, no tray, no single-instance check, no ssh.
        if (args.Contains(Snapshot.Argument))
        {
            Environment.SetEnvironmentVariable(Core.Infrastructure.AppPaths.DataDirectoryVariable,
                Path.Combine(Path.GetTempPath(), "thixtunnel-snapshot", Guid.NewGuid().ToString("N")));
            return Snapshot.Run(args);
        }

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
    }

    /// <summary>Also used by the XAML previewer.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new MacOSPlatformOptions { ShowInDock = true })
            .LogToTrace();
}
