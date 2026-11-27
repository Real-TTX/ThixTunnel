using System.Net.NetworkInformation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using MatDesktop;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Infrastructure;
using ThixTunnel.Core.Models;
using ThixTunnel.Dialogs;
using TrayIcon = ThixTunnel.Tray.TrayIcon;

namespace ThixTunnel;

/// <summary>
/// Start-up and end of ThixTunnel.
///  - Started by ssh as SSH_ASKPASS: only the prompt dialog, then exit.
///  - Windows: the published ThixTunnel.exe is also its setup (RunSetupIfRequested).
///  - The look is MatDesktop's theme with ThixTunnel's accent (DESK-02).
///  - One instance per user and data folder (MatSingleInstance, DESK-05): a second start opens the window of the first one.
///  - "--autostart" (sign-in) and "--tray" (after an update) start hidden in the tray / menu bar (DESK-06) - after an update asked for in
///    the window (or with the window open) the window shows again (UpdateService.TakeShowWindowAfterUpdate).
/// </summary>
public partial class App : Application
{
    public const string TrayArgument = "--tray";

    /// <summary>ThixTunnel's accent (IDENT-01): the blue of the logo's portal, deep enough for white text (4.95:1).</summary>
    public const string Accent = "#1A6FD1";

    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private MatSingleInstance? _instance;
    private TrayIcon? _trayIcon;
    private MainWindow? _mainWindow;
    private MatInfoWindow? _infoWindow;
    private bool _isExiting;

    /// <summary>The program file; the autostart entry points to it, ssh starts it as SSH_ASKPASS.</summary>
    public static string ExecutablePath => Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "ThixTunnel");

    public static App Instance => (App)Current!;

    /// <summary>The main window (null during setup and the askpass dialog).</summary>
    public MainWindow? MainWindow => _mainWindow;

    /// <summary>The main window is on the screen (not only in the tray).</summary>
    public bool IsMainWindowShown => _mainWindow is { IsVisible: true };

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // ThixTunnel speaks German, the texts of MatDesktop (title bar, menu, tray, setup) too.
        MatText.Language = "de";
        // the icon of every window (MatWindow.DefaultIcon): the main window, the dialogs, the setup
        MatTheme.Install(this, new MatThemeOptions(Accent) { Icon = Ui.AppIcon });
    }

    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        _desktop = desktop;
        var args = desktop.Args ?? [];
        ApplyAppearance(SettingsService.Get().Appearance);

        if (AskPassProtocol.IsAskPassInvocation(out var routeName))
        {
            AskPassWindow.Run(routeName, string.Join(' ', args), EndProcess);
            return;
        }

        if (RunSetupIfRequested(args))
        {
            return;
        }

        if (!TakeSingleInstance())
        {
            EndProcess(0);
            return;
        }

        Start(args);
    }

    /// <summary>The setting "Erscheinungsbild": Wie das System, Hell, Dunkel (DESK-02).</summary>
    public static void ApplyAppearance(AppearanceChoice appearance) => MatTheme.SetAppearance(appearance switch
    {
        AppearanceChoice.Light => MatAppearance.Light,
        AppearanceChoice.Dark => MatAppearance.Dark,
        _ => MatAppearance.System,
    });

    /// <summary>
    /// Ends the process through the main loop. Shutting down straight from OnFrameworkInitializationCompleted (update,
    /// quiet uninstall, second instance) throws "Dispatcher shut down" because the loop has not started yet.
    /// </summary>
    private void EndProcess(int exitCode) => Dispatcher.UIThread.Post(() => _desktop?.Shutdown(exitCode));

    private void Start(string[] args)
    {
        Dispatcher.UIThread.UnhandledException += OnUnhandledException;
        Log.Info($"ThixTunnel {AppInfo.Version} starting on {UpdateService.PlatformKey} ({string.Join(' ', args)})");

        TunnelManager.AskPassProgram = ExecutablePath;
        _mainWindow = new MainWindow();
        _trayIcon = new TrayIcon(this);

        // After an update asked for in the window (or with the window open) the window comes back, even though the restart says "--tray".
        var startHidden = (args.Contains(TrayArgument) || args.Contains(AutostartService.StartupArgument)) && !UpdateService.TakeShowWindowAfterUpdate();
        if (!startHidden)
        {
            _mainWindow.Show();
        }

        // macOS: a click on the Dock icon (or opening the app again) shows the window.
        if (TryGetFeature(typeof(IActivatableLifetime)) is IActivatableLifetime activatable)
        {
            activatable.Activated += (_, activation) =>
            {
                if (activation.Kind == ActivationKind.Reopen)
                {
                    ShowMainWindow();
                }
            };
        }

        UpdateCoordinator.Checked += (_, result) =>
        {
            _trayIcon?.SetUpdateAvailable(result.IsUpdateAvailable);
            if (result is { IsUpdateAvailable: true, Manifest: { } manifest } && !SettingsService.Get().InstallUpdatesAutomatically)
            {
                _trayIcon?.ShowNotification("Update verfügbar", $"ThixTunnel {manifest.Version} kann installiert werden (Einstellungen › Updates).");
            }
        };
        UpdateCoordinator.Start();

        // After sleep, a network change or a new Wi-Fi the waiting tunnels try again at once.
        NetworkChange.NetworkAvailabilityChanged += (_, availability) =>
        {
            if (availability.IsAvailable)
            {
                TunnelManager.RetryWaitingRoutesNow();
            }
        };
        NetworkChange.NetworkAddressChanged += (_, _) => TunnelManager.RetryWaitingRoutesNow();

        TunnelManager.EndLeftoverProcesses();
        TunnelManager.ConnectStartupRoutes();
        SessionResume.RestoreRememberedRoutes();
    }

    /// <summary>Opens the main window (tray, second start, Dock) and optionally navigates.</summary>
    public void ShowMainWindow(Action<MainWindow>? navigate = null)
    {
        if (_mainWindow is null || _isExiting)
        {
            return;
        }

        navigate?.Invoke(_mainWindow);
        _mainWindow.ShowInFront();
    }

    /// <summary>"Info …" of the tray (TRAY-05): the state as a dot, a word and a sentence, the facts, Open / Log folder / Close.</summary>
    public void ShowInfo()
    {
        if (_isExiting)
        {
            return;
        }

        _infoWindow?.Close();
        _infoWindow = new MatInfoWindow(CreateInfo());
        _infoWindow.Closed += (_, _) => _infoWindow = null;
        _infoWindow.Show();
        _infoWindow.BringToFront();
    }

    /// <summary>What Info shows: the tunnels, ssh, the data folder (also the snapshots).</summary>
    internal MatInfo CreateInfo()
    {
        var statuses = TunnelManager.GetStatuses();
        var routes = RouteService.List();
        var active = routes.Where(route => TunnelManager.GetStatus(route.Id).IsActive).Select(route => route.Name).ToList();
        var failed = routes.Where(route => TunnelManager.GetStatus(route.Id).State == TunnelState.Failed).Select(route => route.Name).ToList();
        var since = statuses.Where(status => status.State == TunnelState.Connected).Select(status => status.Since).DefaultIfEmpty().Min();
        var sentence = active.Count == 0
            ? routes.Count == 0 ? "Noch keine Verbindung eingerichtet." : "Kein Tunnel ist verbunden."
            : since == default ? "Die Tunnel bauen sich auf." : $"Verbunden seit {since.ToLocalTime():HH:mm} Uhr.";
        var configuredSsh = SettingsService.Get().SshExecutablePath;
        List<MatFact> facts =
        [
            new("Verbindungen", $"{routes.Count} eingerichtet, {active.Count} aktiv"),
            new("Aktiv", active.Count == 0 ? "–" : string.Join(", ", active)),
        ];
        if (failed.Count > 0)
        {
            facts.Add(new("Mit Fehler", string.Join(", ", failed)));
        }

        facts.Add(new("Automatik", TunnelManager.AutomationPaused ? "pausiert" : "an"));
        facts.Add(new("ssh", configuredSsh.Length > 0 ? configuredSsh : Ui.SystemSshPath ?? "nicht gefunden", IsMono: true));
        facts.Add(new("Datenordner", AppPaths.DataDirectory, IsMono: true));
        return new MatInfo(AppInfo.ProductName, AppInfo.Version, AppInfo.Channel, TunnelStates.Overall(), sentence, facts)
        {
            Logo = Ui.Logo,
            Open = () => ShowMainWindow(),
            OpenLogFolder = () => Ui.OpenFolder(AppPaths.LogDirectory),
        };
    }

    /// <summary>"Beenden": disconnects every tunnel and ends the application.</summary>
    public void ExitApplication()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        Log.Info("ThixTunnel exiting");
        TunnelManager.DisconnectAll();
        _trayIcon?.Dispose();
        _infoWindow?.Close();
        if (_mainWindow is not null)
        {
            _mainWindow.IsExiting = true;
            _mainWindow.Close();
        }

        _instance?.Dispose();
        _desktop?.Shutdown(0);
    }

    /// <summary>
    /// The command line of SETUP-05 with the install options of SETUP-08 (InstallerOptions; a wrong value: exit code 2,
    /// nothing installed).
    /// Windows: the published ThixTunnel.exe is its own setup (InstallService):
    ///  - "--uninstall" (from "Installierte Apps"): the uninstall assistant, or silently with "--quiet".
    ///  - "--quiet [options]" (ThixLauncher, ThixInstall, an administrator): installs without a window; "--start" starts it in the tray.
    ///  - "--update [options]" (downloaded by the update check): installs itself silently and starts the installed ThixTunnel in the tray.
    ///  - started anywhere else than the program folder (e.g. from Downloads): the setup assistant.
    /// Both systems: "--configure [options]" applies the options to the installed ThixTunnel and ends (macOS: ThixLauncher calls
    /// it after it copied the bundle). A development build (not single-file) runs as the application otherwise.
    /// </summary>
    private bool RunSetupIfRequested(string[] args)
    {
        SetupRequest request;
        try
        {
            request = InstallerOptions.Parse(args);
        }
        catch (BusinessException exception)
        {
            Log.Warning($"Setup arguments refused: {exception.Message}");
            Console.Error.WriteLine(exception.Message);
            EndProcess(2);
            return true;
        }

        if (request.Action == SetupAction.Configure)
        {
            var executable = OperatingSystem.IsWindows() && File.Exists(InstallService.InstalledExecutable) ? InstallService.InstalledExecutable : ExecutablePath;
            InstallerOptions.Apply(request.Choices, executable, Log.Info);
            EndProcess(0);
            return true;
        }

        return OperatingSystem.IsWindows() && RunWindowsSetup(request);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private bool RunWindowsSetup(SetupRequest request)
    {
        if (request.Action == SetupAction.Uninstall)
        {
            if (request.Quiet)
            {
                if (InstallService.Uninstall(deleteUserData: false, Log.Info))
                {
                    InstallService.ScheduleProgramFolderRemoval();
                }

                EndProcess(0);
                return true;
            }

            Setup.ThixTunnelSetup.Run(isUninstall: true, removeProgramFolder =>
            {
                if (removeProgramFolder)
                {
                    InstallService.ScheduleProgramFolderRemoval();
                }

                EndProcess(0);
            });
            return true;
        }

        if (request.Action is SetupAction.Install or SetupAction.Update)
        {
            var exitCode = 0;
            try
            {
                InstallService.Install(ExecutablePath, request.Choices, Log.Info);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException
                                                  or System.Runtime.InteropServices.COMException or System.Security.SecurityException)
            {
                Log.Error(request.Action == SetupAction.Update ? "Update failed" : "Quiet install failed", exception);
                Console.Error.WriteLine(exception.Message);
                exitCode = 1;
            }

            // an update starts ThixTunnel again in the tray (its tunnels reconnect); a quiet install only with "--start"
            if ((request.Action == SetupAction.Update || request.Start) && File.Exists(InstallService.InstalledExecutable))
            {
                InstallService.StartInstalled(TrayArgument);
            }

            EndProcess(exitCode);
            return true;
        }

#if SINGLE_FILE
        const bool isPublishedBuild = true;
#else
        const bool isPublishedBuild = false;
#endif
        if (isPublishedBuild && !InstallService.IsInstalledExecutable(ExecutablePath))
        {
            Setup.ThixTunnelSetup.Run(isUninstall: false, _ => EndProcess(0));
            return true;
        }

        return false;
    }

    /// <summary>
    /// One instance per user and data folder (MatSingleInstance, DESK-05: a named mutex and a pipe, on macOS a lock file
    /// and a socket): a second start hands its arguments to the first one, which shows its window, and ends. A development
    /// run with its own THIXTUNNEL_DATA_DIR does not meet the installed one. On Windows the setup may also ask the running
    /// instance to end (ListenForExitSignal).
    /// </summary>
    private bool TakeSingleInstance()
    {
        var hasOwnDataFolder = Environment.GetEnvironmentVariable(AppPaths.DataDirectoryVariable) is { Length: > 0 };
        _instance = new MatSingleInstance(AppInfo.ProductName, hasOwnDataFolder ? AppPaths.DataDirectory : null);
        _instance.SecondStart += (_, _) => Ui.Post(() => ShowMainWindow());
        if (!_instance.TryClaim(_desktop?.Args ?? []))
        {
            Log.Info("ThixTunnel runs already: its window was asked to show");
            return false;
        }

        if (OperatingSystem.IsWindows() && !hasOwnDataFolder)
        {
            ListenForExitSignal();
        }

        return true;
    }

    /// <summary>The setup asks a running ThixTunnel to end (update, uninstall): remember the active tunnels and end cleanly.</summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private void ListenForExitSignal()
    {
        var exitSignal = new EventWaitHandle(false, EventResetMode.AutoReset, InstallService.ExitSignalName);
        var listener = new Thread(() =>
        {
            exitSignal.WaitOne();
            Ui.Post(() =>
            {
                Log.Info("Setup asked ThixTunnel to end");
                SessionResume.RememberActiveRoutes();
                ExitApplication();
            });
        })
        {
            IsBackground = true,
            Name = "ThixTunnel exit signal listener",
        };
        listener.Start();
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("Unhandled error", e.Exception);
        e.Handled = true;
        if (e.Exception is BusinessException business)
        {
            _trayIcon?.ShowNotification("ThixTunnel", business.Message, isWarning: true);
            return;
        }

        _trayIcon?.ShowNotification("Unerwarteter Fehler", $"{e.Exception.Message} – Details im Protokoll ({AppPaths.LogDirectory}).", isWarning: true);
    }
}
