using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ThixTunnel.Controls;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Models;
using ThixTunnel.Dialogs;

namespace ThixTunnel.Pages;

/// <summary>
/// Managing the connections (tunnel routes) in the settings window: search, filter, sort, create, edit, delete.
/// Editing opens the route in its own window.
/// </summary>
public partial class RouteListPage : UserControl, IDisposable
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

    public RouteListPage()
    {
        InitializeComponent();
        Toolbar.Configure(Filters, SortFields);
        Toolbar.QueryChanged += (_, _) => Reload(SelectedRow?.Id);
        AppWindows.DataChanged += OnDataChanged;
        TunnelManager.StatusChanged += OnStatusChanged;
        Reload(null);
    }

    private RouteRow? SelectedRow => RouteList.SelectedItem as RouteRow;

    public void Dispose()
    {
        AppWindows.DataChanged -= OnDataChanged;
        TunnelManager.StatusChanged -= OnStatusChanged;
    }

    private void OnDataChanged(object? sender, EventArgs e) => Reload(SelectedRow?.Id);

    // The status filter and the status dots follow the tunnels.
    private void OnStatusChanged(object? sender, TunnelStatus status) => Dispatcher.BeginInvoke(() => Reload(SelectedRow?.Id));

    private void Reload(Guid? selectRouteId)
    {
        var rows = RouteService.List(Toolbar.Query).Select(route => new RouteRow(route, RouteService.GetHops(route))).ToList();
        RouteList.ItemsSource = rows;
        RouteList.SelectedItem = rows.FirstOrDefault(row => row.Id == selectRouteId) ?? rows.FirstOrDefault();
        EmptyText.Text = RouteService.List().Count > 0
            ? "Keine Verbindung passt zur Suche oder zum Filter."
            : ProfileService.List().Count == 0
                ? "Noch keine Verbindung. Lege zuerst im Reiter „SSH-Profile“ deine Server an, dann hier die erste Verbindung."
                : "Noch keine Verbindung. Mit „Neue Verbindung“ legst du die erste an.";
        EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RefreshActions();
    }

    private void RefreshActions()
    {
        var hasSelection = SelectedRow is not null;
        EditButton.IsEnabled = hasSelection;
        CopyButton.IsEnabled = hasSelection;
        DeleteButton.IsEnabled = hasSelection && !AccessPolicy.IsConfigurationLocked;
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshActions();

    private void OnRowDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ItemsControl.ContainerFromElement(RouteList, (DependencyObject)e.OriginalSource) is ListBoxItem)
        {
            OnEditClick(sender, e);
        }
    }

    private void OnNewClick(object sender, RoutedEventArgs e) => AppWindows.ShowRouteEditor(null);

    private void OnEditClick(object sender, RoutedEventArgs e)
    {
        if (SelectedRow is { } row)
        {
            AppWindows.ShowRouteEditor(row.Id);
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

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (SelectedRow is not { } row ||
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

    /// <summary>One entry of the management list.</summary>
    public sealed class RouteRow(TunnelRoute route, IReadOnlyList<SshProfile> hops)
    {
        private readonly TunnelStatus _status = TunnelManager.GetStatus(route.Id);

        public Guid Id { get; } = route.Id;

        public string Name { get; } = route.Name;

        /// <summary>Screen readers and UI automation read the entry by its name.</summary>
        public override string ToString() => Name;

        public string Endpoints { get; } = $"{route.LocalEndpoint} → {route.TargetEndpoint}";

        public string Chain { get; } = "Über " + (hops.Count == 0 ? "–" : string.Join(" → ", hops.Select(hop => hop.Name)));

        public string AutomationText { get; } = route.ConnectOnStartup ? "Auto-Verbindung" : "";

        public Brush DotBrush => (Brush)Application.Current.FindResource(StatusBadge.BrushKeys(_status).Dot);

        public string Details => $"{Name}\n{Endpoints}\n{Chain}\n{StatusBadge.Label(_status)}" + (_status.Message.Length > 0 ? $"\n{_status.Message}" : "");
    }
}
