using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using MatDesktop;
using ThixTunnel.Controls;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Infrastructure;

namespace ThixTunnel.Pages;

/// <summary>
/// About ThixTunnel: reached through the version at the foot of the menu (owner's rule, DESK-04). Logo and word mark, the
/// version as everywhere ("Version 1.0.0 · release", SETUP-07), the state of updates, ssh, the data folder.
/// </summary>
public sealed class AboutPage : UserControl
{
    private readonly MatIcon _updateIcon = new() { Icon = "download", Size = 20, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _updateTitle = new() { FontWeight = FontWeight.SemiBold };
    private readonly TextBlock _updateText = Ui.Text("", "MatText.Muted");
    private readonly Button _updateButton;

    public AboutPage()
    {
        _updateButton = Ui.Button("download", "Nach Updates suchen …", click: OpenUpdates);
        var configuredSsh = SettingsService.Get().SshExecutablePath;
        SystemText = new SelectableTextBlock { Text = $"{RuntimeInformation.OSDescription} ({UpdateService.PlatformKey}), .NET {Environment.Version}", TextWrapping = TextWrapping.Wrap };
        SshText = new SelectableTextBlock { Text = Ui.Breakable(configuredSsh.Length > 0 ? configuredSsh : Ui.SystemSshPath ?? "OpenSSH-Client nicht gefunden"), TextWrapping = TextWrapping.Wrap };
        DataText = new SelectableTextBlock { Text = Ui.Breakable(AppPaths.DataDirectory), TextWrapping = TextWrapping.Wrap };
        VersionText = new SelectableTextBlock { Text = MatVersion.Format(AppInfo.Version, AppInfo.Channel), FontSize = MatTokens.Type.FsSm };

        var wordMark = new TextBlock { FontSize = MatTokens.Type.Fs2xl, FontWeight = FontWeight.SemiBold };
        var tunnel = new Run("Tunnel");
        tunnel.Bind(TextElement.ForegroundProperty, wordMark.GetResourceObservable("Mat.AccentText"));
        wordMark.Inlines = [new Run("Thix"), tunnel];

        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 18 };
        head.Children.Add(new Image { Source = Ui.Logo, Width = 88, Height = 88 });
        RenderOptions.SetBitmapInterpolationMode(head.Children[0], Avalonia.Media.Imaging.BitmapInterpolationMode.HighQuality);
        head.Children.Add(new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 4,
            Children = { wordMark, Ui.Text("SSH-Tunnel für Windows und macOS", "MatText.Muted"), VersionText },
        });

        var update = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        var updateTexts = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2, Children = { _updateTitle, _updateText } };
        Grid.SetColumn(updateTexts, 1);
        Grid.SetColumn(_updateButton, 2);
        _updateButton.Margin = new Thickness(12, 0, 0, 0);
        _updateButton.VerticalAlignment = VerticalAlignment.Center;
        update.Children.Add(_updateIcon);
        update.Children.Add(updateTexts);
        update.Children.Add(_updateButton);
        var updateBox = new Border { Child = update, Padding = new Thickness(14, 12) }
            .Use(Border.BackgroundProperty, "Mat.Surface2").Use(Border.CornerRadiusProperty, "Mat.RadiusLg");

        var facts = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto") };
        AddFact(facts, 0, "Build", new TextBlock { Text = AppInfo.BuildNumber > 0 ? $"#{AppInfo.BuildNumber}" : "–" });
        AddFact(facts, 1, "System", SystemText);
        AddFact(facts, 2, "ssh", SshText);
        AddFact(facts, 3, "Daten", DataText);

        var card = new StackPanel { Spacing = 18 };
        card.Children.Add(head);
        card.Children.Add(updateBox);
        card.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 21,
            Text = "SSH-Profile, mehrere Hops und lokale Portweiterleitungen: ThixTunnel baut Tunnel mit dem OpenSSH des Systems auf (ssh -J … -L …), " +
                   "verbindet auf Wunsch automatisch und nach Abbrüchen erneut – im Infobereich (Windows) oder in der Menüleiste (macOS).",
        });
        card.Children.Add(facts);
        card.Children.Add(Ui.Text("© Matthias Schmoldt. Gestaltung und Symbole: MatDesign (Symbole: Lucide, ISC-Lizenz).", "MatText.Faint"));

        var page = new StackPanel { Spacing = 18, MaxWidth = 680, HorizontalAlignment = HorizontalAlignment.Left };
        page.Children.Add(new TextBlock { Text = "Über ThixTunnel" }.Themed("MatText.PageTitle"));
        page.Children.Add(new Border { Child = card, Padding = new Thickness(24) }.Themed("MatCard"));
        page.Children.Add(new ActionBar
        {
            Children =
            {
                Ui.Button("external", "Projekt auf GitHub", click: () => Ui.OpenExternal($"https://github.com/{AppInfo.Repository}")),
                Ui.Button("route", "Zu den Verbindungen", click: () => (Ui.WindowOf(this) as MainWindow)?.ShowConnections()),
            },
        });
        Content = Ui.Page(page);
        AttachedToVisualTree += (_, _) => RefreshUpdate();
        UpdateCoordinator.Checked += (_, _) => Ui.Post(RefreshUpdate);
    }

    public SelectableTextBlock VersionText { get; }

    public SelectableTextBlock SystemText { get; }

    public SelectableTextBlock SshText { get; }

    public SelectableTextBlock DataText { get; }

    private static void AddFact(Grid grid, int row, string label, Control value)
    {
        var name = Ui.Text(label, "MatText.Muted", wrap: false);
        name.Margin = new Thickness(0, 3, 24, 3);
        value.Margin = new Thickness(0, 3);
        Grid.SetRow(name, row);
        Grid.SetRow(value, row);
        Grid.SetColumn(value, 1);
        grid.Children.Add(name);
        grid.Children.Add(value);
    }

    /// <summary>What the last update check found; the button leads to Einstellungen › Updates (install or check there).</summary>
    private void RefreshUpdate()
    {
        var result = UpdateService.LastResult;
        var available = result is { IsUpdateAvailable: true, Manifest: not null };
        _updateTitle.Text = available ? $"Version {result!.Manifest!.Version} ist verfügbar" : AppInfo.IsLocalBuild ? "Lokaler Build" : "Updates";
        _updateText.Text = available
            ? "Installieren dauert nur einen Moment, aktive Tunnel werden danach wieder verbunden."
            : AppInfo.IsLocalBuild
                ? MatVersion.LocalNote(AppInfo.Channel) ?? ""
                : result is null
                    ? "Noch nicht geprüft."
                    : $"{result.Message} Zuletzt geprüft um {(UpdateService.LastCheck is { } last ? last.ToLocalTime().ToString("HH:mm") : "–")}.";
        ((IconLabel)_updateButton.Content!).Text = available ? "Update installieren …" : "Nach Updates suchen …";
        _updateButton.IsVisible = available || UpdateService.CanUpdate;
        if (available)
        {
            _updateButton.Themed("MatButton.Primary");
        }
        else
        {
            _updateButton.ClearValue(ThemeProperty);
        }

        _updateIcon.Use(MatIcon.ForegroundProperty, available ? "Mat.AccentText" : "Mat.TextMuted");
    }

    private void OpenUpdates()
    {
        if (Ui.WindowOf(this) is not MainWindow window)
        {
            return;
        }

        window.ShowSettings(SettingsPage.UpdatesTab);
        if (UpdateService.LastResult is not { IsUpdateAvailable: true })
        {
            window.SettingsPage.CheckForUpdatesNow();
        }
    }
}
