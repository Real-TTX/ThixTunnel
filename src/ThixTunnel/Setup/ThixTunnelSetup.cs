using System.Runtime.Versioning;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using MatDesktop;
using ThixTunnel.Controls;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Infrastructure;

namespace ThixTunnel.Setup;

/// <summary>
/// The Windows setup of ThixTunnel in MatDesktop's setup window (SETUP-01, SETUP-02): one window for install, update and
/// uninstall, 600 × 560, the footer with "Version x · channel" (SETUP-07). Install: Welcome (per user, no administrator
/// rights, where to: SETUP-03) → Options → Progress → Finish. Update: Welcome with the installed and the new version →
/// Progress → Finish. Uninstall: what goes and "Auch meine Profile und Verbindungen löschen" (off) → Progress → Finish
/// (SETUP-04). The work (InstallService, business layer) runs on a background task and reports its steps; the window only
/// steers and cannot be closed halfway through.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class ThixTunnelSetup
{
    /// <summary>Shows the assistant; <paramref name="finished"/> gets true when the program folder has to be removed after this process ended.</summary>
    public static void Run(bool isUninstall, Action<bool> finished)
    {
        var removeProgramFolder = false;
        var isUpdate = !isUninstall && InstallService.IsInstalled;
        var source = App.ExecutablePath;
        MatSetupWindow? window = null;
        window = Create(isUninstall, isUpdate, InstallService.InstalledVersion, InstallService.ProgramFolder, Ui.SystemSshPath is not null, async run =>
        {
            void Report(string step) => run.Progress.Report(new MatSetupReport(step, MatSetupStepState.Done));
            try
            {
                if (isUninstall)
                {
                    removeProgramFolder = await Task.Run(() => InstallService.Uninstall(run.DeleteData, Report));
                    window!.Steps[^1].Text = UninstalledText(run.DeleteData);
                }
                else
                {
                    var options = isUpdate
                        ? null
                        : new InstallOptions(run.Options.GetValueOrDefault("autostart"), run.Options.GetValueOrDefault("shortcut"), run.Options.GetValueOrDefault("updates"));
                    await Task.Run(() => InstallService.Install(source, options, Report));
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException
                                                  or System.Runtime.InteropServices.COMException or System.Security.SecurityException)
            {
                Log.Error(isUninstall ? "Uninstall failed" : "Setup failed", exception);
                throw new InvalidOperationException($"{exception.Message}\n\nDetails stehen im Protokoll ({AppPaths.LogDirectory}).", exception);
            }
        });

        window.Finished += (_, _) =>
        {
            if (window.StartNow)
            {
                InstallService.StartInstalled();
            }

            finished(removeProgramFolder);
        };
        window.Show();
        window.Activate();
    }

    /// <summary>
    /// The window for install (or update) or uninstall, with what it shows decided here once: the mode, the installed
    /// version, the program folder and whether the system has ssh. <paramref name="work"/> is the work of the Install, Update
    /// or Uninstall button; the snapshots pass work that touches nothing.
    /// </summary>
    public static MatSetupWindow Create(bool isUninstall, bool isUpdate, string? installedVersion, string programFolder, bool hasSsh, Func<MatSetupRun, Task> work)
    {
        var mode = isUninstall ? MatSetupMode.Uninstall : isUpdate ? MatSetupMode.Update : MatSetupMode.Setup;
        IReadOnlyList<MatSetupStep> steps = mode switch
        {
            MatSetupMode.Uninstall =>
            [
                MatSetupStep.Uninstall("Programm, Startmenü-Eintrag, Desktop-Verknüpfung und Autostart werden entfernt. Laufende Tunnel werden getrennt."),
            ],
            MatSetupMode.Update =>
            [
                MatSetupStep.Welcome(string.Empty, Note("Ein laufendes ThixTunnel wird beendet; aktive Tunnel werden danach wieder verbunden. Profile, Verbindungen und Einstellungen bleiben erhalten.")),
            ],
            _ =>
            [
                // SETUP-03: for the user, without administrator rights, and where to. The folder on a line of its own: wrapped
                // inside a sentence the path breaks after "C:".
                MatSetupStep.Welcome(
                    "ThixTunnel verwaltet SSH-Tunnel: SSH-Profile, mehrere Hops und lokale Portweiterleitungen, automatisch verbunden und nach Abbrüchen erneut. Es läuft im Infobereich der Taskleiste.",
                    Notes(hasSsh, $"Wird für dein Benutzerkonto installiert – ohne Administratorrechte – nach\n{programFolder}")),
                MatSetupStep.WithOptions(null,
                    MatSetupOption.Autostart("Im Infobereich, ohne Fenster; Verbindungen mit Auto-Verbindung werden aufgebaut"),
                    MatSetupOption.DesktopShortcut("Ein Startmenü-Eintrag wird immer angelegt"),
                    MatSetupOption.AutomaticUpdates("Neue Versionen ohne Rückfrage; aktive Tunnel werden danach wieder verbunden")),
            ],
        };

        var finish = isUninstall
            ? MatSetupStep.Finish(UninstalledText(deleteData: false), offersStart: false)
            : MatSetupStep.Finish($"ThixTunnel {AppInfo.Version} ist installiert. Du findest es im Startmenü und im Infobereich der Taskleiste.");
        var version = isUninstall ? installedVersion ?? AppInfo.Version : AppInfo.Version;
        return new MatSetupWindow(new MatSetup(AppInfo.ProductName, version, AppInfo.Channel, mode, steps, work)
        {
            Logo = Ui.Logo,
            WordMark = WordMark(),
            InstalledVersion = installedVersion ?? "eine ältere Version",
            Finish = finish,
            DeleteDataText = "Auch meine Profile und Verbindungen löschen",
            DeleteDataDescription = "SSH-Profile, Verbindungen, Einstellungen und die von ThixTunnel angelegten Schlüssel auf diesem Computer. Ohne Haken bleiben sie für eine spätere Installation erhalten.",
        });
    }

    /// <summary>The word mark of the title bar ("Thix" and "Tunnel" in the accent text) for the header of the setup.</summary>
    private static TextBlock WordMark()
    {
        var mark = new TextBlock { FontSize = MatTokens.Type.Fs2xl, FontWeight = Avalonia.Media.FontWeight.SemiBold };
        var tunnel = new Run("Tunnel");
        tunnel.Bind(TextElement.ForegroundProperty, mark.GetResourceObservable("Mat.AccentText"));
        mark.Inlines = [new Run("Thix"), tunnel];
        return mark;
    }

    /// <summary>Where it goes; and a warning when the system lacks the OpenSSH client that ThixTunnel needs.</summary>
    private static Control Notes(bool hasSsh, string place)
    {
        var notes = new StackPanel { Spacing = 10 };
        notes.Children.Add(Note(place));
        if (!hasSsh)
        {
            var warning = new InfoBar { Margin = new Avalonia.Thickness(0) };
            warning.Show("Der Windows-OpenSSH-Client (ssh.exe) fehlt. ThixTunnel braucht ihn: Einstellungen › System › Optionale Features › „OpenSSH-Client“.", InfoSeverity.Warning);
            notes.Children.Add(warning);
        }

        return notes;
    }

    /// <summary>A note under the text of a step, in the tint of information.</summary>
    private static InfoBar Note(string text)
    {
        var note = new InfoBar { Margin = new Avalonia.Thickness(0) };
        note.Show(text, InfoSeverity.Info);
        return note;
    }

    private static string UninstalledText(bool deleteData) =>
        deleteData ? "Programm, Profile und Verbindungen wurden entfernt." : "Das Programm wurde entfernt. Deine Profile und Verbindungen bleiben erhalten.";
}
