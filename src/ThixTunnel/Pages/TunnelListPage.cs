using Avalonia.Controls;
using ThixTunnel.Controls;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Models;

namespace ThixTunnel.Pages;

/// <summary>
/// The connections of the main window: the toolbar (search, filter, sort) above, one card per connection with its state, and
/// Verbinden / Trennen at its right (also a double click or Enter). Creating, changing, deleting, import and export happen
/// in the settings only (owner's decision).
/// </summary>
public sealed class TunnelListPage : UserControl, IRefreshable, ISearchable
{
    public static readonly ListOption[] Filters =
    [
        new(RouteService.FilterAll, "Alle"),
        new(RouteService.FilterActive, "Aktiv"),
        new(RouteService.FilterInactive, "Getrennt"),
        new(RouteService.FilterProblem, "Mit Fehler"),
        new(RouteService.FilterAutoConnect, "Auto-Verbindung"),
    ];

    public static readonly ListOption[] Sortings =
    [
        new(RouteService.SortByName, "Name"),
        new(RouteService.SortByLocalPort, "Lokaler Port"),
        new(RouteService.SortByStatus, "Status"),
    ];

    private readonly MainWindow _window;
    private readonly ListLayout _layout;
    private readonly Button _manageButton;
    private bool _refreshPosted;

    public TunnelListPage(MainWindow window)
    {
        _window = window;
        var toolbar = new ListToolbar("Verbindungen durchsuchen", Filters, Sortings);
        var list = new EntityList("", showOpenButton: false);
        _layout = new ListLayout("Verbindungen",
            "Ein Klick auf Verbinden baut den Tunnel auf: ssh über die SSH-Hops der Verbindung, mit der lokalen Weiterleitung. Anlegen und ändern: Einstellungen › Verbindungen.",
            toolbar, list);
        _manageButton = _layout.AddAction("settings", "Verbindungen anlegen …", () => _window.ShowSettings(SettingsPage.RoutesTab), "MatButton.Primary");
        toolbar.Changed += (_, _) => Refresh();
        list.Opened += (_, id) => Toggle(id);
        Content = Ui.Page(_layout);

        // State changes come from the ssh processes (any thread), often several at once: one redraw per burst.
        TunnelManager.StatusChanged += (_, _) => PostRefresh();
        AppEvents.DataChanged += (_, _) => Refresh();
        Refresh();
    }

    public EntityList List => _layout.List;

    public ListToolbar Toolbar => _layout.Toolbar;

    public InfoBar Message => _layout.Message;

    public void FocusSearch() => _layout.Toolbar.SearchBox.Focus();

    public void Select(Guid routeId) => _layout.List.Select(routeId);

    public void Refresh()
    {
        var total = RouteService.List().Count;
        var routes = RouteService.List(_layout.Toolbar.Query);
        _layout.List.EmptyText = total == 0
            ? "Noch keine Verbindung. Verbindungen und SSH-Profile legst du in den Einstellungen an."
            : "Keine Verbindung passt zur Suche oder zum Filter.";
        _manageButton.IsVisible = total == 0;
        _layout.List.Show(routes.Select(Row).ToList(), total);
    }

    private void PostRefresh()
    {
        if (_refreshPosted)
        {
            return;
        }

        _refreshPosted = true;
        Ui.Post(() =>
        {
            _refreshPosted = false;
            Refresh();
        });
    }

    private EntityRow Row(TunnelRoute route)
    {
        var status = TunnelManager.GetStatus(route.Id);
        var hops = RouteService.GetHops(route);
        var chain = TunnelStates.Chain(hops);
        var detail = TunnelStates.Detail(route, status);
        return new EntityRow(route.Id, status.IsActive ? "link" : "route", route.Name, detail)
        {
            StateWord = TunnelStates.Word(status),
            Badges = route.ConnectOnStartup ? ["Auto-Verbindung"] : [],
            StateToken = TunnelStates.Token(status),
            SubtitleToken = status.State == TunnelState.Failed ? "Mat.DangerText" : null,
            Detail = $"Über {chain}",
            ToolTip = $"{route.Name}\n{route.LocalEndpoint} → {route.TargetEndpoint}\nÜber: {chain}\n{TunnelStates.Word(status)}" +
                      (status.Message.Length > 0 && status.State != TunnelState.Connected ? $"\n{status.Message}" : ""),
            Trailing = () => status.IsActive
                ? Ui.Button("stop", "Trennen", click: () => Toggle(route.Id))
                : Ui.Button("play", "Verbinden", "MatButton.Primary", () => Toggle(route.Id)),
        };
    }

    /// <summary>Connects a disconnected tunnel and disconnects an active one (a manual connect may ask for a password).</summary>
    private void Toggle(Guid routeId)
    {
        _layout.List.Select(routeId);
        _layout.Run(() =>
        {
            if (TunnelManager.GetStatus(routeId).IsActive)
            {
                TunnelManager.Disconnect(routeId);
            }
            else
            {
                TunnelManager.Connect(routeId);
            }
        });
        Refresh();
    }
}
