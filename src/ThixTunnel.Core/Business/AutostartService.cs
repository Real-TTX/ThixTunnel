using System.Runtime.Versioning;
using System.Security;
using Microsoft.Win32;

namespace ThixTunnel.Core.Business;

/// <summary>
/// Start at sign-in for the current user. The entry itself is the truth; the settings page shows what it finds.
///   Windows: a value in HKCU\...\Run (the setup writes the same value, uninstalling removes it).
///   macOS:   a LaunchAgent ~/Library/LaunchAgents/info.schmoldt.thixtunnel.plist (RunAtLoad).
/// </summary>
public static class AutostartService
{
    public const string StartupArgument = "--autostart";

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ThixTunnel";
    private const string LaunchAgentLabel = "info.schmoldt.thixtunnel";

    private static string? _launchAgentFolderForTests;

    /// <summary>The LaunchAgent file (macOS).</summary>
    public static string LaunchAgentFile => Path.Combine(
        _launchAgentFolderForTests ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents"),
        LaunchAgentLabel + ".plist");

    public static bool IsSupported => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();

    public static bool IsEnabled()
    {
        if (OperatingSystem.IsWindows())
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string command && command.Length > 0;
        }

        return OperatingSystem.IsMacOS() && File.Exists(LaunchAgentFile);
    }

    public static void Enable(string executablePath)
    {
        AccessPolicy.EnsureConfigurationEditable();
        WriteEntry(executablePath);
    }

    public static void Disable()
    {
        AccessPolicy.EnsureConfigurationEditable();
        RemoveEntry();
    }

    /// <summary>For the setup: installing and uninstalling the user's own program is not a configuration change.</summary>
    internal static void WriteEntry(string executablePath)
    {
        if (OperatingSystem.IsWindows())
        {
            WriteRunValue(executablePath);
        }
        else if (OperatingSystem.IsMacOS())
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LaunchAgentFile)!);
            File.WriteAllText(LaunchAgentFile, LaunchAgent(executablePath));
        }
    }

    internal static void RemoveEntry()
    {
        if (OperatingSystem.IsWindows())
        {
            RemoveRunValue();
        }
        else if (File.Exists(LaunchAgentFile))
        {
            File.Delete(LaunchAgentFile);
        }
    }

    /// <summary>launchd starts the program at sign-in; it does not restart it when the user ends it (no KeepAlive).</summary>
    internal static string LaunchAgent(string executablePath) => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
        <plist version="1.0">
        <dict>
          <key>Label</key>
          <string>{LaunchAgentLabel}</string>
          <key>ProgramArguments</key>
          <array>
            <string>{SecurityElement.Escape(executablePath)}</string>
            <string>{StartupArgument}</string>
          </array>
          <key>RunAtLoad</key>
          <true/>
          <key>ProcessType</key>
          <string>Interactive</string>
        </dict>
        </plist>

        """;

    internal static void UseLaunchAgentFolderForTests(string? folder) => _launchAgentFolderForTests = folder;

    [SupportedOSPlatform("windows")]
    private static void WriteRunValue(string executablePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        key.SetValue(ValueName, $"\"{executablePath}\" {StartupArgument}");
    }

    [SupportedOSPlatform("windows")]
    private static void RemoveRunValue()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
