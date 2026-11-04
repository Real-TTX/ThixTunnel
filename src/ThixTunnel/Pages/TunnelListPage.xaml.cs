using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ThixTunnel.Controls;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Models;
using ThixTunnel.Dialogs;

namespace ThixTunnel.Pages;

/// <summary>
/// The connection list of the main window. Double-click (or Enter) connects or disconnects;
/// editing opens the route in its own window.
/// </summary>
public partial class TunnelListPage : UserControl, IDisposable
{
    private List<RouteRow> _rows = [];

    public TunnelListPage()
    {
        InitializeComponent();
        TunnelManager.StatusChanged += OnStatusChanged;
        AppWindows.DataChanged += OnDataChanged;
        Reload(null);
    }

    private RouteRow? SelectedRow => RouteList.SelectedItem as RouteRow;

    public void Dispose()
    {
        TunnelManager.StatusChanged -= OnStatusChanged;
        AppWindows.DataChanged -= OnDataChanged;
    }

    public void Select(Guid routeId)
    {
        RouteList.SelectedItem = _rows.FirstOrDefault(row => row.Id == routeId);
        RouteList.ScrollIntoView(RouteList.SelectedItem);
    }

    private void Reload(Guid? selectRouteId)
    {
        var routes = RouteService.List();
        _rows = routes.Select(route => new RouteRow(route, RouteService.GetHops(route))).ToList();
        RouteList.ItemsSource = _rows;
        RouteList.SelectedItem = _rows.FirstOrDefault(row => row.Id == selectRouteId);

        EmptyText.Text = ProfileService.List().Count == 0
                ? "Noch keine Verbindung. Lege zuerst in den Einstellungen (Zahnrad unten rechts) unter „SSH-Profile“ deine Server an, dann mit + die erste Verbindung."
                : "Noch keine Verbindung. Mit + legst du die erste an.";
        EmptyText.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RefreshActions();
    }

    private void OnDataChanged(object? sender, EventArgs e) => Reload(SelectedRow?.Id);

    private void OnStatusChanged(object? sender, TunnelStatus status) => Dispatcher.BeginInvoke(() =>
    {
        _rows.FirstOrDefault(candidate => candidate.Id == status.RouteId)?.Update(status);
        RefreshActions();
    });

    private void RefreshActions()
    {
        var hasSelection = SelectedRow is not null;
        EditButton.IsEnabled = hasSelection;
        CopyButton.IsEnabled = hasSelection;
        DeleteButton.IsEnabled = hasSelection && !AccessPolicy.IsConfigurationLocked;
        DisconnectAllButton.IsEnabled = TunnelManager.GetStatuses().Any(status => status.IsActive);
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshActions();

    private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var clickedRow = ItemsControl.ContainerFromElement(RouteList, (DependencyObject)e.OriginalSource) is ListBoxItem;
        if (clickedRow && SelectedRow is { } row && e.OriginalSource is not Button)
        {
            ToggleConnection(row.Id);
        }
    }

    private void OnListKeyDown(object sender, KeyEventArgs e)
    {
        if (SelectedRow is not { } row)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            ToggleConnection(row.Id);
            e.Handled = true;
        }
        else if (e.Key == Key.Delete)
        {
            OnDeleteClick(sender, e);
            e.Handled = true;
        }
    }

    private void OnRowActionClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Guid routeId })
        {
            RouteList.SelectedItem = _rows.FirstOrDefault(row => row.Id == routeId);
            ToggleConnection(routeId);
        }
    }

    private void ToggleConnection(Guid routeId) => PageActions.Run(() =>
    {
        if (TunnelManager.GetStatus(routeId).IsActive)
        {
            TunnelManager.Disconnect(routeId);
        }
        else
        {
            TunnelManager.Connect(routeId);
        }
    }, MessageBar);

    private void OnNewClick(object sender, RoutedEventArgs e) => AppWindows.ShowRouteEditor(null);

    private void OnEditClick(object sender, RoutedEventArgs e)
    {
        if (SelectedRow is { } row)
        {
            AppWindows.ShowRouteEditor(row.Id);
        }
    }

    private void OnDisconnectAllClick(object sender, RoutedEventArgs e) => TunnelManager.DisconnectAll();

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (SelectedRow is not { } row || AccessPolicy.IsConfigurationLocked ||
            !ConfirmDialog.Ask(Window.GetWindow(this), "Verbindung löschen", $"„{row.Name}“ wird getrennt und gelöscht. Die SSH-Profile bleiben erhalten.", "Löschen", isDestructive: true))
        {
            return;
        }

        if (PageActions.Run(() => RouteService.Delete(row.Id), MessageBar))
        {
            AppWindows.NotifyDataChanged();
            MessageBar.Show($"„{row.Name}“ wurde gelöscht.", InfoSeverity.Success);
        }
    }

    private void OnCopyCommandClick(object sender, RoutedEventArgs e)
    {
        if (SelectedRow is not { } row || RouteService.Get(row.Id) is not { } route)
        {
            return;
        }

        Clipboard.SetText(RouteService.DescribeCommand(route));
        MessageBar.Show("ssh-Befehl in die Zwischenablage kopiert.", InfoSeverity.Success);
    }

    /// <summary>One entry of the list; the status updates in place.</summary>
    public sealed class RouteRow : INotifyPropertyChanged
    {
        private readonly string _endpoints;
        private readonly string _chain;

        public RouteRow(TunnelRoute route, IReadOnlyList<SshProfile> hops)
        {
            Id = route.Id;
            Name = route.Name;
            _endpoints = $"{route.LocalEndpoint} → {route.TargetEndpoint}";
            _chain = hops.Count == 0 ? "Keine SSH-Hops" : string.Join(" → ", hops.Select(hop => hop.Name));
            Status = TunnelManager.GetStatus(route.Id);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public Guid Id { get; }

        public string Name { get; }

        /// <summary>Screen readers and UI automation read the entry by its name.</summary>
        public override string ToString() => Name;

        public TunnelStatus Status { get; private set; }

        public Brush DotBrush => Resource(StatusBadge.BrushKeys(Status).Dot);

        /// <summary>Second line: the endpoints - or, while something is going on, what is going on.</summary>
        public string Detail => Status.State switch
        {
            TunnelState.Connecting or TunnelState.WaitingForRetry or TunnelState.Failed when Status.Message.Length > 0 => Status.Message,
            _ => _endpoints,
        };

        public Brush DetailBrush => Resource(Status.State switch
        {
            TunnelState.Failed => "DangerBrush",
            TunnelState.Connecting or TunnelState.WaitingForRetry => "WarningBrush",
            _ => "TextMutedBrush",
        });

        public string Details
        {
            get
            {
                var status = StatusBadge.Label(Status);
                var retry = Status.State == TunnelState.WaitingForRetry && Status.NextAttempt is { } next ? $" – nächster Versuch um {next:HH:mm:ss}" : "";
                var message = Status.Message.Length > 0 ? $"\n{Status.Message}{retry}" : "";
                return $"{Name}\n{_endpoints}\nÜber: {_chain}\n{status}{message}";
            }
        }

        public string ActionText => Status.IsActive ? "Trennen" : "Verbinden";

        public string ActionIcon => (string)Application.Current.FindResource(Status.IsActive ? "IconDisconnect" : "IconConnect");

        public Brush ActionBrush => Resource(Status.IsActive ? "TextMutedBrush" : "AccentHoverBrush");

        public void Update(TunnelStatus status)
        {
            Status = status;
            foreach (var name in new[] { nameof(Status), nameof(DotBrush), nameof(Detail), nameof(DetailBrush), nameof(Details), nameof(ActionText), nameof(ActionIcon), nameof(ActionBrush) })
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }

        private static Brush Resource(string key) => (Brush)Application.Current.FindResource(key);
    }
}
