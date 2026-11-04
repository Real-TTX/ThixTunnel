using System.Drawing;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Infrastructure;
using ThixTunnel.Core.Models;
using Forms = System.Windows.Forms;

namespace ThixTunnel.Tray;

/// <summary>
/// The icon in the notification area: status at a glance (dot colour, tooltip), and a menu with
/// Öffnen, every tunnel with connect/disconnect, pausing the automation, settings, and Beenden.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _menu;
    private readonly App _app;
    private readonly HashSet<Guid> _notifiedFailures = [];
    private bool _updateAvailable;

    public TrayIcon(App app)
    {
        _app = app;
        _menu = new Forms.ContextMenuStrip { Renderer = new DarkMenuRenderer(), ShowImageMargin = false, Font = new Font("Segoe UI", 9.5f) };
        _menu.Opening += (_, _) => BuildMenu();

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = TrayIconRenderer.Get(TrayState.Idle),
            Text = "ThixTunnel",
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _notifyIcon.MouseClick += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Left)
            {
                _app.ShowMainWindow();
            }
        };
        _notifyIcon.BalloonTipClicked += (_, _) => _app.ShowMainWindow();

        TunnelManager.StatusChanged += OnStatusChanged;
        Refresh();
    }

    public void SetUpdateAvailable(bool available) => _updateAvailable = available;

    public void ShowNotification(string title, string text, Forms.ToolTipIcon icon = Forms.ToolTipIcon.Info) =>
        _notifyIcon.ShowBalloonTip(5000, title, text, icon);

    public void Dispose()
    {
        TunnelManager.StatusChanged -= OnStatusChanged;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
    }

    private void OnStatusChanged(object? sender, TunnelStatus status) => _app.Dispatcher.BeginInvoke(() =>
    {
        Refresh();
        NotifyFailure(status);
    });

    private void Refresh()
    {
        var statuses = TunnelManager.GetStatuses();
        var state = statuses.Any(status => status.State == TunnelState.Failed) ? TrayState.Error
            : statuses.Any(status => status.State is TunnelState.Connecting or TunnelState.WaitingForRetry) ? TrayState.Busy
            : statuses.Any(status => status.State == TunnelState.Connected) ? TrayState.Connected
            : TrayState.Idle;
        _notifyIcon.Icon = TrayIconRenderer.Get(state);
        _notifyIcon.Text = Truncate($"ThixTunnel – {Summary(statuses)}", 127);
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
        ShowNotification($"{name}: Verbindung fehlgeschlagen", status.Message, Forms.ToolTipIcon.Warning);
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

        var header = new Forms.ToolStripLabel($"ThixTunnel  ·  {Summary(statuses)}") { Font = new Font(_menu.Font, FontStyle.Bold), Margin = new Forms.Padding(4, 6, 4, 6) };
        _menu.Items.Add(header);
        _menu.Items.Add("Öffnen", null, (_, _) => _app.ShowMainWindow());
        _menu.Items.Add(new Forms.ToolStripSeparator());

        var routes = RouteService.List();
        if (routes.Count == 0)
        {
            _menu.Items.Add(new Forms.ToolStripMenuItem("Keine Tunnelrouten") { Enabled = false });
        }

        foreach (var route in routes)
        {
            var status = TunnelManager.GetStatus(route.Id);
            var item = new Forms.ToolStripMenuItem($"{StatusSymbol(status)}  {route.Name}   ({Controls.StatusBadge.Label(status)})")
            {
                ToolTipText = $"{route.LocalEndpoint} → {route.TargetEndpoint}\n{status.Message}",
            };
            var routeId = route.Id;
            if (status.IsActive)
            {
                item.DropDownItems.Add("Trennen", null, (_, _) => TunnelManager.Disconnect(routeId));
            }
            else
            {
                item.DropDownItems.Add("Verbinden", null, (_, _) => Connect(routeId));
            }

            item.DropDownItems.Add("Details", null, (_, _) => _app.ShowMainWindow(window => window.ShowTunnels(routeId)));
            item.DropDown.Renderer = _menu.Renderer;
            _menu.Items.Add(item);
        }

        _menu.Items.Add(new Forms.ToolStripSeparator());
        var pause = new Forms.ToolStripMenuItem("Automatik pausieren") { Checked = TunnelManager.AutomationPaused, CheckOnClick = false };
        pause.Text = TunnelManager.AutomationPaused ? "✓  Automatik pausiert" : "Automatik pausieren";
        pause.Click += (_, _) => TunnelManager.SetAutomationPaused(!TunnelManager.AutomationPaused);
        _menu.Items.Add(pause);
        _menu.Items.Add("Einstellungen", null, (_, _) => _app.ShowMainWindow(window => window.ShowSettings()));
        if (_updateAvailable)
        {
            _menu.Items.Add("Update installieren …", null, (_, _) => _app.ShowMainWindow(window => window.ShowSettings(1)));
        }

        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add("Beenden", null, (_, _) => _app.ExitApplication());
    }

    private void Connect(Guid routeId)
    {
        try
        {
            TunnelManager.Connect(routeId);
        }
        catch (BusinessException exception)
        {
            ShowNotification("Verbinden nicht möglich", exception.Message, Forms.ToolTipIcon.Warning);
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

    /// <summary>Dark menu colours matching the window.</summary>
    private sealed class DarkMenuRenderer() : Forms.ToolStripProfessionalRenderer(new DarkColors())
    {
        protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Color.FromArgb(0xE8, 0xEE, 0xF7) : Color.FromArgb(0x9A, 0xAC, 0xC4);
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(Forms.ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Color.FromArgb(0x9A, 0xAC, 0xC4);
            base.OnRenderArrow(e);
        }
    }

    private sealed class DarkColors : Forms.ProfessionalColorTable
    {
        private static readonly Color Background = Color.FromArgb(0x15, 0x22, 0x38);
        private static readonly Color Hover = Color.FromArgb(0x1E, 0x34, 0x56);
        private static readonly Color Border = Color.FromArgb(0x2A, 0x3B, 0x55);

        public override Color ToolStripDropDownBackground => Background;
        public override Color ImageMarginGradientBegin => Background;
        public override Color ImageMarginGradientMiddle => Background;
        public override Color ImageMarginGradientEnd => Background;
        public override Color MenuBorder => Border;
        public override Color MenuItemBorder => Hover;
        public override Color MenuItemSelected => Hover;
        public override Color MenuItemSelectedGradientBegin => Hover;
        public override Color MenuItemSelectedGradientEnd => Hover;
        public override Color MenuItemPressedGradientBegin => Hover;
        public override Color MenuItemPressedGradientEnd => Hover;
        public override Color SeparatorDark => Border;
        public override Color SeparatorLight => Border;
        public override Color CheckBackground => Hover;
        public override Color CheckSelectedBackground => Hover;
    }
}
