using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ThixTunnel.Controls;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Models;
using ThixTunnel.Dialogs;

namespace ThixTunnel.Pages;

/// <summary>List of the tunnel routes with status, connect/disconnect and the details of the selected route.</summary>
public partial class TunnelListPage : UserControl, IDisposable
{
    private static readonly ListOption[] Filters =
    [
        new(RouteService.FilterAll, "Alle"),
        new(RouteService.FilterActive, "Aktiv"),
        new(RouteService.FilterInactive, "Getrennt"),
        new(RouteService.FilterProblem, "Mit Fehler"),
        new(RouteService.FilterAutoConnect, "Auto-Verbindung"),
    ];

    private static readonly ListOption[] SortFields =
    [
        new(RouteService.SortByName, "Name"),
        new(RouteService.SortByLocalPort, "Lokaler Port"),
        new(RouteService.SortByStatus, "Status"),
    ];

    private readonly MainWindow _window;
    private List<RouteRow> _rows = [];

    public TunnelListPage(MainWindow window, Guid? selectRouteId)
    {
        _window = window;
        InitializeComponent();
        Toolbar.Configure(Filters, SortFields);
        Toolbar.QueryChanged += (_, _) => Reload(SelectedRow?.Id);
        TunnelManager.StatusChanged += OnStatusChanged;
        Reload(selectRouteId);
    }

    private RouteRow? SelectedRow => RouteList.SelectedItem as RouteRow;

    public void Dispose() => TunnelManager.StatusChanged -= OnStatusChanged;

    private void Reload(Guid? selectRouteId)
    {
        var routes = RouteService.List(Toolbar.Query);
        _rows = routes.Select(route => new RouteRow(route, RouteService.GetHops(route))).ToList();
        RouteList.ItemsSource = _rows;
        RouteList.SelectedItem = _rows.FirstOrDefault(row => row.Id == selectRouteId) ?? _rows.FirstOrDefault();

        var hasAnyRoute = routes.Count > 0 || RouteService.List().Count > 0;
        EmptyText.Text = hasAnyRoute
            ? "Keine Tunnelroute passt zur Suche oder zum Filter."
            : ProfileService.List().Count == 0
                ? "Noch keine Tunnelroute. Lege zuerst unter „SSH-Profile“ die Server an und danach hier eine Tunnelroute."
                : "Noch keine Tunnelroute. Mit „Neue Tunnelroute“ legst du die erste an.";
        EmptyText.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RefreshActions();
        RefreshDetails();
    }

    private void OnStatusChanged(object? sender, TunnelStatus status) => Dispatcher.BeginInvoke(() =>
    {
        var row = _rows.FirstOrDefault(candidate => candidate.Id == status.RouteId);
        row?.Update(status);
        if (row is not null && row == SelectedRow)
        {
            RefreshDetails();
        }

        RefreshActions();
    });

    private void RefreshActions()
    {
        EditButton.IsEnabled = SelectedRow is not null;
        DeleteButton.IsEnabled = SelectedRow is not null && !AccessPolicy.IsConfigurationLocked;
        DisconnectAllButton.IsEnabled = TunnelManager.GetStatuses().Any(status => status.IsActive);
    }

    private void RefreshDetails()
    {
        var row = SelectedRow;
        var route = row is null ? null : RouteService.Get(row.Id);
        if (row is null || route is null)
        {
            DetailsTile.Visibility = Visibility.Collapsed;
            return;
        }

        var hops = RouteService.GetHops(route);
        DetailsTile.Visibility = Visibility.Visible;
        DetailsTitle.Text = route.Name;
        DetailsChain.Hops = hops.Select(hop => hop.Name).ToList();
        DetailsLocal.Text = route.LocalEndpoint;
        DetailsTarget.Text = route.TargetEndpoint;
        DetailsTargetField.Header = hops.Count > 0 ? $"Ziel auf {hops[^1].Name}" : "Ziel";
        ShowFlag(AutoConnectIcon, AutoConnectText, route.ConnectOnStartup, "Auto-Verbindung aktiv", "Keine Auto-Verbindung");
        ShowFlag(ReconnectIcon, ReconnectText, route.ReconnectOnFailure, "Wiederverbindung aktiv", "Keine Wiederverbindung");

        var status = row.Status;
        DetailsStatusDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, StatusBadge.BrushKeys(status).Dot);
        DetailsStatusText.Text = status.State == TunnelState.WaitingForRetry && status.NextAttempt is { } next
            ? $"{status.Message} – nächster Versuch um {next:HH:mm:ss}"
            : status.Message;
        DetailsCommand.Text = RouteService.DescribeCommand(route);
    }

    private void ShowFlag(TextBlock icon, TextBlock text, bool isOn, string onText, string offText)
    {
        icon.Text = (string)FindResource(isOn ? "IconCheck" : "IconRemove");
        icon.SetResourceReference(TextBlock.ForegroundProperty, isOn ? "SuccessBrush" : "TextMutedBrush");
        text.Text = isOn ? onText : offText;
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshActions();
        RefreshDetails();
    }

    private void OnRowDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (SelectedRow is { } row && e.OriginalSource is not Button)
        {
            _window.EditRoute(row.Id);
        }
    }

    private void OnRowActionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Guid routeId })
        {
            return;
        }

        RouteList.SelectedItem = _rows.FirstOrDefault(row => row.Id == routeId);
        PageActions.Run(() =>
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
    }

    private void OnNewClick(object sender, RoutedEventArgs e) => _window.EditRoute(null);

    private void OnEditClick(object sender, RoutedEventArgs e)
    {
        if (SelectedRow is { } row)
        {
            _window.EditRoute(row.Id);
        }
    }

    private void OnDisconnectAllClick(object sender, RoutedEventArgs e) => TunnelManager.DisconnectAll();

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (SelectedRow is not { } row ||
            !ConfirmDialog.Ask(_window, "Tunnelroute löschen", $"„{row.Name}“ wird getrennt und gelöscht. Die SSH-Profile bleiben erhalten.", "Löschen", isDestructive: true))
        {
            return;
        }

        if (PageActions.Run(() => RouteService.Delete(row.Id), MessageBar))
        {
            Reload(null);
            MessageBar.Show($"„{row.Name}“ wurde gelöscht.", InfoSeverity.Success);
        }
    }

    private void OnCopyCommandClick(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(DetailsCommand.Text);
        MessageBar.Show("Befehl in die Zwischenablage kopiert.", InfoSeverity.Success);
    }

    /// <summary>One row of the table; the status updates in place.</summary>
    public sealed class RouteRow : INotifyPropertyChanged
    {
        public RouteRow(TunnelRoute route, IReadOnlyList<SshProfile> hops)
        {
            Id = route.Id;
            Name = route.Name;
            Chain = hops.Count == 0 ? "Keine SSH-Hops" : string.Join(" → ", hops.Select(hop => hop.Name));
            Local = route.LocalEndpoint;
            Target = route.TargetEndpoint;
            Status = TunnelManager.GetStatus(route.Id);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public Guid Id { get; }

        public string Name { get; }

        /// <summary>Screen readers and UI automation read the row by its name.</summary>
        public override string ToString() => Name;

        public string Chain { get; }

        public string Local { get; }

        public string Target { get; }

        public TunnelStatus Status { get; private set; }

        public string ActionText => Status.IsActive ? "Trennen" : "Verbinden";

        public string ActionIcon => (string)Application.Current.FindResource(Status.IsActive ? "IconDisconnect" : "IconConnect");

        public Style ActionStyle => (Style)Application.Current.FindResource(Status.IsActive ? "ButtonBase" : "PrimaryButton");

        public void Update(TunnelStatus status)
        {
            Status = status;
            foreach (var name in new[] { nameof(Status), nameof(ActionText), nameof(ActionIcon), nameof(ActionStyle) })
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}
