using System.Diagnostics;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using ThixTunnel.Core.Infrastructure;
using ThixTunnel.Core.Models;

namespace ThixTunnel.Core.Business;

/// <summary>One downloadable file of a version (Windows setup, zipped Mac app).</summary>
public sealed record UpdatePackage(
    [property: JsonPropertyName("file")] string File,
    [property: JsonPropertyName("sha256")] string Sha256);

/// <summary>
/// The manifest CI publishes next to the packages in the rolling release of a channel.
/// <see cref="Setup"/>/<see cref="Sha256"/> are the Windows setup (read by every Windows version since 0.1);
/// <see cref="Packages"/> holds one package per platform ("win-x64", "osx-arm64", "osx-x64").
/// </summary>
public sealed record UpdateManifest(
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("channel")] string Channel,
    [property: JsonPropertyName("buildNumber")] int BuildNumber,
    [property: JsonPropertyName("setup")] string Setup,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("publishedAt")] DateTimeOffset PublishedAt,
    [property: JsonPropertyName("packages")] Dictionary<string, UpdatePackage>? Packages = null);

public sealed record UpdateCheckResult(bool IsUpdateAvailable, string Message, UpdateManifest? Manifest = null);

/// <summary>
/// Over-the-air updates from GitHub releases. Each channel has one rolling release whose files every build
/// replaces: "release-channel" (branch main) and "nightly-channel" (branch dev). A build only looks at its own channel;
/// local builds never update themselves.
///
/// Windows: the package is the new ThixTunnel.exe itself, started with "--update" (InstallService): it ends the running
/// ThixTunnel, replaces the program file and starts ThixTunnel again in the tray.
/// macOS: the package is the zipped ThixTunnel.app; a small script replaces the bundle once ThixTunnel has ended and
/// opens it again in the tray. The user data stays as it is on both.
/// </summary>
public static class UpdateService
{
    public const string ManifestFileName = "thixtunnel-update.json";

    /// <summary>The Windows program started by an update installs itself silently (InstallService).</summary>
    public const string WindowsUpdateArgument = "--update";

    public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

    private static readonly HttpClient Http = CreateHttpClient();

    public static DateTimeOffset? LastCheck { get; private set; }

    public static UpdateCheckResult? LastResult { get; private set; }

    /// <summary>The package name of this platform: win-x64, osx-arm64 or osx-x64.</summary>
    public static string PlatformKey { get; } =
        (OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux") + "-" +
        (RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x64");

    public static string ReleaseTag(string channel) => $"{channel}-channel";

    public static string ManifestUrl(string channel) =>
        $"https://github.com/{AppInfo.Repository}/releases/download/{ReleaseTag(channel)}/{ManifestFileName}";

    public static string ReleasePageUrl(string channel) =>
        $"https://github.com/{AppInfo.Repository}/releases/tag/{ReleaseTag(channel)}";

    /// <summary>A Mac app only updates itself as an app bundle (not when started with "dotnet run").</summary>
    public static bool CanUpdate => !AppInfo.IsLocalBuild && !AccessPolicy.AreUpdatesDisabled &&
                                    (OperatingSystem.IsWindows() || (OperatingSystem.IsMacOS() && MacAppBundle.Current is not null));

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

            if (!IsNewer(manifest, AppInfo.Channel, AppInfo.BuildNumber))
            {
                return Remember(new UpdateCheckResult(false, "ThixTunnel ist aktuell."));
            }

            return Remember(PackageFor(manifest, PlatformKey) is null
                ? new UpdateCheckResult(false, $"Version {manifest.Version} gibt es noch nicht für dieses System ({PlatformKey}).")
                : new UpdateCheckResult(true, $"Version {manifest.Version} ist verfügbar.", manifest));
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

    /// <summary>The package of a platform; Windows falls back to the setup fields of older manifests.</summary>
    public static UpdatePackage? PackageFor(UpdateManifest manifest, string platformKey)
    {
        if (manifest.Packages?.TryGetValue(platformKey, out var package) == true)
        {
            return package;
        }

        return platformKey.StartsWith("win-", StringComparison.Ordinal) && manifest.Setup.Length > 0
            ? new UpdatePackage(manifest.Setup, manifest.Sha256)
            : null;
    }

    /// <summary>
    /// Downloads the new version, checks its SHA-256 and starts its installation. The caller ends the application
    /// right afterwards (Windows: the update would end it anyway; macOS: the bundle is replaced once it has ended).
    /// </summary>
    public static async Task DownloadAndStartInstallerAsync(UpdateManifest manifest, IProgress<double>? progress = null,
        CancellationToken cancellationToken = default, bool showWindowAfter = false)
    {
        AccessPolicy.EnsureUpdatesAllowed();
        var package = PackageFor(manifest, PlatformKey);
        if (package is null || !IsSafeFileName(package.File))
        {
            throw new BusinessException("Die Update-Information ist ungültig.");
        }

        Directory.CreateDirectory(AppPaths.UpdateDirectory);
        var packagePath = Path.Combine(AppPaths.UpdateDirectory, package.File);
        var url = $"https://github.com/{AppInfo.Repository}/releases/download/{ReleaseTag(manifest.Channel)}/{package.File}";

        using (var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? 0;
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var target = File.Create(packagePath);
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

        var hash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(packagePath, cancellationToken)));
        if (!string.Equals(hash, package.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(packagePath);
            throw new BusinessException("Das heruntergeladene Update ist beschädigt (Prüfsumme stimmt nicht).");
        }

        Log.Info($"Starting update to {manifest.Version}");
        // Before the new program starts: on Windows it ends this one at once.
        if (showWindowAfter)
        {
            ShowWindowAfterUpdate();
        }

        if (OperatingSystem.IsMacOS())
        {
            MacAppBundle.StartReplacement(packagePath);
            return;
        }

        Process.Start(new ProcessStartInfo(packagePath, WindowsUpdateArgument)
        {
            UseShellExecute = false,
            WorkingDirectory = AppPaths.UpdateDirectory,
        });
    }

    /// <summary>
    /// The update was asked for in the window (or the window was open): the ThixTunnel the update starts again shows its window instead
    /// of waiting in the tray. A marker in the update folder carries this over the restart (the restart itself always says "--tray").
    /// </summary>
    public static void ShowWindowAfterUpdate()
    {
        Directory.CreateDirectory(AppPaths.UpdateDirectory);
        File.WriteAllText(ShowMarker, DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>True once after an update that should show the window (a marker older than 15 minutes is stale); the marker goes.</summary>
    public static bool TakeShowWindowAfterUpdate()
    {
        if (!File.Exists(ShowMarker))
        {
            return false;
        }

        try
        {
            var written = File.GetLastWriteTimeUtc(ShowMarker);
            File.Delete(ShowMarker);
            return DateTime.UtcNow - written < TimeSpan.FromMinutes(15);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string ShowMarker => Path.Combine(AppPaths.UpdateDirectory, "show-window-after-update");

    internal static bool IsSafeFileName(string fileName) =>
        fileName.Length > 0 &&
        (fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) &&
        fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && !fileName.Contains("..") && !fileName.Contains('/') && !fileName.Contains('\\');

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
