using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Infrastructure;
using ThixTunnel.Core.Models;
using ThixTunnel.Pages;

namespace ThixTunnel;

/// <summary>The main window: side bar navigation and the page area. Closing hides it while the tray keeps running.</summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        VersionText.Text = AppInfo.Version;
        NativeWindow.ApplyTheme(this);
        TunnelManager.StatusChanged += (_, _) => Dispatcher.BeginInvoke(RefreshStatus);
        RefreshStatus();
        ShowTunnels();
    }

    /// <summary>Set by the application when it really ends (tray "Beenden"); then closing is not cancelled.</summary>
    public bool IsExiting { get; set; }

    private bool _isSelectingNavigation;
    private bool _navigatedByChecked;

    public void ShowTunnels(Guid? selectRouteId = null)
    {
        SelectNavigation(NavTunnels);
        Navigate(new TunnelListPage(this, selectRouteId));
    }

    public void EditRoute(Guid? routeId)
    {
        SelectNavigation(NavTunnels);
        Navigate(new TunnelEditPage(this, routeId));
    }

    public void ShowProfiles(Guid? selectProfileId = null)
    {
        SelectNavigation(NavProfiles);
        Navigate(new ProfileListPage(this, selectProfileId));
    }

    public void EditProfile(Guid? profileId)
    {
        SelectNavigation(NavProfiles);
        Navigate(new ProfileEditPage(this, profileId));
    }

    public void ShowSettings(int tabIndex = 0)
    {
        SelectNavigation(NavSettings);
        Navigate(new SettingsPage(this, tabIndex));
    }

    public void ShowAbout()
    {
        SelectNavigation(null);
        Navigate(new AboutPage(this));
    }

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
        if (!IsExiting)
        {
            ((App)Application.Current).ExitApplication();
        }
    }

    private void Navigate(UserControl page)
    {
        (PageHost.Content as IDisposable)?.Dispose();
        PageHost.Content = page;
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

        var brush = failed > 0 ? "DangerBrush" : waiting > 0 ? "WarningBrush" : connected > 0 ? "SuccessBrush" : "TextMutedBrush";
        StatusDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, brush);
    }

    /// <summary>Marks the side bar entry without triggering its navigation (null = none, e.g. the about page).</summary>
    private void SelectNavigation(RadioButton? entry)
    {
        _isSelectingNavigation = true;
        foreach (var candidate in new[] { NavTunnels, NavProfiles, NavSettings })
        {
            candidate.IsChecked = candidate == entry;
        }

        _isSelectingNavigation = false;
    }

    // Checked covers mouse, keyboard and UI automation; Click covers clicking the entry that is already selected
    // (e.g. "Tunnel" while editing a route). A mouse click raises Checked first, then Click - navigate only once.
    private void OnNavChecked(object sender, RoutedEventArgs e)
    {
        if (_isSelectingNavigation)
        {
            return;
        }

        _navigatedByChecked = true;
        NavigateTo(sender);
    }

    private void OnNavClick(object sender, RoutedEventArgs e)
    {
        if (_navigatedByChecked)
        {
            _navigatedByChecked = false;
            return;
        }

        NavigateTo(sender);
    }

    private void NavigateTo(object entry)
    {
        if (entry == NavTunnels)
        {
            ShowTunnels();
        }
        else if (entry == NavProfiles)
        {
            ShowProfiles();
        }
        else
        {
            ShowSettings();
        }
    }

    private void OnVersionClick(object sender, RoutedEventArgs e) => ShowAbout();

    private void OnUpdateHintClick(object sender, RoutedEventArgs e) => ShowSettings(1);
}
