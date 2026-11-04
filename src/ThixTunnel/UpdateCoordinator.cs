using System.Windows;
using System.Windows.Threading;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Infrastructure;

namespace ThixTunnel;

/// <summary>
/// When to look for updates and what to do with one: check shortly after start and every 6 hours (when enabled),
/// install on its own with "full auto update", otherwise only point to it (side bar, tray).
/// </summary>
internal static class UpdateCoordinator
{
    private static readonly DispatcherTimer Timer = new() { Interval = UpdateService.CheckInterval };

    /// <summary>Raised on the UI thread with the newest result.</summary>
    public static event EventHandler<UpdateCheckResult>? Checked;

    public static void Start()
    {
        if (!UpdateService.CanUpdate)
        {
            return;
        }

        Timer.Tick += async (_, _) => await CheckInBackgroundAsync();
        Timer.Start();

        // The first check waits a bit: network and tunnels come first after sign-in.
        var firstCheck = new DispatcherTimer { Interval = TimeSpan.FromSeconds(45) };
        firstCheck.Tick += async (_, _) =>
        {
            firstCheck.Stop();
            await CheckInBackgroundAsync();
        };
        firstCheck.Start();
    }

    public static async Task<UpdateCheckResult> CheckAsync()
    {
        var result = await UpdateService.CheckAsync();
        Checked?.Invoke(null, result);
        return result;
    }

    /// <summary>Downloads and starts the setup, then ends ThixTunnel; the setup starts it again.</summary>
    public static async Task InstallAsync(UpdateManifest manifest, IProgress<double>? progress = null)
    {
        await UpdateService.DownloadAndStartInstallerAsync(manifest, progress);
        SessionResume.RememberActiveRoutes();
        ((App)Application.Current).ExitApplication();
    }

    private static async Task CheckInBackgroundAsync()
    {
        var settings = SettingsService.Get();
        if (!settings.CheckForUpdates)
        {
            return;
        }

        try
        {
            var result = await CheckAsync();
            if (result is { IsUpdateAvailable: true, Manifest: { } manifest } && settings.InstallUpdatesAutomatically)
            {
                Log.Info($"Full auto update: installing {manifest.Version}");
                await InstallAsync(manifest);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or Core.Models.BusinessException)
        {
            Log.Warning($"Automatic update failed: {exception.Message}");
        }
    }
}
