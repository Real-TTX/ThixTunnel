using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using MatDesktop;
using ThixTunnel.Controls;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Models;
using ThixTunnel.Dialogs;

namespace ThixTunnel.Pages;

/// <summary>
/// Create or change a connection, tabs "Route" (name and the ordered SSH hops), "Portweiterleitung" and "Automatisierung".
/// Buttons: Speichern, Speichern und verbinden, Zurück … (gap) … Löschen.
/// </summary>
public sealed class TunnelEditPage : UserControl, IRefreshable
{
    private readonly MainWindow _window;
    private readonly TunnelRoute _route;
    private readonly bool _isNew;
    private readonly List<Guid> _hops;
    private readonly EditLayout _layout;
    private readonly TextBox _name = new();
    private readonly StackPanel _hopList = new() { Spacing = 8 };
    private readonly HopChain _chain = new() { Margin = new Thickness(0, 4, 0, 0) };
    private readonly Button _addHop;
    private readonly TextBox _localAddress = new() { MaxWidth = 260, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBox _localPort = new() { MaxWidth = 140, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBox _targetHost = new() { MaxWidth = 420, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBox _targetPort = new() { MaxWidth = 140, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly MatFormField _localAddressField;
    private readonly MatFormField _targetHostField;
    private readonly ToggleSwitch _connectOnStartup;
    private readonly ToggleSwitch _reconnect;
    private IReadOnlyList<SshProfile> _profiles;

    public TunnelEditPage(MainWindow window, Guid? routeId)
    {
        _window = window;
        var existing = routeId is null ? null : RouteService.Get(routeId.Value);
        _isNew = existing is null;
        _route = existing ?? new TunnelRoute();
        _profiles = ProfileService.List();
        _hops = [.. _route.HopProfileIds];
        if (_isNew && _profiles.Count > 0)
        {
            _hops.Add(_profiles[0].Id);
        }

        _layout = new EditLayout(_isNew ? "Neue Verbindung" : _route.Name);

        // ---- Route: name and hops ----
        _name.Text = _route.Name;
        _addHop = Ui.Button("plus", "Hop hinzufügen", click: AddHop);
        var route = new StackPanel { MaxWidth = 680, HorizontalAlignment = HorizontalAlignment.Left };
        route.Children.Add(Ui.Field("Name", _name, nameof(TunnelRoute.Name), "So erscheint die Verbindung in der Liste und im Menü des Symbols."));
        route.Children.Add(Ui.Text("SSH-Hops", "MatText.SectionTitle"));
        route.Children.Add(Ui.Text("Die Profile werden in dieser Reihenfolge verbunden. Alle vor dem letzten dienen als Sprungserver (ssh -J).", "MatText.Muted"));
        var hopsField = Ui.Field("", _hopList, nameof(TunnelRoute.HopProfileIds));
        hopsField.Margin = new Thickness(0, 12, 0, 0);
        route.Children.Add(hopsField);
        route.Children.Add(new ActionBar { Margin = new Thickness(0, 4, 0, 12), Children = { _addHop, Ui.Button("user", "Neues SSH-Profil …", click: NewProfile) } });
        route.Children.Add(_chain);
        _layout.AddTab("Route", route);

        // ---- Port forwarding ----
        _localAddress.Text = _route.LocalAddress;
        _localPort.Text = _route.LocalPort > 0 ? _route.LocalPort.ToString() : "";
        _targetHost.Text = _route.TargetHost;
        _targetPort.Text = _route.TargetPort > 0 ? _route.TargetPort.ToString() : "";
        _localAddressField = Ui.Field("Lokale Adresse", _localAddress, nameof(TunnelRoute.LocalAddress));
        _targetHostField = Ui.Field("Zielhost", _targetHost, nameof(TunnelRoute.TargetHost));
        var forwarding = new StackPanel { MaxWidth = 680, HorizontalAlignment = HorizontalAlignment.Left };
        forwarding.Children.Add(Ui.Text("Auf diesem Rechner", "MatText.SectionTitle"));
        forwarding.Children.Add(Pair(_localAddressField, Ui.Field("Lokaler Port", _localPort, nameof(TunnelRoute.LocalPort))));
        forwarding.Children.Add(Ui.Text("Ziel", "MatText.SectionTitle"));
        forwarding.Children.Add(Pair(_targetHostField, Ui.Field("Zielport", _targetPort, nameof(TunnelRoute.TargetPort))));
        _layout.AddTab("Portweiterleitung", forwarding);
        _localAddress.TextChanged += (_, _) => RefreshLocalAddressHint();
        _localPort.TextChanged += (_, _) => RefreshChain();
        _targetHost.TextChanged += (_, _) => RefreshChain();
        _targetPort.TextChanged += (_, _) => RefreshChain();

        // ---- Automation ----
        _connectOnStartup = Ui.Switch("Beim App-Start automatisch verbinden", _route.ConnectOnStartup);
        _reconnect = Ui.Switch("Bei Verbindungsabbruch erneut verbinden", _route.ReconnectOnFailure);
        var automationHint = new InfoBar();
        automationHint.Show("Automatische Verbindungen fragen nie nach Passwörtern oder Hostschlüsseln. Für alle Hops werden Schlüssel gebraucht " +
                            "(SSH-Agent, Schlüsseldatei oder ein Passwort-Profil mit „Schlüssel beim ersten Anmelden anlegen“); unbekannte Hostschlüssel einmal manuell bestätigen.",
            InfoSeverity.Info);
        var automation = new StackPanel { MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Left };
        automation.Children.Add(Ui.Row("Beim App-Start automatisch verbinden", "Gilt für jeden Start von ThixTunnel – mit dem Autostart also direkt nach der Anmeldung.", _connectOnStartup));
        automation.Children.Add(Ui.Row("Bei Verbindungsabbruch erneut verbinden", "Wiederholt mit wachsenden Pausen. Manuelles Trennen beendet die Versuche.", _reconnect, last: true));
        automationHint.Margin = new Thickness(0, 14, 0, 0);
        automation.Children.Add(automationHint);
        _layout.AddTab("Automatisierung", automation);

        // ---- Buttons ----
        var save = _layout.AddAction("save", "Speichern", () => SaveAndThen(_ => _window.GoBack()), "MatButton.Primary");
        save.IsDefault = true;
        var saveConnect = _layout.AddAction("play", "Speichern und verbinden", () => SaveAndThen(Connect));
        _layout.AddAction("back", "Zurück", _window.GoBack);
        Button? delete = null;
        if (!_isNew)
        {
            delete = _layout.AddAction("trash", "Löschen", Delete, "MatButton.Danger", separated: true);
        }

        if (_profiles.Count == 0)
        {
            _layout.Message.Show("Es gibt noch kein SSH-Profil. Lege zuerst ein Profil an („Neues SSH-Profil …“).", InfoSeverity.Warning);
        }

        if (AccessPolicy.IsConfigurationLocked)
        {
            _layout.Message.Show("Von deiner Organisation festgelegt: die Konfiguration ist gesperrt und kann nicht geändert werden.", InfoSeverity.Warning);
            save.IsEnabled = saveConnect.IsEnabled = false;
            if (delete is not null)
            {
                delete.IsEnabled = false;
            }
        }

        RenderHops();
        RefreshLocalAddressHint();
        Content = Ui.Page(_layout, 860);
        AttachedToVisualTree += (_, _) => _name.Focus();
    }

    public EditLayout Layout => _layout;

    public TextBox NameBox => _name;

    public TextBox LocalPortBox => _localPort;

    public TextBox TargetPortBox => _targetPort;

    /// <summary>Back from "Neues SSH-Profil …": the new profile is offered in the hop lists.</summary>
    public void Refresh()
    {
        var hadNoProfiles = _profiles.Count == 0;
        _profiles = ProfileService.List();
        _hops.RemoveAll(id => _profiles.All(profile => profile.Id != id));
        if (_hops.Count == 0 && _profiles.Count > 0)
        {
            _hops.Add(_profiles[^1].Id);
        }

        if (hadNoProfiles && _profiles.Count > 0)
        {
            _layout.Message.Hide();
        }

        RenderHops();
    }

    /// <summary>Two fields side by side: the address takes the room, the port is narrow.</summary>
    private static Grid Pair(Control wide, Control narrow)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,16,160") };
        Grid.SetColumn(narrow, 2);
        grid.Children.Add(wide);
        grid.Children.Add(narrow);
        return grid;
    }

    private void RenderHops()
    {
        _hopList.Children.Clear();
        for (var index = 0; index < _hops.Count; index++)
        {
            _hopList.Children.Add(HopRow(index));
        }

        _addHop.IsEnabled = _profiles.Count > 0 && _hops.Count < 10;
        var last = _profiles.FirstOrDefault(profile => _hops.Count > 0 && profile.Id == _hops[^1]);
        _targetHostField.Hint = last is null ? null : $"Ziel aus Sicht von {last.Name} – 127.0.0.1 ist dieser Server selbst.";
        RefreshChain();
    }

    private void RefreshChain()
    {
        _chain.Hops = _hops.Select(id => _profiles.FirstOrDefault(profile => profile.Id == id)?.Name ?? "?").ToList();
        var port = (_targetPort.Text ?? "").Trim();
        var host = (_targetHost.Text ?? "").Trim();
        _chain.Target = host.Length == 0 ? "" : port.Length == 0 ? host : $"{host}:{port}";
    }

    private Border HopRow(int index)
    {
        var isLast = index == _hops.Count - 1;
        var profile = _profiles.FirstOrDefault(candidate => candidate.Id == _hops[index]);

        var number = new Border
        {
            Width = 30,
            Height = 30,
            CornerRadius = new CornerRadius(15),
            Margin = new Thickness(0, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = (index + 1).ToString(), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeight.SemiBold }
                .Use(TextBlock.ForegroundProperty, "Mat.AccentText"),
        }.Use(Border.BackgroundProperty, "Mat.AccentSoft");

        var picker = new ComboBox
        {
            ItemsSource = _profiles,
            DisplayMemberBinding = new Binding(nameof(SshProfile.Name)),
            SelectedItem = profile,
            MinWidth = 200,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        Avalonia.Automation.AutomationProperties.SetName(picker, $"Hop {index + 1}");
        picker.SelectionChanged += (_, _) =>
        {
            if (picker.SelectedItem is SshProfile selected && selected.Id != _hops[index])
            {
                _hops[index] = selected.Id;
                Ui.Post(RenderHops);
            }
        };

        var role = Ui.Text((isLast ? "Zielserver" : "Sprungserver") + (profile is null ? "" : $" · {profile.DisplayAddress}"), "MatText.Muted");
        role.Margin = new Thickness(0, 4, 0, 0);
        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { picker, role } };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
        buttons.Children.Add(Ui.IconButton("arrow-up", "Nach oben", () => Swap(index, index - 1), index > 0));
        buttons.Children.Add(Ui.IconButton("arrow-down", "Nach unten", () => Swap(index, index + 1), !isLast));
        buttons.Children.Add(Ui.IconButton("x", "Hop entfernen", () =>
        {
            _hops.RemoveAt(index);
            RenderHops();
        }, _hops.Count > 1));

        var row = new DockPanel();
        DockPanel.SetDock(number, Dock.Left);
        DockPanel.SetDock(buttons, Dock.Right);
        row.Children.Add(number);
        row.Children.Add(buttons);
        row.Children.Add(texts);
        return new Border { Child = row, Padding = new Thickness(14, 12) }.Themed("MatCard");
    }

    private void Swap(int first, int second)
    {
        (_hops[first], _hops[second]) = (_hops[second], _hops[first]);
        RenderHops();
    }

    private void AddHop()
    {
        if (_profiles.Count == 0)
        {
            return;
        }

        var unused = _profiles.FirstOrDefault(profile => !_hops.Contains(profile.Id)) ?? _profiles[0];
        _hops.Add(unused.Id);
        RenderHops();
    }

    private void NewProfile() => _window.OpenPage(new ProfileEditPage(_window, null));

    private void RefreshLocalAddressHint()
    {
        var address = (_localAddress.Text ?? "").Trim();
        _localAddressField.Hint = address.Length == 0 || AccessPolicy.IsLoopback(address)
            ? "127.0.0.1 = nur dieser Rechner."
            : "Achtung: Mit dieser Adresse ist der Tunnel auch für andere Rechner im Netzwerk erreichbar.";
    }

    private static int ParsePort(string? text) => int.TryParse((text ?? "").Trim(), out var port) ? port : 0;

    private void SaveAndThen(Action<TunnelRoute> next)
    {
        var route = _route.Clone();
        route.Name = _name.Text ?? "";
        route.HopProfileIds = [.. _hops];
        route.LocalAddress = _localAddress.Text ?? "";
        route.LocalPort = ParsePort(_localPort.Text);
        route.TargetHost = _targetHost.Text ?? "";
        route.TargetPort = ParsePort(_targetPort.Text);
        route.ConnectOnStartup = _connectOnStartup.IsChecked == true;
        route.ReconnectOnFailure = _reconnect.IsChecked == true;

        TunnelRoute? saved = null;
        if (!_layout.Run(() => saved = RouteService.Save(route)))
        {
            return;
        }

        AppEvents.NotifyDataChanged();
        next(saved!);
    }

    /// <summary>"Speichern und verbinden": a running tunnel uses the old settings, so it starts again.</summary>
    private void Connect(TunnelRoute saved)
    {
        if (TunnelManager.GetStatus(saved.Id).IsActive)
        {
            TunnelManager.Disconnect(saved.Id);
        }

        if (_layout.Run(() => TunnelManager.Connect(saved.Id)))
        {
            _window.ShowConnections(saved.Id);
        }
    }

    private async void Delete()
    {
        if (!await ConfirmDialog.AskAsync(Ui.WindowOf(this), $"„{_route.Name}“ löschen?", "Die Verbindung wird getrennt und gelöscht. Die SSH-Profile bleiben erhalten.", "Löschen", isDestructive: true))
        {
            return;
        }

        if (_layout.Run(() => RouteService.Delete(_route.Id)))
        {
            AppEvents.NotifyDataChanged();
            _window.GoBack();
        }
    }
}
