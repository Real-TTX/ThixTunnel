namespace ThixTunnel.Core.Infrastructure;

/// <summary>Finds ssh.exe. Windows OpenSSH first: the ssh of Git for Windows does not understand Windows paths and agents.</summary>
internal static class SshExecutable
{
    public static string? Find(string configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return File.Exists(configuredPath) ? configuredPath : null;
        }

        var windowsOpenSsh = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH", "ssh.exe");
        if (File.Exists(windowsOpenSsh))
        {
            return windowsOpenSsh;
        }

        var searchPath = Environment.GetEnvironmentVariable("PATH") ?? "";
        return searchPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory.Trim(), "ssh.exe"))
            .FirstOrDefault(File.Exists);
    }
}
