using System.Windows;
using System.Windows.Controls;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Infrastructure;

namespace ThixTunnel.Pages;

/// <summary>About ThixTunnel; opened through the version number at the bottom left of the side bar.</summary>
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
        var sshPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH", "ssh.exe");
        var configured = SettingsService.Get().SshExecutablePath;
        SshText.Text = configured.Length > 0 ? configured : File.Exists(sshPath) ? sshPath : "Windows OpenSSH-Client nicht gefunden";
        DataText.Text = AppPaths.DataFile;
    }

    private void OnProjectClick(object sender, RoutedEventArgs e) => SettingsPage.OpenUrl($"https://github.com/{AppInfo.Repository}");

    private void OnUpdatesClick(object sender, RoutedEventArgs e) => AppWindows.ShowSettings(SettingsPage.UpdatesTab);

    private void OnBackClick(object sender, RoutedEventArgs e) => PageWindow.CloseHostOf(this);
}
