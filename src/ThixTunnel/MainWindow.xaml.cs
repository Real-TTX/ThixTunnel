using System.ComponentModel;
using System.Windows;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Infrastructure;
using ThixTunnel.Core.Models;

namespace ThixTunnel;

/// <summary>
/// The main window: the connection list, narrow and tall like a contact list. It opens at the right edge of the
/// screen and never gets larger than the screen. Closing hides it while the tray keeps running.
/// </summary>
public partial class MainWindow : Window
{
    private const double ScreenMargin = 16;

    public MainWindow()
    {
        InitializeComponent();
        VersionText.Text = AppInfo.Version;
        NativeWindow.ApplyTheme(this);
        PlaceAtRightEdge();
        TunnelManager.StatusChanged += (_, _) => Dispatcher.BeginInvoke(RefreshStatus);
        RefreshStatus();
    }

    /// <summary>Set by the application when it really ends (tray "Beenden"); then closing is not cancelled.</summary>
    public bool IsExiting { get; set; }

    /// <summary>Selects a connection (tray "Details").</summary>
    public void SelectRoute(Guid routeId) => Connections.Select(routeId);

    /// <summary>Brings the window to the front (tray, second program start).</summary>
    public void BringToFront()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    public void SetUpdateAvailable(bool available) =>
        UpdateHint.Visibility = available ? Visibility.Visible : Visibility.Collapsed;

    protected override void OnClosing(CancelEventArgs e)
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
            ((App)Application.Current).ExitApplication();
        }
    }

    /// <summary>Right edge of the work area, vertically centred; on small screens as high as the screen allows.</summary>
    private void PlaceAtRightEdge()
    {
        var workArea = SystemParameters.WorkArea;
        Height = Math.Min(Height, workArea.Height - 2 * ScreenMargin);
        Width = Math.Min(Width, workArea.Width - 2 * ScreenMargin);
        MinHeight = Math.Min(MinHeight, Height);
        MinWidth = Math.Min(MinWidth, Width);
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = workArea.Right - Width - ScreenMargin;
        Top = workArea.Top + (workArea.Height - Height) / 2;
    }

    private void RefreshStatus()
    {
        var statuses = TunnelManager.GetStatuses();
        var connected = statuses.Count(status => status.State == TunnelState.Connected);
        var failed = statuses.Count(status => status.State == TunnelState.Failed);
        var waiting = statuses.Count(status => status.State is TunnelState.Connecting or TunnelState.WaitingForRetry);

        StatusText.Text = connected switch
        {
            0 => "Kein Tunnel aktiv",
            1 => "1 Tunnel aktiv",
            _ => $"{connected} Tunnel aktiv",
        };
        if (failed > 0)
        {
            StatusText.Text += failed == 1 ? " · 1 Fehler" : $" · {failed} Fehler";
        }

        if (TunnelManager.AutomationPaused)
        {
            StatusText.Text += " · Automatik pausiert";
        }

        var brush = failed > 0 ? "DangerBrush" : waiting > 0 ? "WarningBrush" : connected > 0 ? "SuccessBrush" : "TextMutedBrush";
        StatusDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, brush);
    }


    private void OnSettingsClick(object sender, RoutedEventArgs e) => AppWindows.ShowSettings();

    private void OnVersionClick(object sender, RoutedEventArgs e) => AppWindows.ShowAbout();

    private void OnUpdateHintClick(object sender, RoutedEventArgs e) => AppWindows.ShowSettings(Pages.SettingsPage.UpdatesTab);
}
