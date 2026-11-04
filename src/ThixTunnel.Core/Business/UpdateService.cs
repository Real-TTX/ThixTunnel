using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using ThixTunnel.Core.Infrastructure;
using ThixTunnel.Core.Models;

namespace ThixTunnel.Core.Business;

/// <summary>The manifest CI publishes next to the setup in the rolling release of a channel.</summary>
public sealed record UpdateManifest(
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("channel")] string Channel,
    [property: JsonPropertyName("buildNumber")] int BuildNumber,
    [property: JsonPropertyName("setup")] string Setup,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("publishedAt")] DateTimeOffset PublishedAt);

public sealed record UpdateCheckResult(bool IsUpdateAvailable, string Message, UpdateManifest? Manifest = null);

/// <summary>
/// Over-the-air updates from GitHub releases. Each channel has one rolling release whose files every build
/// replaces: "release-channel" (branch main) and "nightly-channel" (branch dev). A build only looks at its own channel;
/// local builds never update themselves.
///
/// The update is the new ThixTunnel.exe itself, started with "--update" (InstallService): it ends the running ThixTunnel,
/// replaces the program file (the user data in %AppData% stays as it is) and starts ThixTunnel again in the tray.
/// </summary>
public static class UpdateService
{
    public const string ManifestFileName = "thixtunnel-update.json";

    public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

    private static readonly HttpClient Http = CreateHttpClient();

    public static DateTimeOffset? LastCheck { get; private set; }

    public static UpdateCheckResult? LastResult { get; private set; }

    public static string ReleaseTag(string channel) => $"{channel}-channel";

    public static string ManifestUrl(string channel) =>
        $"https://github.com/{AppInfo.Repository}/releases/download/{ReleaseTag(channel)}/{ManifestFileName}";

    public static string ReleasePageUrl(string channel) =>
        $"https://github.com/{AppInfo.Repository}/releases/tag/{ReleaseTag(channel)}";

    public static bool CanUpdate => !AppInfo.IsLocalBuild && !AccessPolicy.AreUpdatesDisabled;

    public static async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        AccessPolicy.EnsureUpdatesAllowed();
        if (AppInfo.IsLocalBuild)
        {
            return Remember(new UpdateCheckResult(false, "Lokaler Build – keine automatischen Updates."));
        }

        try
        {
            var manifest = await Http.GetFromJsonAsync<UpdateManifest>(ManifestUrl(AppInfo.Channel), cancellationToken);
            if (manifest is null)
            {
                return Remember(new UpdateCheckResult(false, "Update-Information ist leer."));
            }

            return Remember(IsNewer(manifest, AppInfo.Channel, AppInfo.BuildNumber)
                ? new UpdateCheckResult(true, $"Version {manifest.Version} ist verfügbar.", manifest)
                : new UpdateCheckResult(false, "ThixTunnel ist aktuell."));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            Log.Warning($"Update check failed: {exception.Message}");
            return Remember(new UpdateCheckResult(false, "Update-Prüfung fehlgeschlagen – keine Verbindung zu GitHub?"));
        }
    }

    /// <summary>Same channel and a higher build number (build numbers only grow).</summary>
    public static bool IsNewer(UpdateManifest manifest, string channel, int buildNumber) =>
        string.Equals(manifest.Channel, channel, StringComparison.OrdinalIgnoreCase) && manifest.BuildNumber > buildNumber;

    /// <summary>
    /// Downloads the new version, checks its SHA-256 and starts it with "--update". The caller ends the application
    /// afterwards (the update would end it anyway).
    /// </summary>
    public static async Task DownloadAndStartInstallerAsync(UpdateManifest manifest, IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        AccessPolicy.EnsureUpdatesAllowed();
        if (!IsSafeFileName(manifest.Setup))
        {
            throw new BusinessException("Die Update-Information ist ungültig.");
        }

        Directory.CreateDirectory(AppPaths.UpdateDirectory);
        var setupPath = Path.Combine(AppPaths.UpdateDirectory, manifest.Setup);
        var url = $"https://github.com/{AppInfo.Repository}/releases/download/{ReleaseTag(manifest.Channel)}/{manifest.Setup}";

        using (var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? 0;
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var target = File.Create(setupPath);
            var buffer = new byte[81920];
            long received = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                received += read;
                if (total > 0)
                {
                    progress?.Report((double)received / total);
                }
            }
        }

        var hash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(setupPath, cancellationToken)));
        if (!string.Equals(hash, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(setupPath);
            throw new BusinessException("Das heruntergeladene Update ist beschädigt (Prüfsumme stimmt nicht).");
        }

        Log.Info($"Starting update to {manifest.Version}");
        Process.Start(new ProcessStartInfo(setupPath, InstallService.UpdateArgument)
        {
            UseShellExecute = false,
            WorkingDirectory = AppPaths.UpdateDirectory,
        });
    }

    private static bool IsSafeFileName(string fileName) =>
        fileName.Length > 0 && fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
        fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && !fileName.Contains("..");

    private static UpdateCheckResult Remember(UpdateCheckResult result)
    {
        LastCheck = DateTimeOffset.Now;
        LastResult = result;
        return result;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"ThixTunnel/{AppInfo.BuildNumber}");
        return client;
    }
}
