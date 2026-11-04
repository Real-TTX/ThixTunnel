namespace ThixTunnel.Core.Models;

/// <summary>Global settings of the application (per Windows user).</summary>
public sealed class AppSettings
{
    /// <summary>Start ThixTunnel when the user signs in to Windows (stored in the registry, mirrored here).</summary>
    public bool StartWithWindows { get; set; } = true;

    /// <summary>Start without showing the main window.</summary>
    public bool StartInTray { get; set; } = true;

    /// <summary>The window X only hides the window; the tunnels keep running.</summary>
    public bool KeepRunningInTray { get; set; } = true;

    /// <summary>Show a tray notification when a tunnel fails.</summary>
    public bool NotifyOnErrors { get; set; } = true;

    /// <summary>Look for updates on start and every few hours.</summary>
    public bool CheckForUpdates { get; set; } = true;

    /// <summary>Install found updates without asking (full auto update).</summary>
    public bool InstallUpdatesAutomatically { get; set; }

    /// <summary>Optional path of ssh.exe; empty = Windows OpenSSH.</summary>
    public string SshExecutablePath { get; set; } = "";

    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}
