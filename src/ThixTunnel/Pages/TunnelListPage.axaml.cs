using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using ThixTunnel.Controls;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Models;

namespace ThixTunnel.Pages;

/// <summary>
/// The connection list of the main window: only connect and disconnect (button, double-click or Enter).
/// Creating, changing, deleting, import and export happen in the settings window.
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
        if (RouteList.SelectedItem is { } selected)
        {
            RouteList.ScrollIntoView(selected);
        }
    }

    private void Reload(Guid? selectRouteId)
    {
        var routes = RouteService.List();
        _rows = routes.Select(route => new RouteRow(route, RouteService.GetHops(route))).ToList();
        RouteList.ItemsSource = _rows;
        RouteList.SelectedItem = _rows.FirstOrDefault(row => row.Id == selectRouteId);

        EmptyText.Text = "Noch keine Verbindung. Verbindungen und SSH-Profile legst du in den Einstellungen an (Zahnrad unten rechts).";
        EmptyText.IsVisible = _rows.Count == 0;
    }

    private void OnDataChanged(object? sender, EventArgs e) => Reload(SelectedRow?.Id);

    private void OnStatusChanged(object? sender, TunnelStatus status) =>
        Ui.Post(() => _rows.FirstOrDefault(candidate => candidate.Id == status.RouteId)?.Update(status));

    private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        var source = e.Source as Visual;
        var onRow = source?.FindAncestorOfType<ListBoxItem>(includeSelf: true) is not null;
        var onButton = source?.FindAncestorOfType<Button>(includeSelf: true) is not null;
        if (onRow && !onButton && SelectedRow is { } row)
        {
            ToggleConnection(row.Id);
        }
    }

    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && SelectedRow is { } row)
        {
            ToggleConnection(row.Id);
            e.Handled = true;
        }
    }

    private void OnRowActionClick(object? sender, RoutedEventArgs e)
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

        public IBrush DotBrush => Ui.Brush(StatusBadge.BrushKeys(Status).Dot);

        /// <summary>Second line: the endpoints - or, while something is going on, what is going on.</summary>
        public string Detail => Status.State switch
        {
            TunnelState.Connecting or TunnelState.WaitingForRetry or TunnelState.Failed when Status.Message.Length > 0 => Status.Message,
            _ => _endpoints,
        };

        public IBrush DetailBrush => Ui.Brush(Status.State switch
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

        public string ActionIcon => Ui.Icon(Status.IsActive ? "IconDisconnect" : "IconConnect");

        public IBrush ActionBrush => Ui.Brush(Status.IsActive ? "TextMutedBrush" : "AccentHoverBrush");

        public void Update(TunnelStatus status)
        {
            Status = status;
            foreach (var name in new[] { nameof(Status), nameof(DotBrush), nameof(Detail), nameof(DetailBrush), nameof(Details), nameof(ActionText), nameof(ActionIcon), nameof(ActionBrush) })
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}
