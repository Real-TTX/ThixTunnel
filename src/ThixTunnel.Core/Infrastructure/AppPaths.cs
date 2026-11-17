namespace ThixTunnel.Core.Infrastructure;

/// <summary>
/// Where ThixTunnel keeps its files. Program files and user data are separate: updates and uninstalling never touch
/// the user data unless the user decides so.
///   Windows: %AppData%\ThixTunnel (data) and %LocalAppData%\ThixTunnel (ssh configs, updates, logs)
///   macOS:   ~/Library/Application Support/ThixTunnel (both)
/// </summary>
public static class AppPaths
{
    /// <summary>Developers can point ThixTunnel to a separate folder (sample data, screenshots) with this variable.</summary>
    public const string DataDirectoryVariable = "THIXTUNNEL_DATA_DIR";

    private static string? _dataDirectoryOverride = NullIfEmpty(Environment.GetEnvironmentVariable(DataDirectoryVariable));

    /// <summary>Roaming user data: profiles, routes, settings.</summary>
    public static string DataDirectory => _dataDirectoryOverride ?? Path.Combine(UserFolder(Environment.SpecialFolder.ApplicationData), "ThixTunnel");

    public static string DataFile => Path.Combine(DataDirectory, "thixtunnel.json");

    /// <summary>Machine-local working files: generated ssh configs, downloaded updates, logs.</summary>
    public static string LocalDirectory =>
        _dataDirectoryOverride ?? Path.Combine(UserFolder(Environment.SpecialFolder.LocalApplicationData), "ThixTunnel");

    public static string SshConfigDirectory => Path.Combine(LocalDirectory, "ssh");

    public static string UpdateDirectory => Path.Combine(LocalDirectory, "updates");

    public static string LogDirectory => Path.Combine(LocalDirectory, "logs");

    /// <summary>Tests use a temporary folder.</summary>
    internal static void UseDirectoryForTests(string? directory) => _dataDirectoryOverride = directory;

    /// <summary>On macOS both are ~/Library/Application Support, where Mac apps keep their data.</summary>
    private static string UserFolder(Environment.SpecialFolder windowsFolder) => OperatingSystem.IsMacOS()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support")
        : Environment.GetFolderPath(windowsFolder);

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
