using ThixTunnel.Core.Infrastructure;
using ThixTunnel.Core.Models;

namespace ThixTunnel.Core.Business;

/// <summary>Global settings. "Start with Windows" is read from and written to the registry (<see cref="AutostartService"/>).</summary>
public static class SettingsService
{
    public static event EventHandler? SettingsChanged;

    public static AppSettings Get()
    {
        var settings = DataStore.Read().Settings;
        settings.StartWithWindows = AutostartService.IsEnabled();
        return settings;
    }

    /// <param name="executablePath">The program that the autostart entry starts.</param>
    public static AppSettings Save(AppSettings input, string executablePath)
    {
        AccessPolicy.EnsureConfigurationEditable();

        var settings = input.Clone();
        settings.SshExecutablePath = (settings.SshExecutablePath ?? "").Trim();
        if (settings.SshExecutablePath.Length > 0 && !File.Exists(settings.SshExecutablePath))
        {
            throw new BusinessException("Die angegebene ssh.exe wurde nicht gefunden.", nameof(AppSettings.SshExecutablePath));
        }

        if (!settings.CheckForUpdates)
        {
            settings.InstallUpdatesAutomatically = false;
        }

        if (settings.StartWithWindows)
        {
            AutostartService.Enable(executablePath);
        }
        else
        {
            AutostartService.Disable();
        }

        DataStore.Update(data => data.Settings = settings);
        SettingsChanged?.Invoke(null, EventArgs.Empty);
        return settings;
    }
}
