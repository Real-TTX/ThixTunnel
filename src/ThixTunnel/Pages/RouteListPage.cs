using Avalonia.Controls;
using ThixTunnel.Controls;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Models;
using ThixTunnel.Dialogs;

namespace ThixTunnel.Pages;

/// <summary>
/// Managing the connections (tab "Verbindungen" of the settings): toolbar, list, and below it Neue Verbindung, Bearbeiten,
/// ssh-Befehl kopieren … (gap) … Löschen. Opening a connection edits it on its own page.
/// </summary>
public sealed class RouteListPage : UserControl, IRefreshable, ISearchable
{
    private readonly MainWindow _window;
    private readonly ListLayout _layout;
    private readonly Button _newButton;
    private readonly Button _editButton;
    private readonly Button _copyButton;
    private readonly Button _deleteButton;

    public RouteListPage(MainWindow window)
    {
        _window = window;
        var toolbar = new ListToolbar("Verbindungen durchsuchen", TunnelListPage.Filters, TunnelListPage.Sortings);
        var list = new EntityList("", openName: "bearbeiten");
        _layout = new ListLayout(null, "Eine Verbindung leitet einen lokalen Port über einen oder mehrere SSH-Server (Hops) zu einem Ziel weiter.", toolbar, list);
        _newButton = _layout.AddAction("plus", "Neue Verbindung", () => Open(null), "MatButton.Primary");
        _editButton = _layout.AddAction("edit", "Bearbeiten", () => Open(_layout.List.SelectedId));
        _copyButton = _layout.AddAction("copy", "ssh-Befehl kopieren", CopyCommand);
        _deleteButton = _layout.AddAction("trash", "Löschen", Delete, "MatButton.Danger", separated: true);
        toolbar.Changed += (_, _) => Refresh();
        list.Opened += (_, id) => Open(id);
        list.SelectionChanged += (_, _) => RefreshActions();
        Content = _layout;
        Refresh();
    }

    public EntityList List => _layout.List;

    public InfoBar Message => _layout.Message;

    public void FocusSearch() => _layout.Toolbar.SearchBox.Focus();

    public void Refresh()
    {
        var total = RouteService.List().Count;
        _layout.List.EmptyText = total > 0
            ? "Keine Verbindung passt zur Suche oder zum Filter."
            : ProfileService.List().Count == 0
                ? "Noch keine Verbindung. Lege zuerst im Reiter „SSH-Profile“ deine Server an, dann hier die erste Verbindung."
                : "Noch keine Verbindung. Mit „Neue Verbindung“ legst du die erste an.";
        _layout.List.Show(RouteService.List(_layout.Toolbar.Query).Select(route =>
        {
            var status = TunnelManager.GetStatus(route.Id);
            return new EntityRow(route.Id, "route", route.Name, $"{route.LocalEndpoint} → {route.TargetEndpoint}")
            {
                StateWord = TunnelStates.Word(status),
                Badges = route.ConnectOnStartup ? ["Auto-Verbindung"] : [],
                StateToken = TunnelStates.Token(status),
                Detail = $"Über {TunnelStates.Chain(RouteService.GetHops(route))}",
            };
        }).ToList(), total);
        RefreshActions();
    }

    private void RefreshActions()
    {
        var hasSelection = _layout.List.SelectedId is not null;
        var editable = !AccessPolicy.IsConfigurationLocked;
        _newButton.IsEnabled = editable;
        _editButton.IsEnabled = hasSelection;
        _copyButton.IsEnabled = hasSelection;
        _deleteButton.IsEnabled = hasSelection && editable;
    }

    private void Open(Guid? routeId) => _window.OpenPage(new TunnelEditPage(_window, routeId));

    private async void CopyCommand()
    {
        if (_layout.List.SelectedId is not { } id || RouteService.Get(id) is not { } route || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        await clipboard.SetTextAsync(RouteService.DescribeCommand(route));
        _layout.Message.Show("ssh-Befehl in die Zwischenablage kopiert.", InfoSeverity.Success);
    }

    private async void Delete()
    {
        if (_layout.List.SelectedId is not { } id || RouteService.Get(id) is not { } route)
        {
            return;
        }

        if (!await ConfirmDialog.AskAsync(Ui.WindowOf(this), $"„{route.Name}“ löschen?", "Die Verbindung wird getrennt und gelöscht. Die SSH-Profile bleiben erhalten.", "Löschen", isDestructive: true))
        {
            return;
        }

        if (_layout.Run(() => RouteService.Delete(id)))
        {
            AppEvents.NotifyDataChanged();
            Refresh();
            _layout.Message.Show($"„{route.Name}“ wurde gelöscht.", InfoSeverity.Success);
        }
    }
}
