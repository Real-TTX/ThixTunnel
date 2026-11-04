using System.Windows;
using System.Windows.Controls;
using ThixTunnel.Controls;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Models;
using ThixTunnel.Dialogs;

namespace ThixTunnel.Pages;

/// <summary>List of the SSH profiles.</summary>
public partial class ProfileListPage : UserControl
{
    private static readonly ListOption[] Filters =
    [
        new(ProfileService.FilterAll, "Alle"),
        new(ProfileService.FilterAgent, "SSH-Agent"),
        new(ProfileService.FilterKeyFile, "Schlüsseldatei"),
        new(ProfileService.FilterPassword, "Passwort"),
        new(ProfileService.FilterUnused, "Unbenutzt"),
    ];

    private static readonly ListOption[] SortFields =
    [
        new(ProfileService.SortByName, "Name"),
        new(ProfileService.SortByHost, "Server"),
        new(ProfileService.SortByUsage, "Verwendung"),
    ];

    private readonly MainWindow _window;

    public ProfileListPage(MainWindow window, Guid? selectProfileId)
    {
        _window = window;
        InitializeComponent();
        Toolbar.Configure(Filters, SortFields);
        Toolbar.QueryChanged += (_, _) => Reload((ProfileList.SelectedItem as ProfileRow)?.Id);
        Reload(selectProfileId);
    }

    public static string AuthenticationLabel(SshAuthenticationMethod method) => method switch
    {
        SshAuthenticationMethod.KeyFile => "Schlüsseldatei",
        SshAuthenticationMethod.Password => "Passwort",
        _ => "SSH-Agent",
    };

    private void Reload(Guid? selectProfileId)
    {
        var usage = ProfileService.GetUsage();
        var rows = ProfileService.List(Toolbar.Query).Select(profile => new ProfileRow(profile, usage.GetValueOrDefault(profile.Id))).ToList();
        ProfileList.ItemsSource = rows;
        ProfileList.SelectedItem = rows.FirstOrDefault(row => row.Id == selectProfileId) ?? rows.FirstOrDefault();
        EmptyText.Text = ProfileService.List().Count == 0
            ? "Noch kein SSH-Profil. Ein Profil beschreibt die Anmeldung an einem Server (Adresse, Port, Benutzer, Schlüssel)."
            : "Kein Profil passt zur Suche oder zum Filter.";
        EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RefreshActions();
    }

    private void RefreshActions()
    {
        var selected = ProfileList.SelectedItem is ProfileRow;
        EditButton.IsEnabled = selected;
        DeleteButton.IsEnabled = selected && !AccessPolicy.IsConfigurationLocked;
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshActions();

    private void OnRowDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => OnEditClick(sender, e);

    private void OnNewClick(object sender, RoutedEventArgs e) => _window.EditProfile(null);

    private void OnEditClick(object sender, RoutedEventArgs e)
    {
        if (ProfileList.SelectedItem is ProfileRow row)
        {
            _window.EditProfile(row.Id);
        }
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (ProfileList.SelectedItem is not ProfileRow row ||
            !ConfirmDialog.Ask(_window, "SSH-Profil löschen", $"„{row.Name}“ wird gelöscht.", "Löschen", isDestructive: true))
        {
            return;
        }

        if (PageActions.Run(() => ProfileService.Delete(row.Id), MessageBar))
        {
            Reload(null);
            MessageBar.Show($"„{row.Name}“ wurde gelöscht.", InfoSeverity.Success);
        }
    }

    public sealed class ProfileRow(SshProfile profile, int usage)
    {
        public Guid Id { get; } = profile.Id;

        public string Name { get; } = profile.Name;

        /// <summary>Screen readers and UI automation read the row by its name.</summary>
        public override string ToString() => Name;

        public string Description { get; } = profile.Description;

        public string Server { get; } = profile.Port == 22 ? profile.HostName : $"{profile.HostName}:{profile.Port}";

        public string UserName { get; } = profile.UserName;

        public string Authentication { get; } = AuthenticationLabel(profile.AuthenticationMethod);

        public string AuthenticationIcon { get; } = (string)Application.Current.FindResource(
            profile.AuthenticationMethod == SshAuthenticationMethod.Password ? "IconLock" : "IconKey");

        public string Usage { get; } = usage switch
        {
            0 => "–",
            1 => "1 Route",
            _ => $"{usage} Routen",
        };
    }
}
