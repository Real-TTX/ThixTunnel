using Avalonia.Controls;
using ThixTunnel.Controls;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Models;
using ThixTunnel.Dialogs;

namespace ThixTunnel.Pages;

/// <summary>
/// The SSH profiles (tab "SSH-Profile" of the settings): toolbar, list, and below it Neues SSH-Profil, Bearbeiten … (gap) …
/// Löschen. A profile is the sign-in at one server; connections use profiles as their hops.
/// </summary>
public sealed class ProfileListPage : UserControl, IRefreshable, ISearchable
{
    private static readonly ListOption[] Filters =
    [
        new(ProfileService.FilterAll, "Alle"),
        new(ProfileService.FilterAgent, "SSH-Agent"),
        new(ProfileService.FilterKeyFile, "Schlüsseldatei"),
        new(ProfileService.FilterPassword, "Passwort"),
        new(ProfileService.FilterUnused, "Unbenutzt"),
    ];

    private static readonly ListOption[] Sortings =
    [
        new(ProfileService.SortByName, "Name"),
        new(ProfileService.SortByHost, "Server"),
        new(ProfileService.SortByUsage, "Verwendung"),
    ];

    private readonly MainWindow _window;
    private readonly ListLayout _layout;
    private readonly Button _newButton;
    private readonly Button _editButton;
    private readonly Button _deleteButton;

    public ProfileListPage(MainWindow window)
    {
        _window = window;
        var toolbar = new ListToolbar("Profile durchsuchen", Filters, Sortings);
        var list = new EntityList("", openName: "bearbeiten");
        _layout = new ListLayout(null, "Ein SSH-Profil beschreibt die Anmeldung an einem Server: Adresse, Port, Benutzer und Schlüssel.", toolbar, list);
        _newButton = _layout.AddAction("plus", "Neues SSH-Profil", () => Open(null), "MatButton.Primary");
        _editButton = _layout.AddAction("edit", "Bearbeiten", () => Open(_layout.List.SelectedId));
        _deleteButton = _layout.AddAction("trash", "Löschen", Delete, "MatButton.Danger", separated: true);
        toolbar.Changed += (_, _) => Refresh();
        list.Opened += (_, id) => Open(id);
        list.SelectionChanged += (_, _) => RefreshActions();
        Content = _layout;
        Refresh();
    }

    public EntityList List => _layout.List;

    public InfoBar Message => _layout.Message;

    public void FocusSearch() => _layout.Toolbar.SearchBox.Focus();

    public static string AuthenticationLabel(SshAuthenticationMethod method) => method switch
    {
        SshAuthenticationMethod.KeyFile => "Schlüsseldatei",
        SshAuthenticationMethod.Password => "Passwort",
        _ => "SSH-Agent",
    };

    public void Refresh()
    {
        var usage = ProfileService.GetUsage();
        var total = ProfileService.List().Count;
        _layout.List.EmptyText = total == 0
            ? "Noch kein SSH-Profil. Mit „Neues SSH-Profil“ legst du den ersten Server an."
            : "Kein Profil passt zur Suche oder zum Filter.";
        _layout.List.Show(ProfileService.List(_layout.Toolbar.Query).Select(profile =>
        {
            var used = usage.GetValueOrDefault(profile.Id);
            var badges = new List<string> { AuthenticationLabel(profile.AuthenticationMethod) };
            if (used == 0)
            {
                badges.Add("unbenutzt");
            }

            return new EntityRow(profile.Id, profile.AuthenticationMethod == SshAuthenticationMethod.Password ? "lock" : "key", profile.Name, profile.DisplayAddress)
            {
                Badges = badges,
                Detail = used switch
                {
                    0 => profile.Description,
                    1 => "In 1 Verbindung" + (profile.Description.Length > 0 ? $" · {profile.Description}" : ""),
                    _ => $"In {used} Verbindungen" + (profile.Description.Length > 0 ? $" · {profile.Description}" : ""),
                },
            };
        }).ToList(), total);
        RefreshActions();
    }

    private void RefreshActions()
    {
        var hasSelection = _layout.List.SelectedId is not null;
        var editable = !AccessPolicy.IsConfigurationLocked;
        _newButton.IsEnabled = editable;
        _editButton.IsEnabled = hasSelection;
        _deleteButton.IsEnabled = hasSelection && editable;
    }

    private void Open(Guid? profileId) => _window.OpenPage(new ProfileEditPage(_window, profileId));

    private async void Delete()
    {
        if (_layout.List.SelectedId is not { } id || ProfileService.Get(id) is not { } profile)
        {
            return;
        }

        if (!await ConfirmDialog.AskAsync(Ui.WindowOf(this), $"„{profile.Name}“ löschen?", "Das SSH-Profil wird gelöscht. Ein Profil, das eine Verbindung noch verwendet, bleibt erhalten.", "Löschen", isDestructive: true))
        {
            return;
        }

        if (_layout.Run(() => ProfileService.Delete(id)))
        {
            AppEvents.NotifyDataChanged();
            Refresh();
            _layout.Message.Show($"„{profile.Name}“ wurde gelöscht.", InfoSeverity.Success);
        }
    }
}
