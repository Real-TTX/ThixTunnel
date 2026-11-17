using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace ThixTunnel;

/// <summary>Small helpers for code-built UI: theme resources by key and the UI thread.</summary>
internal static class Ui
{
    public static object Resource(string key) =>
        Application.Current!.TryGetResource(key, Application.Current.ActualThemeVariant, out var value) && value is not null
            ? value
            : throw new InvalidOperationException($"Resource {key} is missing in Themes/Dark.axaml.");

    public static IBrush Brush(string key) => (IBrush)Resource(key);

    /// <summary>An icon glyph (Icon* keys).</summary>
    public static string Icon(string key) => (string)Resource(key);

    /// <summary>A named look (ControlTheme) such as "PrimaryButton" or "MutedText".</summary>
    public static ControlTheme Theme(string key) => (ControlTheme)Resource(key);

    public static FontFamily IconFont => (FontFamily)Resource("IconFont");

    /// <summary>Runs the action on the UI thread (events of the business layer come from any thread).</summary>
    public static void Post(Action action) => Dispatcher.UIThread.Post(action);

    /// <summary>The window that hosts a control (null while it is not shown).</summary>
    public static Window? WindowOf(Control control) => TopLevel.GetTopLevel(control) as Window;

    /// <summary>The ssh of the system (Windows OpenSSH, /usr/bin/ssh); null when it is not installed.</summary>
    public static string? SystemSshPath
    {
        get
        {
            var path = OperatingSystem.IsWindows()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH", "ssh.exe")
                : "/usr/bin/ssh";
            return File.Exists(path) ? path : null;
        }
    }

    /// <summary>Opens a web address or a folder with the program the system uses for it (browser, Explorer, Finder).</summary>
    public static void OpenExternal(string pathOrUrl)
    {
        try
        {
            var startInfo = OperatingSystem.IsMacOS()
                ? new System.Diagnostics.ProcessStartInfo("open") { ArgumentList = { pathOrUrl }, UseShellExecute = false }
                : OperatingSystem.IsLinux()
                    ? new System.Diagnostics.ProcessStartInfo("xdg-open") { ArgumentList = { pathOrUrl }, UseShellExecute = false }
                    : new System.Diagnostics.ProcessStartInfo(pathOrUrl) { UseShellExecute = true };
            System.Diagnostics.Process.Start(startInfo)?.Dispose();
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Core.Infrastructure.Log.Warning($"Could not open {pathOrUrl}: {exception.Message}");
        }
    }
}
