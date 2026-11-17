using Avalonia.Controls;
using Avalonia.Interactivity;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Infrastructure;

namespace ThixTunnel.Pages;

/// <summary>About ThixTunnel; opened through the version number at the bottom left of the main window.</summary>
public partial class AboutPage : UserControl
{
    public AboutPage()
    {
        InitializeComponent();
        VersionText.Text = AppInfo.Version;
        ChannelText.Text = AppInfo.Channel switch
        {
            "release" => $"Release · Build {AppInfo.BuildNumber}",
            "nightly" => $"Nightly · Build {AppInfo.BuildNumber}",
            _ => "Lokaler Build",
        };
        var configured = SettingsService.Get().SshExecutablePath;
        SshText.Text = Breakable(configured.Length > 0 ? configured : Ui.SystemSshPath ?? "OpenSSH-Client nicht gefunden");
        DataText.Text = Breakable(AppPaths.DataFile);
    }

    private void OnProjectClick(object? sender, RoutedEventArgs e) => Ui.OpenExternal($"https://github.com/{AppInfo.Repository}");

    private void OnUpdatesClick(object? sender, RoutedEventArgs e) => AppWindows.ShowSettings(SettingsPage.UpdatesTab);

    private void OnBackClick(object? sender, RoutedEventArgs e) => PageWindow.CloseHostOf(this);

    /// <summary>Long paths wrap at their separators, not in the middle of a name.</summary>
    private static string Breakable(string path) => path.Replace("\\", "\\​").Replace("/", "/​");
}
