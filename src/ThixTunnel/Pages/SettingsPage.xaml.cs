using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ThixTunnel.Controls;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Infrastructure;
using ThixTunnel.Core.Models;

namespace ThixTunnel.Pages;

/// <summary>Global settings: start and behaviour, updates, advanced.</summary>
public partial class SettingsPage : UserControl
{
    private readonly MainWindow _window;
    private UpdateManifest? _availableUpdate;

    public SettingsPage(MainWindow window, int tabIndex)
    {
        _window = window;
        InitializeComponent();
        Tabs.SelectedIndex = tabIndex;

        var settings = SettingsService.Get();
        StartWithWindowsToggle.IsChecked = settings.StartWithWindows;
        StartInTrayToggle.IsChecked = settings.StartInTray;
        KeepRunningToggle.IsChecked = settings.KeepRunningInTray;
        NotifyToggle.IsChecked = settings.NotifyOnErrors;
        CheckUpdatesToggle.IsChecked = settings.CheckForUpdates;
        AutoInstallToggle.IsChecked = settings.InstallUpdatesAutomatically;
        AutoInstallToggle.IsEnabled = settings.CheckForUpdates;
        SshPathBox.Text = settings.SshExecutablePath;
        SshPathField.Hint = "Leer = Windows OpenSSH (" + (SshExecutableHint() ?? "nicht gefunden – optionales Windows-Feature „OpenSSH-Client“ installieren") + ")";
        AutoConnectInfo.Show("Automatisches Verbinden wird je Tunnelroute festgelegt (Tunnel › Bearbeiten › Automatisierung).", InfoSeverity.Info);

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
        ReleasePageButton.Visibility = AppInfo.IsLocalBuild ? Visibility.Collapsed : Visibility.Visible;

        PolicyText.Text = string.Join(Environment.NewLine,
            $"Konfiguration gesperrt: {YesNo(AccessPolicy.IsConfigurationLocked)}",
            $"Nur Loopback-Adressen: {YesNo(AccessPolicy.IsNetworkBindingDenied)}",
            $"Updates deaktiviert: {YesNo(AccessPolicy.AreUpdatesDisabled)}",
            "",
            @"Administratoren setzen diese Werte als DWORD = 1 unter HKLM\SOFTWARE\Policies\ThixTunnel (LockConfiguration, DenyNetworkBinding, DisableUpdates).");

        if (AccessPolicy.IsConfigurationLocked)
        {
            MessageBar.Show("Die Einstellungen sind durch eine Richtlinie gesperrt.", InfoSeverity.Warning);
            SaveButton.IsEnabled = false;
        }
    }

    private static string YesNo(bool value) => value ? "ja" : "nein";

    private static string? SshExecutableHint()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH", "ssh.exe");
        return File.Exists(path) ? path : null;
    }

    private void ShowUpdateResult(UpdateCheckResult? result)
    {
        _availableUpdate = result?.Manifest;
        InstallButton.Visibility = result?.IsUpdateAvailable == true ? Visibility.Visible : Visibility.Collapsed;
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

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var settings = SettingsService.Get();
        settings.StartWithWindows = StartWithWindowsToggle.IsChecked;
        settings.StartInTray = StartInTrayToggle.IsChecked;
        settings.KeepRunningInTray = KeepRunningToggle.IsChecked;
        settings.NotifyOnErrors = NotifyToggle.IsChecked;
        settings.CheckForUpdates = CheckUpdatesToggle.IsChecked;
        settings.InstallUpdatesAutomatically = AutoInstallToggle.IsChecked;
        settings.SshExecutablePath = SshPathBox.Text;

        if (PageActions.Run(() => SettingsService.Save(settings, App.ExecutablePath), MessageBar, Form))
        {
            MessageBar.Show("Einstellungen gespeichert", InfoSeverity.Success);
        }
        else if (SshPathField.Error.Length > 0)
        {
            Tabs.SelectedIndex = 2;
        }
    }

    private void OnBackClick(object sender, RoutedEventArgs e) => _window.ShowTunnels();

    private async void OnCheckClick(object sender, RoutedEventArgs e)
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

    private async void OnInstallClick(object sender, RoutedEventArgs e)
    {
        if (_availableUpdate is null)
        {
            return;
        }

        InstallButton.IsEnabled = CheckButton.IsEnabled = false;
        DownloadProgress.Visibility = Visibility.Visible;
        UpdateStatus.Show($"Lade Version {_availableUpdate.Version} …", InfoSeverity.Info);
        try
        {
            await UpdateCoordinator.InstallAsync(_availableUpdate, new Progress<double>(value => DownloadProgress.Value = value));
        }
        catch (Exception exception) when (exception is BusinessException or HttpRequestException or IOException)
        {
            UpdateStatus.Show($"Update fehlgeschlagen: {exception.Message}", InfoSeverity.Error);
            InstallButton.IsEnabled = CheckButton.IsEnabled = true;
            DownloadProgress.Visibility = Visibility.Collapsed;
        }
    }

    private void OnReleasePageClick(object sender, RoutedEventArgs e) => OpenUrl(UpdateService.ReleasePageUrl(AppInfo.Channel));

    private void OnBrowseSshClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "ssh.exe wählen", Filter = "ssh.exe|ssh.exe|Programme (*.exe)|*.exe" };
        if (dialog.ShowDialog(_window) == true)
        {
            SshPathBox.Text = dialog.FileName;
        }
    }

    private void OnOpenDataFolderClick(object sender, RoutedEventArgs e) => OpenFolder(AppPaths.DataDirectory);

    private void OnOpenLogFolderClick(object sender, RoutedEventArgs e) => OpenFolder(AppPaths.LogDirectory);

    internal static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = false });
    }

    internal static void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}
