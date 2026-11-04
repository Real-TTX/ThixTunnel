using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using ThixTunnel.Controls;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Infrastructure;

namespace ThixTunnel.Setup;

/// <summary>
/// The setup assistant. The work (InstallService, business layer) runs on a background task and reports its
/// steps into the progress page; the window only steers. It cannot be closed halfway through.
/// </summary>
public partial class SetupWindow : Window
{
    private readonly bool _isUninstall;
    private readonly bool _isUpdate;
    private readonly ObservableCollection<string> _steps = [];
    private bool _isWorking;
    private bool _isFinished;
    private bool _succeeded;

    private SetupWindow(bool isUninstall)
    {
        InitializeComponent();
        NativeWindow.ApplyTheme(this);
        var workArea = SystemParameters.WorkArea;
        Width = Math.Min(Width, workArea.Width - 16);
        Height = Math.Min(Height, workArea.Height - 16);

        _isUninstall = isUninstall;
        _isUpdate = !isUninstall && InstallService.IsInstalled;
        StepList.ItemsSource = _steps;
        VersionText.Text = $"Version {(isUninstall ? InstallService.InstalledVersion ?? AppInfo.Version : AppInfo.Version)}";

        if (isUninstall)
        {
            ShowConfirmPage();
        }
        else
        {
            ShowWelcomePage();
        }
    }

    /// <summary>True when the program folder has to be removed after this process ended (uninstall from the program folder).</summary>
    public bool RemoveProgramFolderOnExit { get; private set; }

    /// <summary>Shows the assistant; returns when it was closed.</summary>
    public static SetupWindow Run(bool isUninstall)
    {
        var window = new SetupWindow(isUninstall);
        window.ShowDialog();
        return window;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Half an installation is worse than a slow one.
        e.Cancel = _isWorking;
        base.OnClosing(e);
    }

    private void ShowWelcomePage()
    {
        Title = _isUpdate ? "ThixTunnel aktualisieren" : $"ThixTunnel {AppInfo.Version} Setup";
        ModeText.Text = _isUpdate ? "Update" : "Setup";
        if (_isUpdate)
        {
            // An update keeps autostart and shortcuts as they are: no options to ask for.
            WelcomeTitle.Text = "Update";
            WelcomeText.Text = $"Installiert ist {InstallService.InstalledVersion ?? "eine ältere Version"}, dieses Setup bringt {AppInfo.Version}. " +
                               "Ein laufendes ThixTunnel wird beendet; aktive Tunnel werden danach wieder verbunden. Profile und Verbindungen bleiben erhalten.";
            OptionsTile.Visibility = Visibility.Collapsed;
            PrimaryLabel.Text = "Aktualisieren";
        }
        else
        {
            AutostartToggle.IsChecked = true;
            DesktopToggle.IsChecked = InstallService.HasDesktopShortcut;
            PrimaryLabel.Text = "Installieren";
        }

        PlaceInfo.Show($"Wird für dein Benutzerkonto installiert – ohne Administratorrechte – nach {InstallService.ProgramFolder}.", InfoSeverity.Info);
        if (!File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH", "ssh.exe")))
        {
            SshHint.Show("Der Windows-OpenSSH-Client (ssh.exe) wurde nicht gefunden. ThixTunnel braucht ihn für die Verbindungen: " +
                         "Einstellungen › System › Optionale Features › „OpenSSH-Client“.", InfoSeverity.Warning);
        }
    }

    private void ShowConfirmPage()
    {
        Title = "ThixTunnel deinstallieren";
        ModeText.Text = "Deinstallation";
        WelcomePage.Visibility = Visibility.Collapsed;
        ConfirmPage.Visibility = Visibility.Visible;
        PrimaryButton.SetResourceReference(StyleProperty, "DangerButton");
        PrimaryButton.IsDefault = false;
        PrimaryLabel.Icon = (string)FindResource("IconDelete");
        PrimaryLabel.Text = "Deinstallieren";
    }

    private async void OnPrimaryClick(object sender, RoutedEventArgs e)
    {
        if (_isFinished)
        {
            if (_succeeded && !_isUninstall && StartToggle.IsChecked)
            {
                InstallService.StartInstalled();
            }

            Close();
            return;
        }

        await RunAsync();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();

    private async Task RunAsync()
    {
        _isWorking = true;
        WelcomePage.Visibility = ConfirmPage.Visibility = Visibility.Collapsed;
        ProgressPage.Visibility = Visibility.Visible;
        ProgressTitle.Text = _isUninstall ? "Entferne …" : _isUpdate ? "Aktualisiere …" : "Installiere …";
        PrimaryButton.IsEnabled = CancelButton.IsEnabled = false;

        var source = App.ExecutablePath;
        var options = _isUpdate ? null : new InstallOptions(AutostartToggle.IsChecked, DesktopToggle.IsChecked);
        var deleteData = DeleteDataToggle.IsChecked;
        void Report(string step) => Dispatcher.BeginInvoke(() => _steps.Add(step));

        string? error = null;
        try
        {
            await Task.Run(() =>
            {
                if (_isUninstall)
                {
                    RemoveProgramFolderOnExit = InstallService.Uninstall(deleteData, Report);
                }
                else
                {
                    InstallService.Install(source, options, Report);
                }
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException
                                              or System.Runtime.InteropServices.COMException or System.Security.SecurityException)
        {
            Log.Error(_isUninstall ? "Uninstall failed" : "Setup failed", exception);
            error = exception.Message;
        }

        _isWorking = false;
        ShowFinishPage(error);
    }

    private void ShowFinishPage(string? error)
    {
        _isFinished = true;
        _succeeded = error is null;
        ProgressPage.Visibility = Visibility.Collapsed;
        FinishPage.Visibility = Visibility.Visible;
        CancelButton.Visibility = Visibility.Collapsed;
        PrimaryButton.IsEnabled = true;
        PrimaryButton.IsDefault = true;
        PrimaryButton.SetResourceReference(StyleProperty, "PrimaryButton");
        PrimaryLabel.Icon = (string)FindResource("IconCheck");
        PrimaryLabel.Text = "Fertig";
        StartTile.Visibility = _succeeded && !_isUninstall ? Visibility.Visible : Visibility.Collapsed;

        if (error is not null)
        {
            FinishTitle.Text = _isUninstall ? "Deinstallation fehlgeschlagen" : "Installation fehlgeschlagen";
            ResultBar.Show($"{error}\n\nDetails stehen im Protokoll ({AppPaths.LogDirectory}).", InfoSeverity.Error);
            return;
        }

        FinishTitle.Text = _isUninstall ? "ThixTunnel wurde entfernt" : "ThixTunnel ist bereit";
        ResultBar.Show(_isUninstall
            ? DeleteDataToggle.IsChecked ? "Programm und Daten wurden entfernt." : "Das Programm wurde entfernt. Deine Profile und Verbindungen bleiben erhalten."
            : $"ThixTunnel {AppInfo.Version} ist installiert. Du findest es im Startmenü und im Infobereich der Taskleiste.",
            InfoSeverity.Success);
    }
}
