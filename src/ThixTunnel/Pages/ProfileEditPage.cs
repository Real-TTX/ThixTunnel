using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using MatDesktop;
using ThixTunnel.Controls;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Models;
using ThixTunnel.Dialogs;

namespace ThixTunnel.Pages;

/// <summary>
/// Create or change an SSH profile, tabs "Allgemein" and "Anmeldung". The key file only shows for the key file method, the
/// key setup only for passwords (owner's rule: hide what does not apply). Speichern, Zurück … (gap) … Löschen.
/// </summary>
public sealed class ProfileEditPage : UserControl
{
    private readonly MainWindow _window;
    private readonly SshProfile _profile;
    private readonly bool _isNew;
    private readonly EditLayout _layout;
    private readonly TextBox _name = new();
    private readonly TextBox _host = new();
    private readonly TextBox _port = new() { MaxWidth = 140, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBox _user = new() { MaxWidth = 260, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBox _description = new();
    private readonly TextBox _keyFile = new();
    private readonly ListBox _methods;
    private readonly Control _keyFileField;
    private readonly Control _keySetupRow;
    private readonly ToggleSwitch _keySetup;

    public ProfileEditPage(MainWindow window, Guid? profileId)
    {
        _window = window;
        var existing = profileId is null ? null : ProfileService.Get(profileId.Value);
        _isNew = existing is null;
        _profile = existing ?? new SshProfile { UserName = Environment.UserName.ToLowerInvariant() };
        _layout = new EditLayout(_isNew ? "Neues SSH-Profil" : _profile.Name);

        // ---- General ----
        _name.Text = _profile.Name;
        _host.Text = _profile.HostName;
        _port.Text = _profile.Port.ToString();
        _user.Text = _profile.UserName;
        _description.Text = _profile.Description;
        var general = new StackPanel { MaxWidth = 680, HorizontalAlignment = HorizontalAlignment.Left };
        general.Children.Add(Ui.Field("Name", _name, nameof(SshProfile.Name), "So erscheint das Profil in den Verbindungen, z. B. „Gateway“ oder „Datenbankserver“."));
        var server = new Grid { ColumnDefinitions = new ColumnDefinitions("*,16,160") };
        var portField = Ui.Field("SSH-Port", _port, nameof(SshProfile.Port));
        Grid.SetColumn(portField, 2);
        server.Children.Add(Ui.Field("Server (Hostname oder IP-Adresse)", _host, nameof(SshProfile.HostName)));
        server.Children.Add(portField);
        general.Children.Add(server);
        general.Children.Add(Ui.Field("Benutzer", _user, nameof(SshProfile.UserName)));
        general.Children.Add(Ui.Field("Beschreibung (optional)", _description, nameof(SshProfile.Description)));
        var routes = _isNew ? [] : ProfileService.GetRouteNamesUsing(_profile.Id);
        if (routes.Count > 0)
        {
            general.Children.Add(Ui.Text($"Verwendet in: {string.Join(", ", routes)}", "MatText.Muted"));
        }

        _layout.AddTab("Allgemein", general);

        // ---- Sign-in: the method as choices (a tint with the accent line when chosen, COLOR-08) ----
        var agentText = OperatingSystem.IsWindows()
            ? @"Schlüssel aus dem Windows-OpenSSH-Agenten oder aus %USERPROFILE%\.ssh. Empfohlen – auch für automatische Verbindungen."
            : "Schlüssel aus dem ssh-agent (macOS: auch aus dem Schlüsselbund) oder aus ~/.ssh. Empfohlen – auch für automatische Verbindungen.";
        _methods = new ListBox { SelectionMode = SelectionMode.Single | SelectionMode.AlwaysSelected }.Themed("MatList");
        _methods.Use(ItemsControl.ItemContainerThemeProperty, "MatChoiceRow");
        _methods.ItemsSource = new[]
        {
            Choice(SshAuthenticationMethod.Agent, "key", "SSH-Agent / Standardschlüssel", agentText),
            Choice(SshAuthenticationMethod.KeyFile, "file", "Bestimmte Schlüsseldatei", "Nur dieser private Schlüssel wird verwendet. Eine Passphrase wird beim manuellen Verbinden abgefragt."),
            Choice(SshAuthenticationMethod.Password, "lock", "Passwort", "Wird bei jedem manuellen Verbinden abgefragt und nie gespeichert. Für automatische Verbindungen: unten einen Schlüssel anlegen lassen."),
        };
        Avalonia.Automation.AutomationProperties.SetName(_methods, "Anmeldeverfahren");
        _methods.SelectedIndex = _profile.AuthenticationMethod switch
        {
            SshAuthenticationMethod.KeyFile => 1,
            SshAuthenticationMethod.Password => 2,
            _ => 0,
        };

        _keyFile.Text = _profile.KeyFilePath;
        var browse = Ui.Button("folder-open", "Durchsuchen …", click: BrowseKeyFile);
        browse.Margin = new Thickness(8, 0, 0, 0);
        var keyFileInput = new DockPanel();
        DockPanel.SetDock(browse, Dock.Right);
        keyFileInput.Children.Add(browse);
        keyFileInput.Children.Add(_keyFile);
        Avalonia.Automation.AutomationProperties.SetName(_keyFile, "Private Schlüsseldatei");
        _keyFileField = Ui.Field("Private Schlüsseldatei", keyFileInput, nameof(SshProfile.KeyFilePath));
        _keyFileField.Margin = new Thickness(0, 16, 0, 0);

        _keySetup = Ui.Switch("Beim ersten Anmelden einen Schlüssel anlegen und hinterlegen", _profile.SetUpKeyOnFirstLogin);
        _keySetupRow = Ui.Row("Beim ersten Anmelden einen Schlüssel anlegen und hinterlegen",
            "Beim nächsten manuellen Verbinden fragt ThixTunnel einmal nach dem Passwort, erzeugt einen eigenen Schlüssel und trägt ihn auf dem Server in ~/.ssh/authorized_keys ein. Danach verbindet das Profil ohne Passwort – auch automatisch. Nur für Linux-/Unix-Server.",
            _keySetup, last: true);
        _keySetupRow.Margin = new Thickness(0, 12, 0, 0);

        var signIn = new StackPanel { MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Left };
        signIn.Children.Add(Ui.Text("Anmeldeverfahren", "MatText.SectionTitle"));
        signIn.Children.Add(_methods);
        signIn.Children.Add(_keyFileField);
        signIn.Children.Add(_keySetupRow);
        _layout.AddTab("Anmeldung", signIn);
        _methods.SelectionChanged += (_, _) => RefreshMethod();
        RefreshMethod();

        // ---- Buttons ----
        var save = _layout.AddAction("save", "Speichern", Save, "MatButton.Primary");
        save.IsDefault = true;
        _layout.AddAction("back", "Zurück", _window.GoBack);
        Button? delete = null;
        if (!_isNew)
        {
            delete = _layout.AddAction("trash", "Löschen", Delete, "MatButton.Danger", separated: true);
        }

        if (AccessPolicy.IsConfigurationLocked)
        {
            _layout.Message.Show("Von deiner Organisation festgelegt: die Konfiguration ist gesperrt und kann nicht geändert werden.", InfoSeverity.Warning);
            save.IsEnabled = false;
            if (delete is not null)
            {
                delete.IsEnabled = false;
            }
        }

        Content = Ui.Page(_layout, 860);
        AttachedToVisualTree += (_, _) => _name.Focus();
    }

    public EditLayout Layout => _layout;

    public TextBox NameBox => _name;

    public TextBox HostBox => _host;

    public ListBox Methods => _methods;

    /// <summary>The key file field is there only for the key file method (tests).</summary>
    public bool IsKeyFileShown => _keyFileField.IsVisible;

    public bool IsKeySetupShown => _keySetupRow.IsVisible;

    private SshAuthenticationMethod SelectedMethod => (_methods.SelectedItem as Control)?.Tag as SshAuthenticationMethod? ?? SshAuthenticationMethod.Agent;

    private static Control Choice(SshAuthenticationMethod method, string icon, string title, string text)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Tag = method };
        var glyph = new MatIcon { Icon = icon, Size = 18, Margin = new Thickness(2, 2, 14, 0), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top }.Use(MatIcon.ForegroundProperty, "Mat.AccentText");
        var texts = new StackPanel { Spacing = 2 };
        texts.Children.Add(new TextBlock { Text = title, FontWeight = Avalonia.Media.FontWeight.SemiBold });
        texts.Children.Add(Ui.Text(text, "MatText.Muted"));
        Grid.SetColumn(texts, 1);
        grid.Children.Add(glyph);
        grid.Children.Add(texts);
        Avalonia.Automation.AutomationProperties.SetName(grid, title);
        return grid;
    }

    private void RefreshMethod()
    {
        _keyFileField.IsVisible = SelectedMethod == SshAuthenticationMethod.KeyFile;
        _keySetupRow.IsVisible = SelectedMethod == SshAuthenticationMethod.Password;
    }

    private async void BrowseKeyFile()
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
            _keyFile.Text = path;
        }
    }

    private void Save()
    {
        var profile = _profile.Clone();
        profile.Name = _name.Text ?? "";
        profile.HostName = _host.Text ?? "";
        profile.Port = int.TryParse((_port.Text ?? "").Trim(), out var port) ? port : 0;
        profile.UserName = _user.Text ?? "";
        profile.Description = _description.Text ?? "";
        profile.AuthenticationMethod = SelectedMethod;
        profile.KeyFilePath = _keyFile.Text ?? "";
        profile.SetUpKeyOnFirstLogin = _keySetup.IsChecked == true;

        if (_layout.Run(() => ProfileService.Save(profile)))
        {
            AppEvents.NotifyDataChanged();
            _window.GoBack();
        }
    }

    private async void Delete()
    {
        if (!await ConfirmDialog.AskAsync(Ui.WindowOf(this), $"„{_profile.Name}“ löschen?", "Das SSH-Profil wird gelöscht.", "Löschen", isDestructive: true))
        {
            return;
        }

        if (_layout.Run(() => ProfileService.Delete(_profile.Id)))
        {
            AppEvents.NotifyDataChanged();
            _window.GoBack();
        }
    }
}
