using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ThixTunnel.Controls;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Models;
using ThixTunnel.Dialogs;

namespace ThixTunnel.Pages;

/// <summary>Create or change an SSH profile: tabs "Allgemein" and "Authentifizierung".</summary>
public partial class ProfileEditPage : UserControl
{
    private readonly SshProfile _profile;
    private readonly bool _isNew;

    /// <summary>For the XAML loader and previewer.</summary>
    public ProfileEditPage()
        : this(null)
    {
    }

    public ProfileEditPage(Guid? profileId)
    {
        InitializeComponent();

        var existing = profileId is null ? null : ProfileService.Get(profileId.Value);
        _isNew = existing is null;
        _profile = existing ?? new SshProfile { UserName = Environment.UserName.ToLowerInvariant() };

        Header.Title = _isNew ? "Neues SSH-Profil" : "SSH-Profil bearbeiten";
        AgentDescription.Text = OperatingSystem.IsWindows()
            ? @"Schlüssel aus dem Windows-OpenSSH-Agenten oder aus %USERPROFILE%\.ssh. Empfohlen – auch für automatische Verbindungen."
            : "Schlüssel aus dem ssh-agent (macOS: auch aus dem Schlüsselbund) oder aus ~/.ssh. Empfohlen – auch für automatische Verbindungen.";
        NameBox.Text = _profile.Name;
        HostBox.Text = _profile.HostName;
        PortBox.Text = _profile.Port.ToString();
        UserBox.Text = _profile.UserName;
        DescriptionBox.Text = _profile.Description;
        KeyFileBox.Text = _profile.KeyFilePath;
        KeySetupToggle.IsChecked = _profile.SetUpKeyOnFirstLogin;
        AgentOption.IsChecked = _profile.AuthenticationMethod == SshAuthenticationMethod.Agent;
        KeyFileOption.IsChecked = _profile.AuthenticationMethod == SshAuthenticationMethod.KeyFile;
        PasswordOption.IsChecked = _profile.AuthenticationMethod == SshAuthenticationMethod.Password;
        DeleteButton.IsVisible = !_isNew;

        var routes = _isNew ? [] : ProfileService.GetRouteNamesUsing(_profile.Id);
        UsageText.Text = routes.Count == 0 ? "" : $"Verwendet in: {string.Join(", ", routes)}";

        if (AccessPolicy.IsConfigurationLocked)
        {
            MessageBar.Show("Die Konfiguration ist durch eine Richtlinie gesperrt und kann nicht geändert werden.", InfoSeverity.Warning);
            SaveButton.IsEnabled = DeleteButton.IsEnabled = false;
        }

        foreach (var option in new[] { AgentOption, KeyFileOption, PasswordOption })
        {
            option.IsCheckedChanged += (_, _) => RefreshAuthentication();
        }

        RefreshAuthentication();
        AttachedToVisualTree += (_, _) => NameBox.Focus();
    }

    private SshAuthenticationMethod SelectedMethod =>
        KeyFileOption.IsChecked == true ? SshAuthenticationMethod.KeyFile
        : PasswordOption.IsChecked == true ? SshAuthenticationMethod.Password
        : SshAuthenticationMethod.Agent;

    /// <summary>The key file input only makes sense for the key file method, the key setup only for passwords.</summary>
    private void RefreshAuthentication()
    {
        KeyFileTile.IsVisible = SelectedMethod == SshAuthenticationMethod.KeyFile;
        KeySetupTile.IsVisible = SelectedMethod == SshAuthenticationMethod.Password;
    }

    private async void OnBrowseKeyClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            return;
        }

        var sshDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Private Schlüsseldatei wählen",
            SuggestedStartLocation = Directory.Exists(sshDirectory) ? await storage.TryGetFolderFromPathAsync(sshDirectory) : null,
            FileTypeFilter = [FilePickerFileTypes.All],
        });
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            KeyFileBox.Text = path;
        }
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        var profile = _profile.Clone();
        profile.Name = NameBox.Text ?? "";
        profile.HostName = HostBox.Text ?? "";
        profile.Port = PageActions.ParsePort(PortBox.Text);
        profile.UserName = UserBox.Text ?? "";
        profile.Description = DescriptionBox.Text ?? "";
        profile.AuthenticationMethod = SelectedMethod;
        profile.KeyFilePath = KeyFileBox.Text ?? "";
        profile.SetUpKeyOnFirstLogin = KeySetupToggle.IsChecked;

        if (PageActions.Run(() => ProfileService.Save(profile), MessageBar, Form))
        {
            AppWindows.NotifyDataChanged();
            GoBack();
            return;
        }

        // Show the tab that holds the error.
        Tabs.SelectedIndex = KeyFileTile.IsVisible && KeyFileField.Error.Length > 0 ? 1 : 0;
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => GoBack();

    private void GoBack() => PageWindow.CloseHostOf(this);

    private async void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (await ConfirmDialog.AskAsync(Ui.WindowOf(this), "SSH-Profil löschen", $"„{_profile.Name}“ wird gelöscht.", "Löschen", isDestructive: true) &&
            PageActions.Run(() => ProfileService.Delete(_profile.Id), MessageBar))
        {
            AppWindows.NotifyDataChanged();
            GoBack();
        }
    }
}
