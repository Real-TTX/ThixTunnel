using System.Windows;
using System.Windows.Controls;
using ThixTunnel.Controls;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Models;
using ThixTunnel.Dialogs;

namespace ThixTunnel.Pages;

/// <summary>Create or change a tunnel route: tabs for the hops, the port forwarding and the automation.</summary>
public partial class TunnelEditPage : UserControl, IDisposable
{
    private readonly TunnelRoute _route;
    private readonly bool _isNew;
    private IReadOnlyList<SshProfile> _profiles;
    private readonly List<Guid> _hops;

    public TunnelEditPage(Guid? routeId)
    {
        InitializeComponent();

        var existing = routeId is null ? null : RouteService.Get(routeId.Value);
        _isNew = existing is null;
        _route = existing ?? new TunnelRoute();
        _profiles = ProfileService.List();
        _hops = [.. _route.HopProfileIds];
        if (_isNew && _profiles.Count > 0)
        {
            _hops.Add(_profiles[0].Id);
        }

        Header.Title = _isNew ? "Neue Tunnelroute" : "Tunnelroute bearbeiten";
        NameBox.Text = _route.Name;
        LocalAddressBox.Text = _route.LocalAddress;
        LocalPortBox.Text = _route.LocalPort > 0 ? _route.LocalPort.ToString() : "";
        TargetHostBox.Text = _route.TargetHost;
        TargetPortBox.Text = _route.TargetPort > 0 ? _route.TargetPort.ToString() : "";
        ConnectOnStartupToggle.IsChecked = _route.ConnectOnStartup;
        ReconnectToggle.IsChecked = _route.ReconnectOnFailure;
        DeleteButton.Visibility = _isNew ? Visibility.Collapsed : Visibility.Visible;
        AutomationHint.Show("Automatische Verbindungen fragen nie nach Passwörtern oder Hostschlüsseln. " +
                            "Für alle Hops werden Schlüssel (SSH-Agent oder Schlüsseldatei) benötigt; unbekannte Hostschlüssel einmal manuell bestätigen.",
                            InfoSeverity.Info);

        if (_profiles.Count == 0)
        {
            MessageBar.Show("Es gibt noch kein SSH-Profil. Lege zuerst ein Profil an („Neues SSH-Profil …“).", InfoSeverity.Warning);
        }

        if (AccessPolicy.IsConfigurationLocked)
        {
            MessageBar.Show("Die Konfiguration ist durch eine Richtlinie gesperrt und kann nicht geändert werden.", InfoSeverity.Warning);
            SaveButton.IsEnabled = SaveConnectButton.IsEnabled = DeleteButton.IsEnabled = false;
        }

        RenderHops();
        AppWindows.DataChanged += OnDataChanged;
        Loaded += (_, _) => NameBox.Focus();
    }

    public void Dispose() => AppWindows.DataChanged -= OnDataChanged;

    private void Finish() => PageWindow.CloseHostOf(this);

    /// <summary>A profile was created in its own window (e.g. through "Neues SSH-Profil …"): offer it in the hop lists.</summary>
    private void OnDataChanged(object? sender, EventArgs e)
    {
        var hadNoProfiles = _profiles.Count == 0;
        _profiles = ProfileService.List();
        _hops.RemoveAll(id => _profiles.All(profile => profile.Id != id));
        if (_hops.Count == 0 && _profiles.Count > 0)
        {
            _hops.Add(_profiles[0].Id);
        }

        if (hadNoProfiles && _profiles.Count > 0)
        {
            MessageBar.Clear();
        }

        RenderHops();
    }

    private void RenderHops()
    {
        HopList.Children.Clear();
        for (var index = 0; index < _hops.Count; index++)
        {
            HopList.Children.Add(CreateHopRow(index));
        }

        AddHopButton.IsEnabled = _profiles.Count > 0 && _hops.Count < 10;
        var last = _profiles.FirstOrDefault(profile => _hops.Count > 0 && profile.Id == _hops[^1]);
        TargetHostField.Hint = last is null ? "" : $"Ziel aus Sicht von {last.Name} – 127.0.0.1 ist dieser Server selbst.";
    }

    private Border CreateHopRow(int index)
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
            Child = new TextBlock { Text = (index + 1).ToString(), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold },
        };
        number.SetResourceReference(Border.BackgroundProperty, "SelectionBrush");

        var picker = new ComboBox { ItemsSource = _profiles, ItemTemplate = (DataTemplate)FindResource("ProfileItemTemplate"), SelectedItem = profile, MinWidth = 140 };
        picker.SelectionChanged += (_, _) =>
        {
            if (picker.SelectedItem is SshProfile selected)
            {
                _hops[index] = selected.Id;
                RenderHops();
            }
        };

        var role = new TextBlock
        {
            Text = (isLast ? "Zielserver" : "Sprungserver") + (profile is null ? "" : $" · {profile.DisplayAddress}"),
            Margin = new Thickness(0, 4, 0, 0),
        };
        role.SetResourceReference(StyleProperty, "MutedText");

        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { picker, role } };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
        buttons.Children.Add(HopButton("IconUp", "Nach oben", index > 0, () => Swap(index, index - 1)));
        buttons.Children.Add(HopButton("IconDown", "Nach unten", !isLast, () => Swap(index, index + 1)));
        buttons.Children.Add(HopButton("IconRemove", "Hop entfernen", _hops.Count > 1, () =>
        {
            _hops.RemoveAt(index);
            RenderHops();
        }));

        var row = new DockPanel();
        DockPanel.SetDock(number, Dock.Left);
        DockPanel.SetDock(buttons, Dock.Right);
        row.Children.Add(number);
        row.Children.Add(buttons);
        row.Children.Add(texts);

        var frame = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 12, 14, 12), Margin = new Thickness(0, 0, 0, 10), Child = row };
        frame.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        frame.SetResourceReference(Border.BackgroundProperty, "InputBrush");
        return frame;
    }

    private Button HopButton(string iconKey, string toolTip, bool isEnabled, Action action)
    {
        var button = new Button { Content = FindResource(iconKey), ToolTip = toolTip, IsEnabled = isEnabled, Margin = new Thickness(6, 0, 0, 0) };
        button.SetResourceReference(StyleProperty, "IconButton");
        button.Click += (_, _) => action();
        return button;
    }

    private void Swap(int first, int second)
    {
        (_hops[first], _hops[second]) = (_hops[second], _hops[first]);
        RenderHops();
    }

    private void OnAddHopClick(object sender, RoutedEventArgs e)
    {
        var unused = _profiles.FirstOrDefault(profile => !_hops.Contains(profile.Id)) ?? _profiles[0];
        _hops.Add(unused.Id);
        RenderHops();
    }

    private void OnNewProfileClick(object sender, RoutedEventArgs e) => AppWindows.ShowProfileEditor(null);

    private void OnLocalAddressChanged(object sender, TextChangedEventArgs e) =>
        LocalAddressField.Hint = AccessPolicy.IsLoopback(LocalAddressBox.Text.Trim()) || LocalAddressBox.Text.Trim().Length == 0
            ? "127.0.0.1 = nur dieser PC."
            : "Achtung: Mit dieser Adresse ist der Tunnel auch für andere Rechner im Netzwerk erreichbar.";

    private TunnelRoute? Save()
    {
        var route = _route.Clone();
        route.Name = NameBox.Text;
        route.HopProfileIds = [.. _hops];
        route.LocalAddress = LocalAddressBox.Text;
        route.LocalPort = PageActions.ParsePort(LocalPortBox.Text);
        route.TargetHost = TargetHostBox.Text;
        route.TargetPort = PageActions.ParsePort(TargetPortBox.Text);
        route.ConnectOnStartup = ConnectOnStartupToggle.IsChecked;
        route.ReconnectOnFailure = ReconnectToggle.IsChecked;

        TunnelRoute? saved = null;
        if (!PageActions.Run(() => saved = RouteService.Save(route), MessageBar, Form))
        {
            SelectTabWithError();
            return null;
        }

        AppWindows.NotifyDataChanged();
        return saved;
    }

    private void SelectTabWithError()
    {
        for (var index = 0; index < Tabs.Items.Count; index++)
        {
            if (Tabs.Items[index] is TabItem { Content: DependencyObject content } && HasError(content))
            {
                Tabs.SelectedIndex = index;
                return;
            }
        }
    }

    private static bool HasError(DependencyObject root) =>
        LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()
            .Any(child => child is FormField { Error.Length: > 0 } || HasError(child));

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (Save() is not null)
        {
            Finish();
        }
    }

    private void OnSaveConnectClick(object sender, RoutedEventArgs e)
    {
        if (Save() is not { } saved)
        {
            return;
        }

        // A running tunnel uses the old settings: restart it.
        if (TunnelManager.GetStatus(saved.Id).IsActive)
        {
            TunnelManager.Disconnect(saved.Id);
        }

        if (PageActions.Run(() => TunnelManager.Connect(saved.Id), MessageBar))
        {
            Finish();
        }
    }

    private void OnBackClick(object sender, RoutedEventArgs e) => PageWindow.CloseHostOf(this);

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (ConfirmDialog.Ask(Window.GetWindow(this), "Tunnelroute löschen", $"„{_route.Name}“ wird getrennt und gelöscht. Die SSH-Profile bleiben erhalten.", "Löschen", isDestructive: true) &&
            PageActions.Run(() => RouteService.Delete(_route.Id), MessageBar))
        {
            AppWindows.NotifyDataChanged();
            Finish();
        }
    }
}
