using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ThixTunnel.Controls;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Infrastructure;
using ThixTunnel.Core.Models;
using ThixTunnel.Dialogs;

namespace ThixTunnel.Pages;

/// <summary>
/// The settings window: start and behaviour, connections, SSH profiles, import/export, updates, advanced.
/// "Speichern" belongs to the settings tabs; the connection, profile and import/export tabs act right away and hide it.
/// </summary>
public partial class SettingsPage : UserControl, IDisposable
{
    public const int GeneralTab = 0;
    public const int RoutesTab = 1;
    public const int ProfilesTab = 2;
    public const int TransferTab = 3;
    public const int UpdatesTab = 4;
    public const int AdvancedTab = 5;

    private static readonly FilePickerFileType TransferFileType = new("ThixTunnel-Export") { Patterns = ["*.thixtunnel.json", "*.json"] };

    private readonly RouteListPage _routes = new();
    private readonly ProfileListPage _profiles = new(null) { IsEmbedded = true };
    private UpdateManifest? _availableUpdate;

    /// <summary>For the XAML loader and previewer.</summary>
    public SettingsPage()
        : this(GeneralTab)
    {
    }

    public SettingsPage(int tabIndex)
    {
        InitializeComponent();
        RoutesHost.Content = _routes;
        ProfilesHost.Content = _profiles;
        Tabs.SelectedIndex = tabIndex;
        RefreshSaveButton();
        ImportButton.IsEnabled = !AccessPolicy.IsConfigurationLocked;
        CheckUpdatesToggle.Changed += OnCheckUpdatesChanged;

        var settings = SettingsService.Get();
        StartWithWindowsToggle.IsChecked = settings.StartWithWindows;
        StartWithWindowsToggle.IsVisible = AutostartService.IsSupported;
        StartInTrayToggle.IsChecked = settings.StartInTray;
        KeepRunningToggle.IsChecked = settings.KeepRunningInTray;
        NotifyToggle.IsChecked = settings.NotifyOnErrors;
        CheckUpdatesToggle.IsChecked = settings.CheckForUpdates;
        AutoInstallToggle.IsChecked = settings.InstallUpdatesAutomatically;
        AutoInstallToggle.IsEnabled = settings.CheckForUpdates;
        SshPathBox.Text = settings.SshExecutablePath;
        var systemSsh = Ui.SystemSshPath;
        SshPathField.Hint = OperatingSystem.IsWindows()
            ? "Leer = Windows OpenSSH (" + (systemSsh ?? "nicht gefunden – optionales Windows-Feature „OpenSSH-Client“ installieren") + ")"
            : "Leer = OpenSSH des Systems (" + (systemSsh ?? "nicht gefunden") + ")";
        AutoConnectInfo.Show("Automatisches Verbinden wird je Verbindung festgelegt (Verbindungen › Bearbeiten › Automatisierung).", InfoSeverity.Info);

        VersionText.Text = AppInfo.Version;
        ChannelText.Text = AppInfo.Channel switch
        {
            "release" => $"Kanal: Release · Build {AppInfo.BuildNumber}",
            "nightly" => $"Kanal: Nightly (Entwicklungsstand) · Build {AppInfo.BuildNumber}",
            _ => "Lokaler Build – Updates werden nur für Release- und Nightly-Builds angeboten.",
        };
        ShowUpdateResult(UpdateService.LastResult);

        var updatesPossible = UpdateService.CanUpdate;
        CheckButton.IsEnabled = updatesPossible;
        UpdateOptionsTile.IsEnabled = updatesPossible;
        ReleasePageButton.IsVisible = !AppInfo.IsLocalBuild;

        var policyHint = OperatingSystem.IsMacOS()
            ? $"Administratoren setzen diese Werte (true) mit einem Konfigurationsprofil für die Domain {AccessPolicy.MacPolicyDomain} (LockConfiguration, DenyNetworkBinding, DisableUpdates)."
            : @"Administratoren setzen diese Werte als DWORD = 1 unter HKLM\SOFTWARE\Policies\ThixTunnel (LockConfiguration, DenyNetworkBinding, DisableUpdates).";
        PolicyText.Text = string.Join(Environment.NewLine,
            $"Konfiguration gesperrt: {YesNo(AccessPolicy.IsConfigurationLocked)}",
            $"Nur Loopback-Adressen: {YesNo(AccessPolicy.IsNetworkBindingDenied)}",
            $"Updates deaktiviert: {YesNo(AccessPolicy.AreUpdatesDisabled)}",
            "",
            policyHint);

        if (AccessPolicy.IsConfigurationLocked)
        {
            MessageBar.Show("Die Einstellungen sind durch eine Richtlinie gesperrt.", InfoSeverity.Warning);
            SaveButton.IsEnabled = false;
        }
    }

    public void Dispose()
    {
        _routes.Dispose();
        _profiles.Dispose();
    }

    public void SelectTab(int tabIndex) => Tabs.SelectedIndex = tabIndex;

    private void OnTabChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.Source != Tabs)
        {
            return; // combo boxes and lists inside the tabs raise the same event
        }

        RefreshSaveButton();
    }

    private void RefreshSaveButton() => SaveButton.IsVisible = Tabs.SelectedIndex is not (RoutesTab or ProfilesTab or TransferTab);

    private async void OnExportClick(object? sender, RoutedEventArgs e)
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

        var exported = (Profiles: 0, Routes: 0);
        if (PageActions.Run(() => exported = TransferService.Export(path), TransferResult))
        {
            TransferResult.Show($"{exported.Profiles} Profile und {exported.Routes} Verbindungen exportiert nach {path}.", InfoSeverity.Success);
        }
    }

    private async void OnImportClick(object? sender, RoutedEventArgs e)
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

        TransferFile? file = null;
        if (!PageActions.Run(() => file = TransferService.Read(path), TransferResult))
        {
            return;
        }

        var replace = ReplaceExistingToggle.IsChecked;
        var question = $"Die Datei enthält {file!.Profiles.Count} Profile und {file.Routes.Count} Verbindungen " +
                       $"(exportiert am {file.ExportedAt.LocalDateTime:dd.MM.yyyy HH:mm}). " +
                       (replace ? "Vorhandene Einträge mit gleichem Namen werden ersetzt." : "Vorhandene Einträge mit gleichem Namen bleiben unverändert.");
        if (!await ConfirmDialog.AskAsync(Ui.WindowOf(this), "Importieren", question, "Importieren"))
        {
            return;
        }

        ImportResult? result = null;
        if (!PageActions.Run(() => result = TransferService.Import(path, replace), TransferResult))
        {
            return;
        }

        AppWindows.NotifyDataChanged();
        var problems = result!.Problems.Count == 0 ? "" : "\n\n" + string.Join("\n", result.Problems.Select(problem => "• " + problem));
        TransferResult.Show(result.Summary + problems, result.Problems.Count == 0 ? InfoSeverity.Success : InfoSeverity.Warning);
    }

    private static string YesNo(bool value) => value ? "ja" : "nein";

    private void ShowUpdateResult(UpdateCheckResult? result)
    {
        _availableUpdate = result?.Manifest;
        InstallButton.IsVisible = result?.IsUpdateAvailable == true;
        if (result is null)
        {
            UpdateStatus.Clear();
            return;
        }

        var checkedAt = UpdateService.LastCheck is { } at ? $" (geprüft {at:HH:mm})" : "";
        UpdateStatus.Show(result.Message + checkedAt, result.IsUpdateAvailable ? InfoSeverity.Warning : InfoSeverity.Success);
    }

    private void OnCheckUpdatesChanged(object? sender, EventArgs e)
    {
        AutoInstallToggle.IsEnabled = CheckUpdatesToggle.IsChecked;
        if (!CheckUpdatesToggle.IsChecked)
        {
            AutoInstallToggle.IsChecked = false;
        }
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        var settings = SettingsService.Get();
        settings.StartWithWindows = StartWithWindowsToggle.IsChecked;
        settings.StartInTray = StartInTrayToggle.IsChecked;
        settings.KeepRunningInTray = KeepRunningToggle.IsChecked;
        settings.NotifyOnErrors = NotifyToggle.IsChecked;
        settings.CheckForUpdates = CheckUpdatesToggle.IsChecked;
        settings.InstallUpdatesAutomatically = AutoInstallToggle.IsChecked;
        settings.SshExecutablePath = SshPathBox.Text ?? "";

        if (PageActions.Run(() => SettingsService.Save(settings, App.ExecutablePath), MessageBar, Form))
        {
            MessageBar.Show("Einstellungen gespeichert", InfoSeverity.Success);
        }
        else if (SshPathField.Error.Length > 0)
        {
            Tabs.SelectedIndex = AdvancedTab;
        }
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => PageWindow.CloseHostOf(this);

    private async void OnCheckClick(object? sender, RoutedEventArgs e)
    {
        CheckButton.IsEnabled = false;
        UpdateStatus.Show("Suche nach Updates …", InfoSeverity.Info);
        try
        {
            ShowUpdateResult(await UpdateCoordinator.CheckAsync());
        }
        catch (BusinessException exception)
        {
            UpdateStatus.Show(exception.Message, InfoSeverity.Error);
        }
        finally
        {
            CheckButton.IsEnabled = true;
        }
    }

    private async void OnInstallClick(object? sender, RoutedEventArgs e)
    {
        if (_availableUpdate is null)
        {
            return;
        }

        InstallButton.IsEnabled = CheckButton.IsEnabled = false;
        DownloadProgress.IsVisible = true;
        UpdateStatus.Show($"Lade Version {_availableUpdate.Version} …", InfoSeverity.Info);
        try
        {
            await UpdateCoordinator.InstallAsync(_availableUpdate, new Progress<double>(value => DownloadProgress.Value = value));
        }
        catch (Exception exception) when (exception is BusinessException or HttpRequestException or IOException)
        {
            UpdateStatus.Show($"Update fehlgeschlagen: {exception.Message}", InfoSeverity.Error);
            InstallButton.IsEnabled = CheckButton.IsEnabled = true;
            DownloadProgress.IsVisible = false;
        }
    }

    private void OnReleasePageClick(object? sender, RoutedEventArgs e) => Ui.OpenExternal(UpdateService.ReleasePageUrl(AppInfo.Channel));

    private async void OnBrowseSshClick(object? sender, RoutedEventArgs e)
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
            SshPathBox.Text = path;
        }
    }

    private void OnOpenDataFolderClick(object? sender, RoutedEventArgs e) => OpenFolder(AppPaths.DataDirectory);

    private void OnOpenLogFolderClick(object? sender, RoutedEventArgs e) => OpenFolder(AppPaths.LogDirectory);

    internal static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Ui.OpenExternal(path);
    }
}
