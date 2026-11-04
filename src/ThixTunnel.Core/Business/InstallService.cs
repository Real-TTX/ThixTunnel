using System.Diagnostics;
using Microsoft.Win32;
using ThixTunnel.Core.Infrastructure;

namespace ThixTunnel.Core.Business;

/// <summary>What the setup decides on a first installation. An update keeps what is installed.</summary>
public sealed record InstallOptions(bool StartWithWindows, bool DesktopShortcut);

/// <summary>
/// ThixTunnel installs itself - like the Matgate agent and the MatMon setup, without a separate installer tool.
/// The published ThixTunnel.exe is the setup: started from anywhere else it copies itself to
/// %LocalAppData%\Programs\ThixTunnel (per user, no administrator rights), creates the Start menu entry, the entry in
/// "Installierte Apps" and optionally the autostart and a desktop shortcut. "--uninstall" removes every one of them.
/// User data (%AppData%\ThixTunnel) stays unless the user explicitly deletes it while uninstalling.
/// </summary>
public static class InstallService
{
    public const string UpdateArgument = "--update";
    public const string UninstallArgument = "--uninstall";
    public const string QuietArgument = "--quiet";
    public const string ExecutableName = "ThixTunnel.exe";

    /// <summary>A running ThixTunnel listens for this signal and ends itself cleanly (tunnels are remembered for the restart).</summary>
    public const string ExitSignalName = "ThixTunnel.Exit";

    private const string DefaultUninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ThixTunnel";

    // The first installer of ThixTunnel 0.1 was an Inno Setup; its entry is replaced by this one.
    private const string LegacyInnoKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{5E1F3C1B-7A41-4C4E-9B7E-2D6A1C0F7B21}_is1";

    private static TestLocations? _testLocations;

    public static string ProgramFolder => _testLocations?.ProgramFolder ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", AppInfo.ProductName);

    public static string InstalledExecutable => Path.Combine(ProgramFolder, ExecutableName);

    public static string StartMenuShortcut => Path.Combine(
        _testLocations?.ShortcutFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.Programs), $"{AppInfo.ProductName}.lnk");

    public static string DesktopShortcut => Path.Combine(
        _testLocations?.DesktopFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), $"{AppInfo.ProductName}.lnk");

    private static string UninstallKeyPath => _testLocations?.UninstallKeyPath ?? DefaultUninstallKeyPath;

    /// <summary>Installed means: the entry in "Installierte Apps" and the program file exist.</summary>
    public static bool IsInstalled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(UninstallKeyPath);
            return key is not null && File.Exists(InstalledExecutable);
        }
    }

    public static string? InstalledVersion
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(UninstallKeyPath);
            return key?.GetValue("DisplayVersion") as string;
        }
    }

    public static bool HasDesktopShortcut => File.Exists(DesktopShortcut);

    public static bool IsInstalledExecutable(string path) =>
        string.Equals(Path.GetFullPath(path), Path.GetFullPath(InstalledExecutable), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Installs or updates. <paramref name="options"/> null = update: shortcuts and autostart stay as they are.
    /// </summary>
    public static void Install(string sourceExecutable, InstallOptions? options, Action<string> log)
    {
        var isUpdate = options is null;
        log(isUpdate ? $"Aktualisiere ThixTunnel in {ProgramFolder}" : $"Installiere ThixTunnel nach {ProgramFolder}");
        StopRunningInstances(log);

        Directory.CreateDirectory(ProgramFolder);
        if (!IsInstalledExecutable(sourceExecutable))
        {
            CopyWithRetry(sourceExecutable, InstalledExecutable);
            log("Programmdatei kopiert");
        }

        CreateShortcut(StartMenuShortcut, "SSH-Tunnel verwalten");
        log("Startmenü-Eintrag angelegt");

        var wantsDesktopShortcut = options?.DesktopShortcut ?? HasDesktopShortcut;
        if (wantsDesktopShortcut)
        {
            CreateShortcut(DesktopShortcut, "SSH-Tunnel verwalten");
            log("Desktop-Verknüpfung angelegt");
        }

        if (_testLocations is null)
        {
            // An update keeps the autostart as the user left it, but points it at the installed file.
            var wantsAutostart = options?.StartWithWindows ?? AutostartService.IsEnabled();
            if (wantsAutostart)
            {
                AutostartService.WriteEntry(InstalledExecutable);
                log("Autostart bei der Windows-Anmeldung eingerichtet");
            }

            RemoveLegacyInnoSetup(log);
        }

        WriteUninstallEntry();
        log("Eintrag unter „Installierte Apps“ geschrieben");
        log(isUpdate ? "Update abgeschlossen" : "Installation abgeschlossen");
    }

    /// <summary>
    /// Removes program, shortcuts, autostart and the entry in "Installierte Apps"; the user data only on request.
    /// Returns true when the program folder could not be deleted yet because this process runs from it -
    /// then call <see cref="ScheduleProgramFolderRemoval"/> right before the process ends.
    /// </summary>
    public static bool Uninstall(bool deleteUserData, Action<string> log)
    {
        log("Entferne ThixTunnel");
        StopRunningInstances(log);

        if (_testLocations is null)
        {
            AutostartService.RemoveEntry();
            log("Autostart entfernt");
        }

        DeleteFile(StartMenuShortcut);
        DeleteFile(DesktopShortcut);
        log("Verknüpfungen entfernt");
        Registry.CurrentUser.DeleteSubKeyTree(UninstallKeyPath, throwOnMissingSubKey: false);
        log("Eintrag unter „Installierte Apps“ entfernt");

        if (deleteUserData)
        {
            DeleteFolder(AppPaths.DataDirectory);
            DeleteFolder(AppPaths.LocalDirectory);
            log("SSH-Profile, Verbindungen, Einstellungen und Protokolle gelöscht");
        }

        var runsFromProgramFolder = (Environment.ProcessPath ?? "").StartsWith(ProgramFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        if (runsFromProgramFolder)
        {
            log("Programmordner wird nach dem Schließen gelöscht");
            return true;
        }

        DeleteFolder(ProgramFolder);
        log("Programmordner gelöscht");
        return false;
    }

    /// <summary>The program folder cannot go while this exe runs from it: a short-lived cmd removes it afterwards.</summary>
    public static void ScheduleProgramFolderRemoval() =>
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c ping -n 4 127.0.0.1 >nul & rmdir /s /q \"{ProgramFolder}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        });

    /// <summary>Starts the installed ThixTunnel (after setup or update).</summary>
    public static void StartInstalled(string arguments = "") =>
        Process.Start(new ProcessStartInfo(InstalledExecutable, arguments) { UseShellExecute = false, WorkingDirectory = ProgramFolder });

    /// <summary>Redirects program folder, shortcuts and the uninstall entry for tests; null = the real places.</summary>
    public static void UseLocationsForTests(string? programFolder, string? shortcutFolder = null, string? uninstallKeyPath = null) =>
        _testLocations = programFolder is null ? null : new TestLocations(programFolder, shortcutFolder ?? programFolder, Path.Combine(shortcutFolder ?? programFolder, "Desktop"), uninstallKeyPath ?? @"Software\ThixTunnelTests\Uninstall");

    /// <summary>Asks a running ThixTunnel from the program folder to end, and ends it when it does not react in time.</summary>
    private static void StopRunningInstances(Action<string> log)
    {
        var running = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ExecutableName))
            .Where(process => process.Id != Environment.ProcessId && RunsFrom(process, InstalledExecutable))
            .ToList();
        if (running.Count == 0)
        {
            return;
        }

        log("Beende das laufende ThixTunnel");
        if (_testLocations is null && EventWaitHandle.TryOpenExisting(ExitSignalName, out var exitSignal))
        {
            exitSignal.Set();
            exitSignal.Dispose();
        }

        foreach (var process in running)
        {
            try
            {
                if (!process.WaitForExit(TimeSpan.FromSeconds(15)))
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(TimeSpan.FromSeconds(10));
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // Already gone.
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private static bool RunsFrom(Process process, string executable)
    {
        try
        {
            return string.Equals(process.MainModule?.FileName, executable, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    /// <summary>The old program may need a moment to release its file after it ended.</summary>
    private static void CopyWithRetry(string source, string target)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Copy(source, target, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < 10)
            {
                Thread.Sleep(500);
            }
        }
    }

    private static void CreateShortcut(string path, string description)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("WScript.Shell is not available.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(path);
        shortcut.TargetPath = InstalledExecutable;
        shortcut.WorkingDirectory = ProgramFolder;
        shortcut.Description = description;
        shortcut.IconLocation = InstalledExecutable + ",0";
        shortcut.Save();
    }

    private static void WriteUninstallEntry()
    {
        using var key = Registry.CurrentUser.CreateSubKey(UninstallKeyPath);
        key.SetValue("DisplayName", AppInfo.ProductName);
        key.SetValue("DisplayVersion", AppInfo.Version);
        key.SetValue("Publisher", "Matthias Schmoldt");
        key.SetValue("DisplayIcon", InstalledExecutable + ",0");
        key.SetValue("InstallLocation", ProgramFolder);
        key.SetValue("UninstallString", $"\"{InstalledExecutable}\" {UninstallArgument}");
        key.SetValue("QuietUninstallString", $"\"{InstalledExecutable}\" {UninstallArgument} {QuietArgument}");
        key.SetValue("URLInfoAbout", $"https://github.com/{AppInfo.Repository}");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.SetValue("EstimatedSize", (int)(new FileInfo(InstalledExecutable).Length / 1024), RegistryValueKind.DWord);
        key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
    }

    /// <summary>Removes what the former Inno Setup left next to the program (its uninstaller and its "Apps" entry).</summary>
    private static void RemoveLegacyInnoSetup(Action<string> log)
    {
        using (var key = Registry.CurrentUser.OpenSubKey(LegacyInnoKeyPath))
        {
            if (key is null)
            {
                return;
            }
        }

        Registry.CurrentUser.DeleteSubKeyTree(LegacyInnoKeyPath, throwOnMissingSubKey: false);
        foreach (var file in Directory.GetFiles(ProgramFolder, "unins*.*"))
        {
            DeleteFile(file);
        }

        log("Alten Inno-Setup-Eintrag ersetzt");
    }

    private static void DeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.Warning($"Could not delete {path}: {exception.Message}");
        }
    }

    private static void DeleteFolder(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.Warning($"Could not delete {path}: {exception.Message}");
        }
    }

    private sealed record TestLocations(string ProgramFolder, string ShortcutFolder, string DesktopFolder, string UninstallKeyPath);
}
