using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Infrastructure;
using ThixTunnel.Dialogs;
using ThixTunnel.Tray;

namespace ThixTunnel;

/// <summary>
/// Start-up and end of ThixTunnel.
///  - Started by ssh as SSH_ASKPASS: only the prompt dialog, then exit.
///  - One instance per user: a second start opens the window of the first one.
///  - "--autostart" (Windows sign-in) and "--tray" (after an update) may start hidden in the tray.
/// </summary>
public partial class App : Application
{
    public const string TrayArgument = "--tray";

    private const string InstanceName = "ThixTunnel.SingleInstance";
    private const string ShowWindowSignalName = "ThixTunnel.ShowWindow";

    private Mutex? _instanceMutex;
    private EventWaitHandle? _showWindowSignal;
    private TrayIcon? _trayIcon;
    private MainWindow? _mainWindow;
    private bool _isExiting;

    /// <summary>The program file; the autostart entry points to it.</summary>
    public static string ExecutablePath => Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "ThixTunnel.exe");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (AskPassProtocol.IsAskPassInvocation(out var routeName))
        {
            var exitCode = AskPassWindow.Run(routeName, string.Join(' ', e.Args));
            Shutdown(exitCode);
            return;
        }

        if (!TakeSingleInstance())
        {
            Shutdown(0);
            return;
        }

        DispatcherUnhandledException += OnUnhandledException;
        Log.Info($"ThixTunnel {AppInfo.Version} starting ({string.Join(' ', e.Args)})");

        TunnelManager.AskPassProgram = ExecutablePath;
        _trayIcon = new TrayIcon(this);
        _mainWindow = new MainWindow();

        var settings = SettingsService.Get();
        var startHidden = e.Args.Contains(TrayArgument) || (e.Args.Contains(AutostartService.StartupArgument) && settings.StartInTray);
        if (!startHidden)
        {
            _mainWindow.Show();
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

        NetworkChange.NetworkAvailabilityChanged += (_, args) =>
        {
            if (args.IsAvailable)
            {
                TunnelManager.RetryWaitingRoutesNow();
            }
        };
        NetworkChange.NetworkAddressChanged += (_, _) => TunnelManager.RetryWaitingRoutesNow();
        SystemEvents.PowerModeChanged += (_, args) =>
        {
            if (args.Mode == PowerModes.Resume)
            {
                TunnelManager.RetryWaitingRoutesNow();
            }
        };

        TunnelManager.ConnectStartupRoutes();
        SessionResume.RestoreRememberedRoutes();
    }

    /// <summary>Opens the main window (tray, second start) and optionally navigates.</summary>
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
        if (_mainWindow is not null)
        {
            _mainWindow.IsExiting = true;
            _mainWindow.Close();
        }

        Shutdown(0);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        TunnelManager.DisconnectAll();
        _trayIcon?.Dispose();
        _showWindowSignal?.Dispose();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }

    private bool TakeSingleInstance()
    {
        _instanceMutex = new Mutex(initiallyOwned: true, InstanceName, out var isFirst);
        if (!isFirst)
        {
            // Ask the running instance to show its window.
            if (EventWaitHandle.TryOpenExisting(ShowWindowSignalName, out var signal))
            {
                signal.Set();
                signal.Dispose();
            }

            return false;
        }

        _showWindowSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowWindowSignalName);
        var listener = new Thread(() =>
        {
            while (_showWindowSignal.WaitOne())
            {
                Dispatcher.BeginInvoke(() => ShowMainWindow());
            }
        })
        {
            IsBackground = true,
            Name = "ThixTunnel second start listener",
        };
        listener.Start();
        return true;
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("Unhandled error", e.Exception);
        if (e.Exception is Core.Models.BusinessException business)
        {
            _trayIcon?.ShowNotification("ThixTunnel", business.Message);
            e.Handled = true;
            return;
        }

        MessageBox.Show($"Unerwarteter Fehler: {e.Exception.Message}\n\nDetails stehen im Protokoll ({AppPaths.LogDirectory}).",
            "ThixTunnel", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
