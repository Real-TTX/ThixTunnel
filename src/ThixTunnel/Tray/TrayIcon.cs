using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MatDesktop;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Infrastructure;
using ThixTunnel.Core.Models;

namespace ThixTunnel.Tray;

/// <summary>
/// The icon in the notification area (Windows) or the menu bar (macOS), with MatDesktop's tray (TRAY-01 … TRAY-05): the menu in
/// the order of every Mat tray – "ThixTunnel 1.0.0", the state in words, "ThixTunnel öffnen", ThixTunnel's own items (every
/// connection with Verbinden / Trennen, then Automatik pausieren / fortsetzen), "Info …", "Einstellungen …", "Log-Ordner öffnen",
/// "Update installieren …" while one waits, "Beenden". The dot of the icon is the state of the tunnels (TRAY-02): green
/// connected, amber connecting or paused, red an error. Windows: a left click opens the window, the right button the menu;
/// macOS: every click opens the menu (TRAY-03).
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly App _app;
    private readonly MatTrayMenu _menu;
    private readonly MatTray _tray;
    private readonly HashSet<Guid> _notifiedFailures = [];
    private bool _updateAvailable;
    private bool _refreshPosted;

    public TrayIcon(App app)
    {
        _app = app;
        _menu = new MatTrayMenu(AppInfo.ProductName, AppInfo.Version, () => _app.ExitApplication())
        {
            Open = () => _app.ShowMainWindow(),
            Info = () => _app.ShowInfo(),
            Settings = () => _app.ShowMainWindow(window => window.ShowSettings()),
            LogFolder = () => Ui.OpenFolder(AppPaths.LogDirectory),
        };
        Update();
        _tray = new MatTray(app, _menu, Mark());
        _tray.MenuOpening += (_, _) => Update();
        TunnelManager.StatusChanged += OnStatusChanged;
        AppEvents.DataChanged += OnDataChanged;
    }

    public MatTrayMenu Menu => _menu;

    /// <summary>The mark the tray icons are drawn from: the small logo on Windows, the template image (black on transparent) on macOS.</summary>
    public static Bitmap Mark(bool? template = null) =>
        new(AssetLoader.Open(new Uri((template ?? OperatingSystem.IsMacOS()) ? "avares://ThixTunnel/Assets/tray-macos.png" : "avares://ThixTunnel/Assets/tray-windows.png")));

    public void SetUpdateAvailable(bool available)
    {
        _updateAvailable = available;
        Refresh();
    }

    /// <summary>A notification of ThixTunnel's own (TRAY-04: only for what the user did not cause); a click opens the window.</summary>
    public void ShowNotification(string title, string text, bool isWarning = false) =>
        MatNotification.Show(title, text, isWarning, () => _app.ShowMainWindow());

    public void Dispose()
    {
        TunnelManager.StatusChanged -= OnStatusChanged;
        AppEvents.DataChanged -= OnDataChanged;
        _tray.Dispose();
    }

    private void OnDataChanged(object? sender, EventArgs e) => PostRefresh();

    private void OnStatusChanged(object? sender, TunnelStatus status)
    {
        PostRefresh();
        Ui.Post(() => NotifyFailure(status));
    }

    /// <summary>Several state changes at once draw the menu and the icon once.</summary>
    private void PostRefresh()
    {
        if (_refreshPosted)
        {
            return;
        }

        _refreshPosted = true;
        Ui.Post(() =>
        {
            _refreshPosted = false;
            Refresh();
        });
    }

    private void Refresh()
    {
        Update();
        _tray.Refresh();
    }

    private void Update()
    {
        _menu.State = TunnelStates.Overall();
        _menu.TooltipText = TunnelStates.Summary();
        _menu.InstallUpdate = _updateAvailable ? () => _app.ShowMainWindow(window => window.ShowSettings(Pages.SettingsPage.UpdatesTab)) : null;
        _menu.ClearOwn();
        var routes = RouteService.List();
        foreach (var route in routes)
        {
            var status = TunnelManager.GetStatus(route.Id);
            var routeId = route.Id;
            _menu.AddOwn(status.IsActive
                    ? $"{route.Name} trennen  ({TunnelStates.Word(status)})"
                    : $"{route.Name} verbinden" + (status.State == TunnelState.Failed ? "  (Fehler)" : ""),
                () => Toggle(routeId));
        }

        if (routes.Count > 0)
        {
            _menu.AddOwnSeparator();
        }

        _menu.AddOwn(TunnelManager.AutomationPaused ? "Automatik fortsetzen" : "Automatik pausieren",
            () => TunnelManager.SetAutomationPaused(!TunnelManager.AutomationPaused));
    }

    private void Toggle(Guid routeId)
    {
        try
        {
            if (TunnelManager.GetStatus(routeId).IsActive)
            {
                TunnelManager.Disconnect(routeId);
            }
            else
            {
                TunnelManager.Connect(routeId);
            }
        }
        catch (BusinessException exception)
        {
            // The user asked for it, so no notification (TRAY-04): the window shows the reason at the connection.
            _app.ShowMainWindow(window =>
            {
                window.ShowConnections(routeId);
                window.ConnectionsPage.Message.Show(exception.Message, Controls.InfoSeverity.Error);
            });
        }
    }

    /// <summary>One notification per failure that the user did not cause (an automatic or repeated attempt), no endless series.</summary>
    private void NotifyFailure(TunnelStatus status)
    {
        if (status.State != TunnelState.Failed)
        {
            if (status.State == TunnelState.Connected)
            {
                _notifiedFailures.Remove(status.RouteId);
            }

            return;
        }

        if (!SettingsService.Get().NotifyOnErrors || !_notifiedFailures.Add(status.RouteId))
        {
            return;
        }

        var name = RouteService.Get(status.RouteId)?.Name ?? "Tunnel";
        ShowNotification($"{name}: Verbindung fehlgeschlagen", status.Message, isWarning: true);
    }
}
