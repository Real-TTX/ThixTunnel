namespace ThixTunnel.Core.Infrastructure;

/// <summary>
/// Finds ssh and its tools. Windows: Windows OpenSSH first (the ssh of Git for Windows does not understand Windows
/// paths and agents). macOS/Linux: the system OpenSSH in /usr/bin.
/// </summary>
internal static class SshExecutable
{
    public static string FileName(string tool) => OperatingSystem.IsWindows() ? tool + ".exe" : tool;

    public static string? Find(string configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return File.Exists(configuredPath) ? configuredPath : null;
        }

        var systemOpenSsh = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH", "ssh.exe")
            : "/usr/bin/ssh";
        if (File.Exists(systemOpenSsh))
        {
            return systemOpenSsh;
        }

        var searchPath = Environment.GetEnvironmentVariable("PATH") ?? "";
        return searchPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory.Trim(), FileName("ssh")))
            .FirstOrDefault(File.Exists);
    }

    /// <summary>A tool of the same OpenSSH installation, e.g. ssh-keygen next to ssh.</summary>
    public static string Sibling(string sshExecutable, string tool) =>
        Path.Combine(Path.GetDirectoryName(sshExecutable) ?? "", FileName(tool));
}
