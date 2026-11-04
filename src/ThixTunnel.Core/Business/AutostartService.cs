using Microsoft.Win32;

namespace ThixTunnel.Core.Business;

/// <summary>
/// Start at Windows sign-in for the current user: a value in HKCU\...\Run. The installer writes the same value,
/// the uninstaller removes it. The registry is the truth; the settings page shows what it finds there.
/// </summary>
public static class AutostartService
{
    public const string StartupArgument = "--autostart";

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ThixTunnel";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(ValueName) is string command && command.Length > 0;
    }

    public static void Enable(string executablePath)
    {
        AccessPolicy.EnsureConfigurationEditable();

        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        key.SetValue(ValueName, $"\"{executablePath}\" {StartupArgument}");
    }

    public static void Disable()
    {
        AccessPolicy.EnsureConfigurationEditable();

        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
