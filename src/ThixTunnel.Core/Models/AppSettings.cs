namespace ThixTunnel.Core.Models;

/// <summary>The setting "Erscheinungsbild" (DESK-02): like the system, light or dark.</summary>
public enum AppearanceChoice
{
    System = 0,
    Light = 1,
    Dark = 2,
}

/// <summary>Global settings of the application (per user).</summary>
public sealed class AppSettings
{
    /// <summary>Start ThixTunnel when the user signs in (Windows: HKCU Run, macOS: LaunchAgent), mirrored here.</summary>
    public bool StartWithWindows { get; set; } = true;

    /// <summary>Show a notification when a tunnel fails.</summary>
    public bool NotifyOnErrors { get; set; } = true;

    /// <summary>Look for updates on start and every few hours.</summary>
    public bool CheckForUpdates { get; set; } = true;

    /// <summary>Install found updates without asking (full auto update).</summary>
    public bool InstallUpdatesAutomatically { get; set; }

    /// <summary>Optional path of ssh; empty = the system OpenSSH (Windows OpenSSH, /usr/bin/ssh).</summary>
    public string SshExecutablePath { get; set; } = "";

    /// <summary>Light, dark or like the system.</summary>
    public AppearanceChoice Appearance { get; set; } = AppearanceChoice.System;

    /// <summary>Size of the main window (remembered, DESK-03).</summary>
    public double WindowWidth { get; set; } = 1100;

    public double WindowHeight { get; set; } = 720;

    /// <summary>The menu of the main window folded to its rail (remembered, DESK-04).</summary>
    public bool IsMenuFolded { get; set; }

    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}
