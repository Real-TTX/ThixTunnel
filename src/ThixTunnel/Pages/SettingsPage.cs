using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using MatDesktop;
using ThixTunnel.Controls;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Infrastructure;
using ThixTunnel.Core.Models;
using ThixTunnel.Dialogs;

namespace ThixTunnel.Pages;

/// <summary>
/// The settings in tabs: Allgemein | Verbindungen | SSH-Profile | Import / Export | Updates | Erweitert. The connections and
/// profiles are managed here only (owner's decision). A switch applies at once (like macOS); a setting a policy decides is
/// shown turned off with "Von deiner Organisation festgelegt" (DESK-10).
/// </summary>
public sealed class SettingsPage : UserControl, IRefreshable
{
    public const string GeneralTab = "Allgemein";
    public const string RoutesTab = "Verbindungen";
    public const string ProfilesTab = "SSH-Profile";
    public const string TransferTab = "Import / Export";
    public const string UpdatesTab = "Updates";
    public const string AdvancedTab = "Erweitert";

    private static readonly FilePickerFileType TransferFileType = new("ThixTunnel-Export") { Patterns = ["*.thixtunnel.json", "*.json"] };

    private readonly MainWindow _window;
    private readonly TabControl _tabs = new() { Margin = new Thickness(0, 12, 0, 0) };
    private readonly InfoBar _pageMessage = new() { Margin = new Thickness(0, 14, 0, 0) };
    private readonly ToggleSwitch _autostart;
    private readonly ToggleSwitch _notify;
    private readonly ComboBox _appearance;
    private readonly ToggleSwitch _checkUpdates;
    private readonly ToggleSwitch _installAutomatically;
    private readonly ToggleSwitch _replaceOnImport;
    private readonly InfoBar _transferResult = new();
    private readonly InfoBar _updateStatus = new();
    private readonly ProgressBar _download = new() { Minimum = 0, Maximum = 1, IsVisible = false, Margin = new Thickness(0, 0, 0, 12) };
    private readonly Button _checkButton;
    private readonly Button _installButton;
    private readonly TextBox _sshPath = new();
    private readonly MatFormField _sshPathField;
    private bool _isLoading;
    private UpdateManifest? _availableUpdate;

    public SettingsPage(MainWindow window)
    {
        _window = window;
        Routes = new RouteListPage(window);
        Profiles = new ProfileListPage(window);
        var locked = AccessPolicy.IsConfigurationLocked;

        _autostart = Ui.Switch("Bei der Anmeldung starten", false, _ => SaveSwitches());
        _notify = Ui.Switch("Bei Verbindungsfehlern benachrichtigen", false, _ => SaveSwitches());
        _appearance = new ComboBox { MinWidth = 170, ItemsSource = new[] { "Wie das System", "Hell", "Dunkel" } };
        Avalonia.Automation.AutomationProperties.SetName(_appearance, "Erscheinungsbild");
        _appearance.SelectionChanged += (_, _) => SaveSwitches();
        _checkUpdates = Ui.Switch("Automatisch nach Updates suchen", false, _ => SaveSwitches());
        _installAutomatically = Ui.Switch("Updates automatisch installieren", false, _ => SaveSwitches());
        _replaceOnImport = Ui.Switch("Vorhandene ersetzen", false);
        _checkButton = Ui.Button("refresh", "Jetzt prüfen", click: CheckForUpdates);
        _installButton = Ui.Button("download", "Update installieren", "MatButton.Primary", InstallUpdate);
        _sshPathField = Ui.Field("ssh-Programm (optional)", SshPathInput(), nameof(AppSettings.SshExecutablePath));

        var page = new StackPanel();
        page.Children.Add(new TextBlock { Text = "Einstellungen" }.Themed("MatText.PageTitle"));
        if (locked)
        {
            page.Children.Add(Ui.Text("Von deiner Organisation festgelegt: die Konfiguration ist gesperrt, was eine Richtlinie bestimmt, ist hier ausgeschaltet.", "MatText.Muted"));
        }

        page.Children.Add(_pageMessage);
        page.Children.Add(_tabs);
        AddTab(GeneralTab, GeneralContent());
        AddTab(RoutesTab, Routes);
        AddTab(ProfilesTab, Profiles);
        AddTab(TransferTab, TransferContent(locked));
        AddTab(UpdatesTab, UpdatesContent());
        AddTab(AdvancedTab, AdvancedContent());
        _tabs.SelectionChanged += (_, e) =>
        {
            if (e.Source == _tabs)
            {
                _pageMessage.Hide();
            }
        };

        Content = Ui.Page(page);
        UpdateCoordinator.Checked += (_, result) => Ui.Post(() => ShowUpdateResult(result));
        Refresh();
    }

    public RouteListPage Routes { get; }

    public ProfileListPage Profiles { get; }

    public TabControl Tabs => _tabs;

    public ToggleSwitch AutostartSwitch => _autostart;

    public ComboBox AppearanceChoice => _appearance;

    public TextBox SshPathBox => _sshPath;

    public MatFormField SshPathField => _sshPathField;

    public InfoBar PageMessage => _pageMessage;

    public void SelectTab(string header) =>
        _tabs.SelectedItem = _tabs.Items.OfType<TabItem>().FirstOrDefault(tab => Equals(tab.Header, header)) ?? _tabs.SelectedItem;

    /// <summary>Reads the settings again (shown, after an import); the lists of the tabs too.</summary>
    public void Refresh()
    {
        _isLoading = true;
        var settings = SettingsService.Get();
        var locked = AccessPolicy.IsConfigurationLocked;
        _autostart.IsChecked = settings.StartWithWindows;
        _notify.IsChecked = settings.NotifyOnErrors;
        _appearance.SelectedIndex = (int)settings.Appearance;
        _checkUpdates.IsChecked = settings.CheckForUpdates;
        _installAutomatically.IsChecked = settings.InstallUpdatesAutomatically;
        _sshPath.Text = settings.SshExecutablePath;
        foreach (var control in new Control[] { _autostart, _notify, _appearance, _checkUpdates, _sshPath })
        {
            control.IsEnabled = !locked;
        }

        _installAutomatically.IsEnabled = !locked && settings.CheckForUpdates;
        _checkUpdates.IsEnabled = !locked && UpdateService.CanUpdate;
        _installAutomatically.IsEnabled &= UpdateService.CanUpdate;
        _checkButton.IsEnabled = UpdateService.CanUpdate;
        ShowUpdateResult(UpdateService.LastResult);
        _isLoading = false;
        Routes.Refresh();
        Profiles.Refresh();
    }

    /// <summary>Checks for an update now (tray, About).</summary>
    public void CheckForUpdatesNow() => CheckForUpdates();

    private void AddTab(string header, Control content)
    {
        var tab = new TabItem { Header = header, Content = new StackPanel { Margin = new Thickness(0, 14, 0, 0), Children = { content } } };
        Avalonia.Automation.AutomationProperties.SetName(tab, header);
        _tabs.Items.Add(tab);
    }

    private Control GeneralContent()
    {
        var rows = new StackPanel();
        if (AutostartService.IsSupported)
        {
            rows.Children.Add(Ui.Row("Bei der Anmeldung starten",
                "ThixTunnel startet unsichtbar im Infobereich / in der Menüleiste; Verbindungen mit Auto-Verbindung werden aufgebaut.", _autostart));
        }

        rows.Children.Add(Ui.Row("Bei Verbindungsfehlern benachrichtigen", "Ein kurzer Hinweis am Bildschirmrand, wenn ein Tunnel nicht verbinden kann.", _notify));
        rows.Children.Add(Ui.Row("Erscheinungsbild", "Hell, dunkel oder wie das System – es wechselt mit ihm.", _appearance, last: true));
        var hint = new InfoBar { Margin = new Thickness(0, 14, 0, 0) };
        hint.Show("Automatisches Verbinden legst du je Verbindung fest (Verbindungen › Bearbeiten › Automatisierung). Das Fenster schließt sich nur: die Tunnel laufen weiter, „Beenden“ im Menü des Symbols trennt alle.", InfoSeverity.Info);
        return new StackPanel { MaxWidth = 820, HorizontalAlignment = HorizontalAlignment.Left, Children = { rows, hint } };
    }

    private Control TransferContent(bool locked)
    {
        var export = new StackPanel();
        export.Children.Add(Ui.Text("Exportieren", "MatText.SectionTitle"));
        export.Children.Add(Spaced(Ui.Text("Speichert alle SSH-Profile und Verbindungen in einer Datei – für den Umzug auf einen anderen Rechner, zum Teilen oder als Sicherung. Passwörter werden nie gespeichert, Schlüsseldateien nur als Pfad.", "MatText.Muted")));
        export.Children.Add(new ActionBar { Children = { Ui.Button("upload", "Exportieren …", "MatButton.Primary", Export) } });

        var import = new StackPanel();
        import.Children.Add(Ui.Text("Importieren", "MatText.SectionTitle"));
        import.Children.Add(Spaced(Ui.Text("Übernimmt SSH-Profile und Verbindungen aus einer Exportdatei. Vorhandene Einträge (gleiche ID oder gleicher Name) bleiben unverändert – außer du wählst „Vorhandene ersetzen“.", "MatText.Muted")));
        import.Children.Add(Ui.Row("Vorhandene ersetzen", "Gleichnamige Profile und Verbindungen mit den Angaben aus der Datei überschreiben.", _replaceOnImport, last: true));
        var importButton = Ui.Button("download", "Importieren …", click: Import);
        importButton.IsEnabled = !locked;
        import.Children.Add(new ActionBar { Margin = new Thickness(0, 10, 0, 0), Children = { importButton } });

        return new StackPanel { MaxWidth = 820, HorizontalAlignment = HorizontalAlignment.Left, Children = { _transferResult, Ui.Card(export), Ui.Card(import) } };
    }

    private Control UpdatesContent()
    {
        var installed = new StackPanel();
        installed.Children.Add(Ui.Text("Installierte Version", "MatText.SectionTitle"));
        installed.Children.Add(new SelectableTextBlock { Text = MatVersion.Format(AppInfo.Version, AppInfo.Channel), FontSize = MatTokens.Type.FsLg });
        var note = AppInfo.Channel switch
        {
            "release" => $"Kanal Release · Build {AppInfo.BuildNumber}",
            "nightly" => $"Kanal Nightly (Entwicklungsstand) · Build {AppInfo.BuildNumber}",
            _ => MatVersion.LocalNote(AppInfo.Channel) ?? "Lokaler Build",
        };
        if (AccessPolicy.AreUpdatesDisabled)
        {
            note += " · Updates von deiner Organisation abgeschaltet.";
        }

        var noteText = Ui.Text(note, "MatText.Muted");
        noteText.Margin = new Thickness(0, 4, 0, 14);
        installed.Children.Add(noteText);
        installed.Children.Add(_updateStatus);
        installed.Children.Add(_download);
        var releaseNotes = Ui.Button("external", "Versionshinweise auf GitHub", "MatButton.Link", () => Ui.OpenExternal(UpdateService.ReleasePageUrl(AppInfo.Channel)));
        releaseNotes.IsVisible = !AppInfo.IsLocalBuild;
        installed.Children.Add(new ActionBar { Children = { _checkButton, _installButton, releaseNotes } });

        var options = new StackPanel();
        options.Children.Add(Ui.Row("Automatisch nach Updates suchen", "Beim Start und alle 6 Stunden.", _checkUpdates));
        options.Children.Add(Ui.Row("Updates automatisch installieren",
            "Voll-automatisches Update: ThixTunnel installiert neue Versionen selbst und startet neu. Aktive Tunnel werden danach wieder verbunden.", _installAutomatically, last: true));

        return new StackPanel { MaxWidth = 820, HorizontalAlignment = HorizontalAlignment.Left, Children = { Ui.Card(installed), options } };
    }

    private Control AdvancedContent()
    {
        var ssh = new StackPanel();
        var systemSsh = Ui.SystemSshPath;
        _sshPathField.Hint = OperatingSystem.IsWindows()
            ? "Leer = Windows OpenSSH (" + (systemSsh ?? "nicht gefunden – optionales Windows-Feature „OpenSSH-Client“ installieren") + ")"
            : "Leer = OpenSSH des Systems (" + (systemSsh ?? "nicht gefunden") + ")";
        ssh.Children.Add(_sshPathField);
        ssh.Children.Add(new ActionBar
        {
            Children =
            {
                Ui.Button("folder-open", "Datenordner öffnen", click: () => Ui.OpenFolder(AppPaths.DataDirectory)),
                Ui.Button("file-text", "Protokolle öffnen", click: () => Ui.OpenFolder(AppPaths.LogDirectory)),
            },
        });

        var policies = new StackPanel();
        policies.Children.Add(Ui.Text("Richtlinien", "MatText.SectionTitle"));
        var policyHint = OperatingSystem.IsMacOS()
            ? $"Administratoren setzen diese Werte (true) mit einem Konfigurationsprofil für die Domain {AccessPolicy.MacPolicyDomain}."
            : @"Administratoren setzen diese Werte als DWORD = 1 unter HKLM\SOFTWARE\Policies\ThixTunnel.";
        policies.Children.Add(Spaced(Ui.Text(policyHint, "MatText.Muted")));
        policies.Children.Add(new MatFacts
        {
            Facts =
            [
                new MatFact("LockConfiguration", AccessPolicy.IsConfigurationLocked ? "an – Konfiguration gesperrt" : "aus"),
                new MatFact("DenyNetworkBinding", AccessPolicy.IsNetworkBindingDenied ? "an – nur Loopback-Adressen" : "aus"),
                new MatFact("DisableUpdates", AccessPolicy.AreUpdatesDisabled ? "an – keine Updates" : "aus"),
            ],
        });

        return new StackPanel { MaxWidth = 820, HorizontalAlignment = HorizontalAlignment.Left, Children = { Ui.Card(ssh), Ui.Card(policies) } };
    }

    private static Control Spaced(Control control)
    {
        control.Margin = new Thickness(0, 2, 0, 12);
        return control;
    }

    /// <summary>The path of ssh: a text box, Durchsuchen …, Übernehmen; Enter or leaving the box applies it too.</summary>
    private Control SshPathInput()
    {
        var browse = Ui.Button("folder-open", "Durchsuchen …", click: BrowseSsh);
        var apply = Ui.Button("check", "Übernehmen", click: SaveSshPath);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(8, 0, 0, 0), Children = { browse, apply } };
        var input = new DockPanel();
        DockPanel.SetDock(buttons, Dock.Right);
        input.Children.Add(buttons);
        input.Children.Add(_sshPath);
        _sshPath.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                e.Handled = true;
                SaveSshPath();
            }
        };
        return input;
    }

    /// <summary>Saves what the switches say; a business error shows on top and the switches show the saved state again.</summary>
    private void SaveSwitches()
    {
        if (_isLoading)
        {
            return;
        }

        var settings = SettingsService.Get();
        settings.StartWithWindows = _autostart.IsChecked == true;
        settings.NotifyOnErrors = _notify.IsChecked == true;
        settings.Appearance = (AppearanceChoice)Math.Max(0, _appearance.SelectedIndex);
        settings.CheckForUpdates = _checkUpdates.IsChecked == true;
        settings.InstallUpdatesAutomatically = _installAutomatically.IsChecked == true;
        Save(settings);
    }

    private void SaveSshPath()
    {
        var settings = SettingsService.Get();
        settings.SshExecutablePath = _sshPath.Text ?? "";
        if (Save(settings))
        {
            _pageMessage.Show("ssh-Programm übernommen.", InfoSeverity.Success);
        }
    }

    private bool Save(AppSettings settings)
    {
        _pageMessage.Hide();
        _sshPathField.Error = null;
        try
        {
            SettingsService.Save(settings, App.ExecutablePath);
            App.ApplyAppearance(settings.Appearance);
            Refresh();
            return true;
        }
        catch (BusinessException exception)
        {
            if (exception.Field == nameof(AppSettings.SshExecutablePath))
            {
                _sshPathField.Error = exception.Message;
                SelectTab(AdvancedTab);
            }
            else
            {
                _pageMessage.Show(exception.Message, InfoSeverity.Error);
                Refresh();
            }

            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.Error("Saving the settings failed", exception);
            _pageMessage.Show($"Speichern nicht möglich: {exception.Message}", InfoSeverity.Error);
            Refresh();
            return false;
        }
    }

    private void ShowUpdateResult(UpdateCheckResult? result)
    {
        _availableUpdate = result?.Manifest;
        _installButton.IsVisible = result?.IsUpdateAvailable == true;
        if (result is null)
        {
            _updateStatus.Hide();
            return;
        }

        var checkedAt = UpdateService.LastCheck is { } at ? $" (geprüft um {at.ToLocalTime():HH:mm})" : "";
        _updateStatus.Show(result.Message + checkedAt, result.IsUpdateAvailable ? InfoSeverity.Warning : InfoSeverity.Success);
    }

    private async void CheckForUpdates()
    {
        _checkButton.IsEnabled = false;
        _updateStatus.Show("Suche nach Updates …", InfoSeverity.Info);
        try
        {
            ShowUpdateResult(await UpdateCoordinator.CheckAsync());
        }
        catch (Exception exception) when (exception is BusinessException or HttpRequestException or IOException or TaskCanceledException)
        {
            _updateStatus.Show(exception.Message, InfoSeverity.Error);
        }
        finally
        {
            _checkButton.IsEnabled = UpdateService.CanUpdate;
        }
    }

    private async void InstallUpdate()
    {
        if (_availableUpdate is null)
        {
            return;
        }

        _installButton.IsEnabled = _checkButton.IsEnabled = false;
        _download.IsVisible = true;
        _updateStatus.Show($"Lade Version {_availableUpdate.Version} …", InfoSeverity.Info);
        try
        {
            await UpdateCoordinator.InstallAsync(_availableUpdate, new Progress<double>(value => _download.Value = value), showWindow: true);
        }
        catch (Exception exception) when (exception is BusinessException or HttpRequestException or IOException)
        {
            _updateStatus.Show($"Update fehlgeschlagen: {exception.Message}", InfoSeverity.Error);
            _installButton.IsEnabled = _checkButton.IsEnabled = true;
            _download.IsVisible = false;
        }
    }

    private async void BrowseSsh()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            return;
        }

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "ssh-Programm wählen",
            FileTypeFilter = OperatingSystem.IsWindows()
                ? [new FilePickerFileType("ssh.exe") { Patterns = ["ssh.exe"] }, new FilePickerFileType("Programme") { Patterns = ["*.exe"] }]
                : [FilePickerFileTypes.All],
        });
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            _sshPath.Text = path;
            SaveSshPath();
        }
    }

    private async void Export()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            return;
        }

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "SSH-Profile und Verbindungen exportieren",
            SuggestedFileName = TransferService.DefaultFileName,
            FileTypeChoices = [TransferFileType],
            DefaultExtension = "json",
        });
        if (file?.TryGetLocalPath() is not { } path)
        {
            return;
        }

        try
        {
            var (profiles, routes) = TransferService.Export(path);
            _transferResult.Show($"{profiles} Profile und {routes} Verbindungen exportiert nach {path}.", InfoSeverity.Success);
        }
        catch (Exception exception) when (exception is BusinessException or IOException or UnauthorizedAccessException)
        {
            _transferResult.Show(exception.Message, InfoSeverity.Error);
        }
    }

    private async void Import()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            return;
        }

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "SSH-Profile und Verbindungen importieren",
            FileTypeFilter = [TransferFileType],
        });
        if (files.Count == 0 || files[0].TryGetLocalPath() is not { } path)
        {
            return;
        }

        try
        {
            var file = TransferService.Read(path);
            var replace = _replaceOnImport.IsChecked == true;
            var question = $"Die Datei enthält {file.Profiles.Count} Profile und {file.Routes.Count} Verbindungen " +
                           $"(exportiert am {file.ExportedAt.LocalDateTime:dd.MM.yyyy HH:mm}). " +
                           (replace ? "Vorhandene Einträge mit gleichem Namen werden ersetzt." : "Vorhandene Einträge mit gleichem Namen bleiben unverändert.");
            if (!await ConfirmDialog.AskAsync(Ui.WindowOf(this), "Importieren?", question, "Importieren"))
            {
                return;
            }

            var result = TransferService.Import(path, replace);
            AppEvents.NotifyDataChanged();
            Refresh();
            var problems = result.Problems.Count == 0 ? "" : "\n\n" + string.Join("\n", result.Problems.Select(problem => "• " + problem));
            _transferResult.Show(result.Summary + problems, result.Problems.Count == 0 ? InfoSeverity.Success : InfoSeverity.Warning);
        }
        catch (Exception exception) when (exception is BusinessException or IOException or UnauthorizedAccessException)
        {
            _transferResult.Show(exception.Message, InfoSeverity.Error);
        }
    }
}
