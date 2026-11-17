using System.IO.Pipes;
using System.Net.NetworkInformation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Infrastructure;
using ThixTunnel.Dialogs;
using TrayIcon = ThixTunnel.Tray.TrayIcon;

namespace ThixTunnel;

/// <summary>
/// Start-up and end of ThixTunnel.
///  - Started by ssh as SSH_ASKPASS: only the prompt dialog, then exit.
///  - Windows: the published ThixTunnel.exe is also its setup (RunSetupIfRequested).
///  - One instance per user and data folder: a second start opens the window of the first one.
///  - "--autostart" (sign-in) and "--tray" (after an update) may start hidden in the tray / menu bar.
/// </summary>
public partial class App : Application
{
    public const string TrayArgument = "--tray";

    // One instance per data folder: a development run with its own THIXTUNNEL_DATA_DIR does not meet the installed one.
    private static readonly string InstanceSuffix = Environment.GetEnvironmentVariable(AppPaths.DataDirectoryVariable) is { Length: > 0 }
        ? "." + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(AppPaths.DataDirectory.ToUpperInvariant())))[..16]
        : "";

    private static readonly string InstanceName = "ThixTunnel.SingleInstance" + InstanceSuffix;
    private static readonly string ShowWindowPipeName = "ThixTunnel.ShowWindow" + InstanceSuffix + "." + Environment.UserName;

    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private Mutex? _instanceMutex;
    private TrayIcon? _trayIcon;
    private MainWindow? _mainWindow;
    private bool _isExiting;

    /// <summary>The program file; the autostart entry points to it, ssh starts it as SSH_ASKPASS.</summary>
    public static string ExecutablePath => Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "ThixTunnel");

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        _desktop = desktop;
        var args = desktop.Args ?? [];

        if (AskPassProtocol.IsAskPassInvocation(out var routeName))
        {
            AskPassWindow.Run(routeName, string.Join(' ', args), EndProcess);
            return;
        }

        if (OperatingSystem.IsWindows() && RunSetupIfRequested(args))
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
        _trayIcon = new TrayIcon(this);
        _mainWindow = new MainWindow();

        var settings = SettingsService.Get();
        var startHidden = args.Contains(TrayArgument) || (args.Contains(AutostartService.StartupArgument) && settings.StartInTray);
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
            _mainWindow?.SetUpdateAvailable(result.IsUpdateAvailable);
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
        _mainWindow.BringToFront();
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
        AppWindows.CloseAll();
        if (_mainWindow is not null)
        {
            _mainWindow.IsExiting = true;
            _mainWindow.Close();
        }

        _instanceMutex?.Dispose();
        _desktop?.Shutdown(0);
    }

    /// <summary>
    /// Windows: the published ThixTunnel.exe is its own setup (InstallService):
    ///  - "--uninstall" (from "Installierte Apps"): the uninstall assistant, or silently with "--quiet".
    ///  - "--update" (downloaded by the update check): installs itself silently and starts the installed ThixTunnel in the tray.
    ///  - started anywhere else than the program folder (e.g. from Downloads): the setup assistant.
    /// A development build (not single-file) always runs as the application.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private bool RunSetupIfRequested(string[] args)
    {
        if (args.Contains(InstallService.UninstallArgument))
        {
            if (args.Contains(InstallService.QuietArgument))
            {
                if (InstallService.Uninstall(deleteUserData: false, Log.Info))
                {
                    InstallService.ScheduleProgramFolderRemoval();
                }

                EndProcess(0);
                return true;
            }

            Setup.SetupWindow.Run(isUninstall: true, removeProgramFolder =>
            {
                if (removeProgramFolder)
                {
                    InstallService.ScheduleProgramFolderRemoval();
                }

                EndProcess(0);
            });
            return true;
        }

        if (args.Contains(InstallService.UpdateArgument))
        {
            try
            {
                InstallService.Install(ExecutablePath, options: null, Log.Info);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException
                                                  or System.Runtime.InteropServices.COMException)
            {
                Log.Error("Update failed", exception);
            }

            if (File.Exists(InstallService.InstalledExecutable))
            {
                InstallService.StartInstalled(TrayArgument);
            }

            EndProcess(0);
            return true;
        }

#if SINGLE_FILE
        const bool isPublishedBuild = true;
#else
        const bool isPublishedBuild = false;
#endif
        if (isPublishedBuild && !InstallService.IsInstalledExecutable(ExecutablePath))
        {
            Setup.SetupWindow.Run(isUninstall: false, _ => EndProcess(0));
            return true;
        }

        return false;
    }

    /// <summary>
    /// The first instance owns the mutex and listens on a pipe; a second start knocks on the pipe (the first one shows
    /// its window) and ends. On Windows the setup may also ask the running instance to end (ListenForExitSignal).
    /// </summary>
    private bool TakeSingleInstance()
    {
        _instanceMutex = new Mutex(initiallyOwned: true, InstanceName, out var isFirst);
        if (!isFirst)
        {
            KnockOnRunningInstance();
            return false;
        }

        var listener = new Thread(ListenForSecondStarts) { IsBackground = true, Name = "ThixTunnel second start listener" };
        listener.Start();
        if (OperatingSystem.IsWindows())
        {
            ListenForExitSignal();
        }

        return true;
    }

    private static void KnockOnRunningInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", ShowWindowPipeName, PipeDirection.Out);
            client.Connect(2000);
            client.WriteByte(1);
        }
        catch (Exception exception) when (exception is IOException or TimeoutException or UnauthorizedAccessException)
        {
            Log.Warning($"Could not reach the running instance: {exception.Message}");
        }
    }

    private void ListenForSecondStarts()
    {
        while (true)
        {
            try
            {
                using var server = new NamedPipeServerStream(ShowWindowPipeName, PipeDirection.In, 1);
                server.WaitForConnection();
                server.ReadByte();
                Ui.Post(() => ShowMainWindow());
            }
            catch (IOException exception)
            {
                Log.Warning($"Second start listener: {exception.Message}");
                Thread.Sleep(1000);
            }
        }
    }

    /// <summary>The setup asks a running ThixTunnel to end (update, uninstall): remember the active tunnels and end cleanly.</summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private void ListenForExitSignal()
    {
        if (InstanceSuffix.Length > 0)
        {
            return; // a development run with its own data folder is none of the setup's business
        }

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
        if (e.Exception is Core.Models.BusinessException business)
        {
            _trayIcon?.ShowNotification("ThixTunnel", business.Message, isWarning: true);
            return;
        }

        _trayIcon?.ShowNotification("Unerwarteter Fehler", $"{e.Exception.Message} – Details im Protokoll ({AppPaths.LogDirectory}).", isWarning: true);
    }
}
