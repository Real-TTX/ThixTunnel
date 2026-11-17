using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Infrastructure;
using ThixTunnel.Core.Models;

namespace ThixTunnel;

/// <summary>
/// The main window: the connection list, narrow and tall like a contact list. It opens at the right edge of the
/// screen and never gets larger than the screen. Closing hides it while the tray / menu bar icon keeps running.
/// </summary>
public partial class MainWindow : ThemedWindow
{
    private const double ScreenMargin = 16;

    public MainWindow()
    {
        InitializeComponent();
        VersionText.Text = AppInfo.Version;
        PlaceAtRightEdge();
        TunnelManager.StatusChanged += (_, _) => Ui.Post(RefreshStatus);
        RefreshStatus();
    }

    /// <summary>Set by the application when it really ends ("Beenden"); then closing is not cancelled.</summary>
    public bool IsExiting { get; set; }

    /// <summary>Selects a connection (tray "In der Liste zeigen").</summary>
    public void SelectRoute(Guid routeId) => Connections.Select(routeId);

    public void SetUpdateAvailable(bool available) => UpdateHint.IsVisible = available;

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!IsExiting && SettingsService.Get().KeepRunningInTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        Connections.Dispose();
        if (!IsExiting)
        {
            ((App)Application.Current!).ExitApplication();
        }
    }

    /// <summary>Right edge of the work area, vertically centred; on small screens as high as the screen allows.</summary>
    private void PlaceAtRightEdge()
    {
        var screen = Screens.Primary;
        if (screen is null)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        var workArea = WorkArea();
        Height = Math.Min(Height, workArea.Height - 2 * ScreenMargin);
        Width = Math.Min(Width, workArea.Width - 2 * ScreenMargin);
        MinHeight = Math.Min(MinHeight, Height);
        MinWidth = Math.Min(MinWidth, Width);
        WindowStartupLocation = WindowStartupLocation.Manual;
        var left = workArea.Right - Width - ScreenMargin;
        var top = workArea.Top + (workArea.Height - Height) / 2;
        Position = new PixelPoint((int)(left * screen.Scaling), (int)(top * screen.Scaling));
    }

    private void RefreshStatus()
    {
        var statuses = TunnelManager.GetStatuses();
        var connected = statuses.Count(status => status.State == TunnelState.Connected);
        var failed = statuses.Count(status => status.State == TunnelState.Failed);
        var waiting = statuses.Count(status => status.State is TunnelState.Connecting or TunnelState.WaitingForRetry);

        var text = connected switch
        {
            0 => "Kein Tunnel aktiv",
            1 => "1 Tunnel aktiv",
            _ => $"{connected} Tunnel aktiv",
        };
        if (failed > 0)
        {
            text += failed == 1 ? " · 1 Fehler" : $" · {failed} Fehler";
        }

        if (TunnelManager.AutomationPaused)
        {
            text += " · Automatik pausiert";
        }

        StatusText.Text = text;
        StatusDot.Fill = Ui.Brush(failed > 0 ? "DangerBrush" : waiting > 0 ? "WarningBrush" : connected > 0 ? "SuccessBrush" : "TextMutedBrush");
    }

    private void OnSettingsClick(object? sender, RoutedEventArgs e) => AppWindows.ShowSettings();

    private void OnVersionClick(object? sender, RoutedEventArgs e) => AppWindows.ShowAbout();

    private void OnUpdateHintClick(object? sender, RoutedEventArgs e) => AppWindows.ShowSettings(Pages.SettingsPage.UpdatesTab);
}
