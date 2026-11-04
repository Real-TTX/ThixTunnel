using System.Reflection;

namespace ThixTunnel.Core.Infrastructure;

/// <summary>Version, channel and build number baked in at build time (Directory.Build.props).</summary>
public static class AppInfo
{
    private static readonly Assembly ThisAssembly = typeof(AppInfo).Assembly;

    public const string ProductName = "ThixTunnel";

    /// <summary>e.g. 0.1.42-20261104, nightly-43-20261104 or local-20261104.</summary>
    public static string Version { get; } =
        ThisAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "local";

    /// <summary>release, nightly or local.</summary>
    public static string Channel { get; } = ReadMetadata("ThixTunnelChannel") ?? "local";

    public static int BuildNumber { get; } = int.TryParse(ReadMetadata("ThixTunnelBuildNumber"), out var number) ? number : 0;

    /// <summary>owner/name of the GitHub repository that publishes the builds.</summary>
    public static string Repository { get; } = ReadMetadata("ThixTunnelRepository") ?? "Real-TTX/ThixTunnel";

    public static bool IsLocalBuild => Channel == "local";

    private static string? ReadMetadata(string key) =>
        ThisAssembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(attribute => attribute.Key == key)?.Value;
}
