using Avalonia.Controls;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Models;
using AvaloniaTrayIcon = Avalonia.Controls.TrayIcon;

namespace ThixTunnel.Tray;

/// <summary>
/// The icon in the notification area (Windows) or the menu bar (macOS): status at a glance (dot colour, tool tip),
/// and a menu with Öffnen, every tunnel with connect/disconnect, pausing the automation, settings, and Beenden.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly AvaloniaTrayIcon _trayIcon;
    private readonly NativeMenu _menu = new();
    private readonly App _app;
    private readonly HashSet<Guid> _notifiedFailures = [];
    private bool _updateAvailable;

    public TrayIcon(App app)
    {
        _app = app;
        _trayIcon = new AvaloniaTrayIcon
        {
            Icon = TrayIconRenderer.Get(TrayState.Idle),
            ToolTipText = "ThixTunnel",
            Menu = _menu,
            IsVisible = true,
        };

        // Windows: a click on the icon opens the window, the right button the menu. macOS: every click opens the menu.
        _trayIcon.Clicked += (_, _) => _app.ShowMainWindow();
        _menu.Opening += (_, _) => BuildMenu();
        AvaloniaTrayIcon.SetIcons(app, [_trayIcon]);

        TunnelManager.StatusChanged += OnStatusChanged;
        Refresh();
        BuildMenu();
    }

    public void SetUpdateAvailable(bool available)
    {
        _updateAvailable = available;
        BuildMenu();
    }

    public void ShowNotification(string title, string text, bool isWarning = false) =>
        NotificationToast.Show(title, text, isWarning, () => _app.ShowMainWindow());

    public void Dispose()
    {
        TunnelManager.StatusChanged -= OnStatusChanged;
        _trayIcon.IsVisible = false;
        _trayIcon.Dispose();
    }

    private void OnStatusChanged(object? sender, TunnelStatus status) => Ui.Post(() =>
    {
        Refresh();
        BuildMenu();
        NotifyFailure(status);
    });

    private void Refresh()
    {
        var statuses = TunnelManager.GetStatuses();
        var state = statuses.Any(status => status.State == TunnelState.Failed) ? TrayState.Error
            : statuses.Any(status => status.State is TunnelState.Connecting or TunnelState.WaitingForRetry) ? TrayState.Busy
            : statuses.Any(status => status.State == TunnelState.Connected) ? TrayState.Connected
            : TrayState.Idle;
        _trayIcon.Icon = TrayIconRenderer.Get(state);
        _trayIcon.ToolTipText = Truncate($"ThixTunnel – {Summary(statuses)}", 127);
    }

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

        // One notification per failure, no endless series.
        if (!SettingsService.Get().NotifyOnErrors || !_notifiedFailures.Add(status.RouteId))
        {
            return;
        }

        var name = RouteService.Get(status.RouteId)?.Name ?? "Tunnel";
        ShowNotification($"{name}: Verbindung fehlgeschlagen", status.Message, isWarning: true);
    }

    private static string Summary(IReadOnlyList<TunnelStatus> statuses)
    {
        var connected = statuses.Count(status => status.State == TunnelState.Connected);
        var failed = statuses.Count(status => status.State == TunnelState.Failed);
        var text = connected == 1 ? "1 Tunnel aktiv" : connected == 0 ? "kein Tunnel aktiv" : $"{connected} Tunnel aktiv";
        return failed > 0 ? $"{text}, {failed} mit Fehler" : text;
    }

    private void BuildMenu()
    {
        _menu.Items.Clear();
        var statuses = TunnelManager.GetStatuses();

        _menu.Items.Add(new NativeMenuItem($"ThixTunnel  ·  {Summary(statuses)}") { IsEnabled = false });
        _menu.Items.Add(Item("Öffnen", () => _app.ShowMainWindow()));
        _menu.Items.Add(new NativeMenuItemSeparator());

        var routes = RouteService.List();
        if (routes.Count == 0)
        {
            _menu.Items.Add(new NativeMenuItem("Keine Verbindungen") { IsEnabled = false });
        }

        foreach (var route in routes)
        {
            var status = TunnelManager.GetStatus(route.Id);
            var routeId = route.Id;
            var submenu = new NativeMenu();
            submenu.Items.Add(status.IsActive
                ? Item("Trennen", () => TunnelManager.Disconnect(routeId))
                : Item("Verbinden", () => Connect(routeId)));
            submenu.Items.Add(Item("In der Liste zeigen", () => _app.ShowMainWindow(window => window.SelectRoute(routeId))));
            _menu.Items.Add(new NativeMenuItem($"{StatusSymbol(status)}  {route.Name}   ({Controls.StatusBadge.Label(status)})") { Menu = submenu });
        }

        _menu.Items.Add(new NativeMenuItemSeparator());
        _menu.Items.Add(new NativeMenuItem("Automatik pausieren")
        {
            ToggleType = NativeMenuItemToggleType.CheckBox,
            IsChecked = TunnelManager.AutomationPaused,
            Command = new RelayCommand(() => TunnelManager.SetAutomationPaused(!TunnelManager.AutomationPaused)),
        });
        _menu.Items.Add(Item("Einstellungen", () => AppWindows.ShowSettings()));
        if (_updateAvailable)
        {
            _menu.Items.Add(Item("Update installieren …", () => AppWindows.ShowSettings(Pages.SettingsPage.UpdatesTab)));
        }

        _menu.Items.Add(new NativeMenuItemSeparator());
        _menu.Items.Add(Item("Beenden", () => _app.ExitApplication()));
    }

    private static NativeMenuItem Item(string header, Action action) => new(header) { Command = new RelayCommand(action) };

    private void Connect(Guid routeId)
    {
        try
        {
            TunnelManager.Connect(routeId);
        }
        catch (BusinessException exception)
        {
            ShowNotification("Verbinden nicht möglich", exception.Message, isWarning: true);
        }
    }

    private static string StatusSymbol(TunnelStatus status) => status.State switch
    {
        TunnelState.Connected => "●",
        TunnelState.Failed => "✕",
        TunnelState.Connecting or TunnelState.WaitingForRetry => "◐",
        _ => "○",
    };

    private static string Truncate(string text, int length) => text.Length <= length ? text : text[..(length - 1)] + "…";

    /// <summary>Menu items run their action through a command (works on Windows and in the macOS menu bar).</summary>
    private sealed class RelayCommand(Action action) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Ui.Post(action);
    }
}
