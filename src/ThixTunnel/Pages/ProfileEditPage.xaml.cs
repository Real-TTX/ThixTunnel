using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ThixTunnel.Controls;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Models;
using ThixTunnel.Dialogs;

namespace ThixTunnel.Pages;

/// <summary>Create or change an SSH profile: tabs "Allgemein" and "Authentifizierung".</summary>
public partial class ProfileEditPage : UserControl
{
    private readonly MainWindow _window;
    private readonly SshProfile _profile;
    private readonly bool _isNew;

    public ProfileEditPage(MainWindow window, Guid? profileId)
    {
        _window = window;
        InitializeComponent();
        Header.ParentClicked += (_, _) => GoBack();

        var existing = profileId is null ? null : ProfileService.Get(profileId.Value);
        _isNew = existing is null;
        _profile = existing ?? new SshProfile { UserName = Environment.UserName.ToLowerInvariant() };

        Header.Title = _isNew ? "Neues SSH-Profil" : "SSH-Profil bearbeiten";
        Header.CurrentCrumb = _isNew ? "Neu" : _profile.Name;
        NameBox.Text = _profile.Name;
        HostBox.Text = _profile.HostName;
        PortBox.Text = _profile.Port.ToString();
        UserBox.Text = _profile.UserName;
        DescriptionBox.Text = _profile.Description;
        KeyFileBox.Text = _profile.KeyFilePath;
        AgentOption.IsChecked = _profile.AuthenticationMethod == SshAuthenticationMethod.Agent;
        KeyFileOption.IsChecked = _profile.AuthenticationMethod == SshAuthenticationMethod.KeyFile;
        PasswordOption.IsChecked = _profile.AuthenticationMethod == SshAuthenticationMethod.Password;
        DeleteButton.Visibility = _isNew ? Visibility.Collapsed : Visibility.Visible;

        var routes = _isNew ? [] : ProfileService.GetRouteNamesUsing(_profile.Id);
        UsageText.Text = routes.Count == 0 ? "" : $"Verwendet in: {string.Join(", ", routes)}";

        if (AccessPolicy.IsConfigurationLocked)
        {
            MessageBar.Show("Die Konfiguration ist durch eine Richtlinie gesperrt und kann nicht geändert werden.", InfoSeverity.Warning);
            SaveButton.IsEnabled = DeleteButton.IsEnabled = false;
        }

        RefreshAuthentication();
        Loaded += (_, _) => NameBox.Focus();
    }

    private SshAuthenticationMethod SelectedMethod =>
        KeyFileOption.IsChecked == true ? SshAuthenticationMethod.KeyFile
        : PasswordOption.IsChecked == true ? SshAuthenticationMethod.Password
        : SshAuthenticationMethod.Agent;

    /// <summary>The key file input only makes sense for the key file method.</summary>
    private void RefreshAuthentication()
    {
        if (KeyFileTile is not null)
        {
            KeyFileTile.Visibility = SelectedMethod == SshAuthenticationMethod.KeyFile ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void OnAuthenticationChanged(object sender, RoutedEventArgs e) => RefreshAuthentication();

    private void OnBrowseKeyClick(object sender, RoutedEventArgs e)
    {
        var sshDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
        var dialog = new OpenFileDialog
        {
            Title = "Private Schlüsseldatei wählen",
            InitialDirectory = Directory.Exists(sshDirectory) ? sshDirectory : "",
            Filter = "Alle Dateien (*.*)|*.*",
        };
        if (dialog.ShowDialog(_window) == true)
        {
            KeyFileBox.Text = dialog.FileName;
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var profile = _profile.Clone();
        profile.Name = NameBox.Text;
        profile.HostName = HostBox.Text;
        profile.Port = PageActions.ParsePort(PortBox.Text);
        profile.UserName = UserBox.Text;
        profile.Description = DescriptionBox.Text;
        profile.AuthenticationMethod = SelectedMethod;
        profile.KeyFilePath = KeyFileBox.Text;

        SshProfile? saved = null;
        if (PageActions.Run(() => saved = ProfileService.Save(profile), MessageBar, Form))
        {
            _window.ShowProfiles(saved!.Id);
            return;
        }

        // Show the tab that holds the error.
        Tabs.SelectedIndex = KeyFileTile.Visibility == Visibility.Visible && HasKeyFileError() ? 1 : 0;
    }

    private bool HasKeyFileError() =>
        LogicalTreeHelper.GetChildren(KeyFileTile).OfType<FormField>().Any(field => field.Error.Length > 0);

    private void OnBackClick(object sender, RoutedEventArgs e) => GoBack();

    private void GoBack() => _window.ShowProfiles(_isNew ? null : _profile.Id);

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (ConfirmDialog.Ask(_window, "SSH-Profil löschen", $"„{_profile.Name}“ wird gelöscht.", "Löschen", isDestructive: true) &&
            PageActions.Run(() => ProfileService.Delete(_profile.Id), MessageBar))
        {
            _window.ShowProfiles();
        }
    }
}
