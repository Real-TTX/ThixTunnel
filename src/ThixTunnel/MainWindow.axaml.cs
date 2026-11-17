using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MatDesktop;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Infrastructure;
using ThixTunnel.Pages;

namespace ThixTunnel;

/// <summary>
/// The main window: the connections (connect / disconnect), the settings (where connections and profiles are managed) and
/// About, as pages of one window. Edit pages open inside it with a back stack; Esc goes back one page, then hides the window.
///   Strg/⌘+1 Verbindungen · Strg/⌘+, Einstellungen · Strg/⌘+B fold the menu · Strg/⌘+F the search of the list.
/// </summary>
public partial class MainWindow : MatWindow
{
    private readonly DispatcherTimer _sizeTimer = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private readonly MatNavItem _navConnections;
    private readonly MatNavItem _navSettings;
    private readonly Lazy<TunnelListPage> _connections;
    private readonly Lazy<SettingsPage> _settings;
    private readonly Lazy<AboutPage> _about = new(() => new AboutPage());
    private readonly Stack<Control> _back = new();

    public MainWindow()
    {
        InitializeComponent();
        UseOwnTitleBar();
        _connections = new(() => new TunnelListPage(this));
        _settings = new(() => new SettingsPage(this));
        var settings = SettingsService.Get();
        Width = Math.Max(MinWidth, settings.WindowWidth);
        Height = Math.Max(MinHeight, settings.WindowHeight);
        FitIntoScreen();

        // The X (and Esc, Alt+F4) only hides: the tunnels keep running, the tray / menu bar icon stays (DESK-06).
        TitleBar.Logo = Ui.Logo;
        TitleBar.CloseRequested += (_, e) =>
        {
            e.Handled = true;
            Hide();
        };

        var command = Ui.CommandKey.Name;
        _navConnections = new MatNavItem("route", "Verbindungen", $"{command}+1") { IsCurrent = true };
        _navSettings = new MatNavItem("settings", "Einstellungen", $"{command}+,");
        Sidebar.Items.Add(_navConnections);
        Sidebar.Settings = _navSettings;
        Sidebar.Version = AppInfo.Version;
        Sidebar.Channel = AppInfo.Channel;
        Sidebar.TitleBar = TitleBar;
        Sidebar.IsFolded = settings.IsMenuFolded;
        Sidebar.FoldShortcut = $"{command}+B";
        Sidebar.ItemInvoked += (_, item) => Navigate(item);
        Sidebar.AboutRequested += (_, _) => ShowAbout();
        Sidebar.UpdateRequested += (_, _) => ShowSettings(SettingsPage.UpdatesTab);
        Sidebar.FoldChanged += (_, _) => RememberWindow();

        _sizeTimer.Tick += (_, _) =>
        {
            _sizeTimer.Stop();
            RememberWindow();
        };
        Resized += (_, _) =>
        {
            _sizeTimer.Stop();
            _sizeTimer.Start();
        };

        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        UpdateCoordinator.Checked += (_, _) => Ui.Post(RefreshUpdate);
        TunnelManager.StatusChanged += (_, _) => Ui.Post(RefreshState);
        AppEvents.DataChanged += (_, _) => RefreshState();
        ShowSection(_navConnections);
        RefreshUpdate();
        RefreshState();
    }

    /// <summary>Set by App when the application ends: then the window really closes.</summary>
    public bool IsExiting { get; set; }

    /// <summary>The version at the foot of the menu (<c>Version 1.0.0 · release</c>, SETUP-07): it opens About.</summary>
    public Button VersionButton => Sidebar.VersionButton;

    /// <summary>The page shown now (tests).</summary>
    public Control? CurrentPage => PageHost.Content as Control;

    public TunnelListPage ConnectionsPage => _connections.Value;

    public SettingsPage SettingsPage => _settings.Value;

    /// <summary>Shows the window (tray, Dock, second start) in front of the others.</summary>
    public void ShowInFront()
    {
        if (!IsVisible)
        {
            Show();
        }

        BringToFront();
    }

    /// <summary>The connections; <paramref name="select"/> chooses one (tray).</summary>
    public void ShowConnections(Guid? select = null)
    {
        ShowSection(_navConnections);
        if (select is { } id)
        {
            _connections.Value.Select(id);
        }
    }

    public void ShowSettings(string? tab = null)
    {
        _back.Clear();
        Sidebar.Select(_navSettings);
        PageHost.Content = _settings.Value;
        _settings.Value.Refresh();
        if (tab is not null)
        {
            _settings.Value.SelectTab(tab);
        }
    }

    public void ShowAbout()
    {
        _back.Clear();
        Sidebar.Select(null);
        PageHost.Content = _about.Value;
    }

    /// <summary>Opens a page of an entry (edit page); Zurück or Esc returns to the page before.</summary>
    public void OpenPage(Control page)
    {
        if (PageHost.Content is Control current)
        {
            _back.Push(current);
        }

        PageHost.Content = page;
    }

    /// <summary>Back to the page before (an edit page closes); the list it came from draws again.</summary>
    public void GoBack()
    {
        if (_back.Count == 0)
        {
            ShowSection(_navConnections);
            return;
        }

        var previous = _back.Pop();
        PageHost.Content = previous;
        (previous as IRefreshable)?.Refresh();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!IsExiting)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }

    /// <summary>A remembered size never makes the window larger than the screen it opens on.</summary>
    private void FitIntoScreen()
    {
        if (Screens.Primary is null)
        {
            return;
        }

        var workArea = WorkArea();
        Width = Math.Min(Width, workArea.Width - 32);
        Height = Math.Min(Height, workArea.Height - 32);
    }

    private void RememberWindow()
    {
        if (WindowState == WindowState.Normal)
        {
            SettingsService.RememberWindow(Width, Height, Sidebar.IsFolded);
        }
    }

    private void ShowSection(MatNavItem item)
    {
        if (item == _navSettings)
        {
            ShowSettings();
            return;
        }

        _back.Clear();
        Sidebar.Select(item);
        PageHost.Content = _connections.Value;
        _connections.Value.Refresh();
    }

    private void Navigate(MatNavItem item) => ShowSection(item);

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        var withCommand = e.KeyModifiers.HasFlag(Ui.CommandKey.Modifier);
        switch (e.Key)
        {
            case Key.Escape when OwnedWindows.Count == 0 && FocusManager?.GetFocusedElement() is not ComboBox { IsDropDownOpen: true }:
                e.Handled = true;
                if (_back.Count > 0)
                {
                    GoBack();
                }
                else
                {
                    Hide();
                }

                return;
            case Key.D1 when withCommand:
                e.Handled = true;
                ShowSection(_navConnections);
                return;
            case Key.OemComma when withCommand:
                e.Handled = true;
                ShowSettings();
                return;
            case Key.B when withCommand:
                e.Handled = true;
                Sidebar.IsFolded = !Sidebar.IsFolded;
                return;
            case Key.F when withCommand && PageHost.Content is ISearchable searchable:
                e.Handled = true;
                searchable.FocusSearch();
                return;
        }
    }

    private void RefreshUpdate() =>
        Sidebar.UpdateVersion = UpdateService.LastResult is { IsUpdateAvailable: true, Manifest: { } manifest } ? manifest.Version : null;

    /// <summary>The state of the tunnels in the title bar, and the button that pauses or resumes the automation.</summary>
    private void RefreshState()
    {
        var state = TunnelStates.Overall();
        var summary = TunnelStates.Summary();
        StateDot.State = state with { Word = summary };
        AutomationProperties.SetName(StateButton, summary);

        var paused = TunnelManager.AutomationPaused;
        PauseLabel.Icon = paused ? "play" : "pause";
        PauseLabel.Text = paused ? "Automatik fortsetzen" : "Automatik pausieren";
        AutomationProperties.SetName(PauseButton, PauseLabel.Text);
        ToolTip.SetTip(PauseButton, paused
            ? "Automatische Verbindungen und Wiederholungen laufen wieder."
            : "Keine automatischen Verbindungen und keine Wiederholungen, bis du fortsetzt. Verbundene Tunnel bleiben verbunden.");
        if (paused)
        {
            PauseButton.Themed("MatButton.Warning");
        }
        else
        {
            PauseButton.ClearValue(ThemeProperty);
        }
    }

    private void OnStateClick(object? sender, RoutedEventArgs e) => ShowConnections();

    private void OnPauseClick(object? sender, RoutedEventArgs e)
    {
        TunnelManager.SetAutomationPaused(!TunnelManager.AutomationPaused);
        RefreshState();
    }
}

/// <summary>A page that reads its data again when it is shown (after an edit page, after an import).</summary>
public interface IRefreshable
{
    void Refresh();
}

/// <summary>A list page with a search (Strg/⌘+F).</summary>
public interface ISearchable
{
    void FocusSearch();
}

/// <summary>
/// Changes of the data made in the UI (a connection or a profile saved, deleted, imported): the tray menu and open lists
/// draw again. UI only - the data itself goes through the business layer.
/// </summary>
internal static class AppEvents
{
    public static event EventHandler? DataChanged;

    public static void NotifyDataChanged() => DataChanged?.Invoke(null, EventArgs.Empty);
}
