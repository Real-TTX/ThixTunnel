using ThixTunnel.Core.Infrastructure;
using ThixTunnel.Core.Models;

namespace ThixTunnel.Core.Business;

/// <summary>Global settings. "Bei der Anmeldung starten" is read from and written to the system (<see cref="AutostartService"/>).</summary>
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
            throw new BusinessException("Das angegebene ssh-Programm wurde nicht gefunden.", nameof(AppSettings.SshExecutablePath));
        }

        if (!Enum.IsDefined(settings.Appearance))
        {
            throw new BusinessException("Unbekanntes Erscheinungsbild.", nameof(AppSettings.Appearance));
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

    /// <summary>
    /// Remembers the state of the window (size, folded menu). That is no configuration: a locked configuration
    /// (LockConfiguration) does not stop it, and nothing else of the settings changes.
    /// </summary>
    public static void RememberWindow(double width, double height, bool isMenuFolded)
    {
        if (width < 100 || height < 100 || double.IsNaN(width) || double.IsNaN(height))
        {
            return;
        }

        DataStore.Update(data =>
        {
            data.Settings.WindowWidth = Math.Round(width);
            data.Settings.WindowHeight = Math.Round(height);
            data.Settings.IsMenuFolded = isMenuFolded;
        });
    }
}
