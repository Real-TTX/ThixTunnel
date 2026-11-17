using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using MatDesktop;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Infrastructure;

namespace ThixTunnel;

/// <summary>
/// "ThixTunnel --snapshot &lt;folder&gt;": draws the windows of ThixTunnel into PNG files without a screen (Avalonia headless
/// with Skia, the real windows, themes and fonts of the machine), in light and dark - the pictures of docs/images and the
/// README. It works in a temporary data folder (Program sets it) filled with the sample setup (SampleData: three profiles,
/// four connections and their states) and version 1.0.0 on the release channel: no ssh, tray icon, registry value, network
/// or update check is touched.
/// </summary>
internal static class Snapshot
{
    public const string Argument = "--snapshot";

    /// <summary>What every picture shows as version and channel.</summary>
    public const string Version = "1.0.0";

    public const string Channel = "release";

    /// <summary>"Now" in every picture: Thursday, 12 November 2026, 14:32.</summary>
    public static readonly DateTimeOffset Moment = new(new DateTime(2026, 11, 12, 14, 32, 0, DateTimeKind.Local));

    private const string ProgramFolder = @"C:\Users\Anna\AppData\Local\Programs\ThixTunnel";
    private const string DataFolder = @"C:\Users\Anna\AppData\Roaming\ThixTunnel";

    private static readonly string[] Themes = ["light", "dark"];

    private static readonly string[] ThemedPictures =
    [
        "setup-welcome", "setup-options", "setup-progress", "setup-finish", "setup-update", "setup-uninstall",
        "main-connections", "main-folded", "main-route-edit", "main-profile-edit", "main-settings", "main-routes", "main-profiles",
        "main-updates", "main-about", "info", "askpass-password", "askpass-hostkey", "toast",
    ];

    /// <summary>Every file a snapshot writes (the tray icons are the same in both themes).</summary>
    public static IReadOnlyList<string> FileNames { get; } =
    [
        .. ThemedPictures.SelectMany(name => Themes.Select(theme => $"{name}-{theme}.png")),
        "tray-icon-windows.png", "tray-icon-macos.png",
    ];

    /// <summary>The command: sets Avalonia up headless, draws every picture into the folder, ends with 0 (all written) or 1.</summary>
    public static int Run(string[] args)
    {
        var index = Array.IndexOf(args, Argument);
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"Usage: ThixTunnel {Argument} <folder>");
            return 2;
        }

        var folder = Path.GetFullPath(args[index + 1]);
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();
        AvaloniaSynchronizationContext.InstallIfNeeded();

        int exitCode;
        try
        {
            var report = Render(folder);
            foreach (var file in report.Written)
            {
                Console.WriteLine($"written  {Path.Combine(folder, file)}");
            }

            foreach (var (file, reason) in report.Skipped)
            {
                Console.WriteLine($"skipped  {file}: {reason}");
            }

            exitCode = report.Written.Count + report.Skipped.Count == FileNames.Count ? 0 : 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Snapshot failed: {exception}");
            exitCode = 1;
        }

        // The temporary data folder goes again.
        try
        {
            Directory.Delete(AppPaths.DataDirectory, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }

        return exitCode;
    }

    /// <summary>
    /// Draws every picture into <paramref name="folder"/> (Avalonia must run headless, the data folder must be a fresh
    /// one: the command and the UI tests take care of both). Existing files of the same names are replaced.
    /// </summary>
    public static SnapshotReport Render(string folder)
    {
        Directory.CreateDirectory(folder);
        var report = new SnapshotReport();
        using (Isolate())
        {
            SampleData.Create();
            TunnelManager.ShowStatusesForTests(SampleData.Statuses(Moment));
            foreach (var theme in Themes)
            {
                MatTheme.SetAppearance(theme == "dark" ? MatAppearance.Dark : MatAppearance.Light);
                Application.Current!.RequestedThemeVariant = theme == "dark" ? ThemeVariant.Dark : ThemeVariant.Light;
                RenderSetup(folder, theme, report);
                RenderMainWindow(folder, theme, report);
                RenderDialogs(folder, theme, report);
            }

            RenderTrayIcons(folder, report);
            TunnelManager.ShowStatusesForTests(null);
        }

        MatTheme.SetAppearance(MatAppearance.System);
        return report;
    }

    private static void RenderSetup(string folder, string theme, SnapshotReport report)
    {
        foreach (var page in Enum.GetValues<SetupSnapshotPage>())
        {
            var name = $"setup-{page.ToString().ToLowerInvariant()}-{theme}.png";
            if (!OperatingSystem.IsWindows())
            {
                report.Skipped.Add((name, "the setup assistant exists on Windows only (macOS: the app bundle and the dmg)"));
                continue;
            }

            RenderSetupPage(folder, name, page, report);
        }
    }

    /// <summary>
    /// One page of the setup with fixed values (MatSetupWindow, ThixTunnelSetup.Create): nothing is installed, removed or
    /// asked from the system. Progress shows an installation two thirds along, Finish the end of one that worked.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void RenderSetupPage(string folder, string file, SetupSnapshotPage page, SnapshotReport report)
    {
        var installed = page == SetupSnapshotPage.Update ? "0.9.0" : Version;
        var hold = new TaskCompletionSource();
        var window = Setup.ThixTunnelSetup.Create(page == SetupSnapshotPage.Uninstall, page == SetupSnapshotPage.Update, installed, ProgramFolder, hasSsh: true, async run =>
        {
            if (page != SetupSnapshotPage.Progress)
            {
                return;
            }

            run.Progress.Report(new MatSetupReport($"Installiere ThixTunnel nach {ProgramFolder}", MatSetupStepState.Done));
            run.Progress.Report(new MatSetupReport("Programmdatei kopiert", MatSetupStepState.Done));
            run.Progress.Report(new MatSetupReport("Startmenü-Eintrag angelegt", MatSetupStepState.Done));
            run.Progress.Report(new MatSetupReport("Autostart bei der Windows-Anmeldung eingerichtet", MatSetupStepState.Running, 65));
            await hold.Task;
        });
        window.Show();
        Settle(window);
        if (page == SetupSnapshotPage.Options)
        {
            window.GoTo(MatSetupStepKind.Options);
        }
        else if (page is SetupSnapshotPage.Progress or SetupSnapshotPage.Finish)
        {
            _ = window.RunAsync();
        }

        Settle(window);
        Save(window, folder, file, report);
        hold.TrySetResult();
        Settle(window);
        window.Close();
    }

    private static void RenderMainWindow(string folder, string theme, SnapshotReport report)
    {
        var window = new MainWindow { Width = 1100, Height = 720 };
        window.Show();
        window.ShowConnections(SampleData.PostgresRouteId);
        Settle(window, waitLonger: true);
        Save(window, folder, $"main-connections-{theme}.png", report);

        window.Width = 860;
        window.FindControl<MatNav>("Sidebar")!.IsFolded = true;
        Settle(window, waitLonger: true);
        Save(window, folder, $"main-folded-{theme}.png", report);
        window.FindControl<MatNav>("Sidebar")!.IsFolded = false;
        window.Width = 1100;

        window.ShowSettings(Pages.SettingsPage.RoutesTab);
        window.OpenPage(new Pages.TunnelEditPage(window, SampleData.PostgresRouteId));
        Settle(window, waitLonger: true);
        Save(window, folder, $"main-route-edit-{theme}.png", report);
        window.GoBack();

        window.ShowSettings(Pages.SettingsPage.ProfilesTab);
        var profile = new Pages.ProfileEditPage(window, SampleData.BuildServerId);
        window.OpenPage(profile);
        profile.Layout.Tabs.SelectedIndex = 1;
        Settle(window, waitLonger: true);
        Save(window, folder, $"main-profile-edit-{theme}.png", report);
        window.GoBack();

        window.ShowSettings(Pages.SettingsPage.GeneralTab);
        Settle(window);
        Save(window, folder, $"main-settings-{theme}.png", report);
        window.ShowSettings(Pages.SettingsPage.RoutesTab);
        Settle(window);
        Save(window, folder, $"main-routes-{theme}.png", report);
        window.ShowSettings(Pages.SettingsPage.ProfilesTab);
        Settle(window);
        Save(window, folder, $"main-profiles-{theme}.png", report);
        window.ShowSettings(Pages.SettingsPage.UpdatesTab);
        Settle(window);
        Save(window, folder, $"main-updates-{theme}.png", report);

        // The about page tells this machine's system and folders: the pictures show a typical installation instead.
        window.ShowAbout();
        var about = (Pages.AboutPage)window.CurrentPage!;
        about.SystemText.Text = OperatingSystem.IsMacOS()
            ? $"macOS 15.5 ({UpdateService.PlatformKey}), .NET 10.0.0"
            : $"Microsoft Windows 10.0.26100 ({UpdateService.PlatformKey}), .NET 10.0.0";
        about.SshText.Text = Ui.Breakable(OperatingSystem.IsMacOS() ? "/usr/bin/ssh" : @"C:\Windows\System32\OpenSSH\ssh.exe");
        about.DataText.Text = Ui.Breakable(OperatingSystem.IsMacOS() ? "/Users/anna/Library/Application Support/ThixTunnel" : DataFolder);
        Settle(window);
        Save(window, folder, $"main-about-{theme}.png", report);

        window.IsExiting = true;
        window.Close();
    }

    private static void RenderDialogs(string folder, string theme, SnapshotReport report)
    {
        var info = App.Instance.CreateInfo();
        var facts = info.Facts
            .Select(fact => fact.Label switch
            {
                "ssh" => fact with { Value = OperatingSystem.IsMacOS() ? "/usr/bin/ssh" : @"C:\Windows\System32\OpenSSH\ssh.exe" },
                "Datenordner" => fact with { Value = OperatingSystem.IsMacOS() ? "/Users/anna/Library/Application Support/ThixTunnel" : DataFolder },
                _ => fact,
            })
            .ToList();
        var infoWindow = new MatInfoWindow(info with { Facts = facts, Sentence = "Verbunden seit 13:50 Uhr." });
        infoWindow.Show();
        Settle(infoWindow, waitLonger: true);
        Save(infoWindow, folder, $"info-{theme}.png", report);
        infoWindow.Close();

        var password = new Dialogs.AskPassWindow("Jenkins", "anna@build.example.com's password:");
        password.Show();
        Settle(password);
        Save(password, folder, $"askpass-password-{theme}.png", report);
        password.Close();

        var hostKey = new Dialogs.AskPassWindow("Postgres Produktion",
            "The authenticity of host 'gateway.example.com (203.0.113.10)' can't be established.\n" +
            "ED25519 key fingerprint is SHA256:p2QAMXNIC1TJYWeIOttrVc98/R1BUFWu3/LiyKgUfQM.\n" +
            "This key is not known by any other names.\nAre you sure you want to continue connecting (yes/no/[fingerprint])?");
        hostKey.Show();
        Settle(hostKey);
        Save(hostKey, folder, $"askpass-hostkey-{theme}.png", report);
        hostKey.Close();

        var toast = MatNotification.Show("Jenkins: Verbindung fehlgeschlagen", "Anmeldung fehlgeschlagen – Passwort beim manuellen Verbinden eingeben.", isWarning: true);
        Settle(toast);
        Save(toast, folder, $"toast-{theme}.png", report);
        toast.Close();
        Settle(toast);
    }

    /// <summary>
    /// The tray / menu bar icons at 64 px, as MatDesktop draws them (TRAY-02): the mark with the green dot of connected
    /// tunnels, on macOS the template image.
    /// </summary>
    private static void RenderTrayIcons(string folder, SnapshotReport report)
    {
        foreach (var (template, platform) in new[] { (false, "windows"), (true, "macos") })
        {
            var name = $"tray-icon-{platform}.png";
            using var mark = Tray.TrayIcon.Mark(template);
            File.WriteAllBytes(Path.Combine(folder, name), MatTrayIcons.Png(mark, MatState.Running, template));
            report.Written.Add(name);
        }
    }

    /// <summary>Lets the dispatcher, the layout and the render timer run; longer for the background image decoding.</summary>
    private static void Settle(TopLevel topLevel, bool waitLonger = false)
    {
        for (var round = 0; round < (waitLonger ? 30 : 8); round++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Thread.Sleep(waitLonger ? 30 : 15);
        }

        Dispatcher.UIThread.RunJobs();
        topLevel.UpdateLayout();
    }

    private static void Save(TopLevel topLevel, string folder, string name, SnapshotReport report)
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        var frame = topLevel.CaptureRenderedFrame() ?? throw new InvalidOperationException($"{name}: no frame was rendered");
        frame.Save(Path.Combine(folder, name));
        report.Written.Add(name);
    }

    /// <summary>Pins version and channel and keeps policies and autostart away from the machine (SandboxScope).</summary>
    private static IDisposable Isolate()
    {
        AppInfo.Pin(Version, Channel);
        var sandbox = SandboxScope.Begin();
        return new Restore(() =>
        {
            sandbox.Dispose();
            AppInfo.Pin(null);
        });
    }

    private sealed class Restore(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }
}

/// <summary>What a snapshot wrote (file names) and what it left out, with the reason.</summary>
internal sealed class SnapshotReport
{
    public List<string> Written { get; } = [];

    public List<(string File, string Reason)> Skipped { get; } = [];
}

/// <summary>The pages of the setup that "ThixTunnel --snapshot" draws (setup-welcome ... setup-uninstall).</summary>
internal enum SetupSnapshotPage
{
    Welcome,
    Options,
    Progress,
    Finish,
    Update,
    Uninstall,
}
