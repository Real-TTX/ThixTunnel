using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MatDesktop;
using ThixTunnel.Controls;
using ThixTunnel.Core.Infrastructure;

namespace ThixTunnel.Dialogs;

/// <summary>
/// The SSH_ASKPASS dialog. ssh starts the ThixTunnel program with the prompt as argument; the answer goes to standard
/// output (straight to ssh). Two kinds: a secret (password, passphrase) or a yes/no question (unknown host key). A dialog
/// of the program's own in MatDesktop's chrome: header with logo and title, the question, the footer with the primary
/// button and Abbrechen.
/// </summary>
public sealed class AskPassWindow : MatWindow
{
    private readonly TextBox _secret = new() { PasswordChar = '●' };

    public AskPassWindow(string routeName, string prompt)
    {
        Avalonia.Automation.AutomationProperties.SetName(_secret, "Passwort");
        var isConfirmation = AskPassProtocol.IsConfirmation(prompt);
        Title = isConfirmation ? "ThixTunnel – Hostschlüssel bestätigen" : "ThixTunnel – Anmeldung";
        Width = isConfirmation ? 620 : 460;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;
        this.Use(BackgroundProperty, "Mat.Surface");

        var body = new StackPanel { Margin = new Thickness(28, 20, 28, 22) };
        body.Children.Add(Ui.Text($"Verbindung „{routeName}“", "MatText.Muted"));
        var promptText = new SelectableTextBlock { Text = prompt.Trim(), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 14) };
        if (isConfirmation)
        {
            promptText.Use(SelectableTextBlock.FontFamilyProperty, "Mat.Mono");
            promptText.FontSize = MatTokens.Type.FsSm;
        }

        body.Children.Add(promptText);

        Button confirm;
        if (isConfirmation)
        {
            var warning = new InfoBar { Margin = new Thickness(0) };
            warning.Show("Nur bestätigen, wenn der Fingerabdruck mit dem des Servers übereinstimmt. Der Schlüssel wird danach in known_hosts gespeichert.", InfoSeverity.Warning);
            body.Children.Add(warning);
            confirm = Ui.Button("shield", "Vertrauen und verbinden", "MatButton.Primary", () => Finish("yes"));
        }
        else
        {
            body.Children.Add(_secret);
            confirm = Ui.Button("login", "Anmelden", "MatButton.Primary", () => Finish(_secret.Text ?? ""));
            confirm.IsDefault = true;
            Opened += (_, _) => _secret.Focus();
        }

        var cancel = Ui.Button("x", "Abbrechen", click: Close);
        cancel.IsCancel = true;
        var header = MatDialogChrome.Header(Ui.Logo, "ThixTunnel", isConfirmation ? "Unbekannter Hostschlüssel" : "Anmeldung");
        Content = MatDialogChrome.Compose(header, body, MatDialogChrome.Footer(null, confirm, cancel));
    }

    /// <summary>The answer for ssh; null when cancelled.</summary>
    public string? Answer { get; private set; }

    /// <summary>Shows the dialog; when it closes, writes the answer and reports the exit code for ssh (0 = answered).</summary>
    public static void Run(string routeName, string prompt, Action<int> finished)
    {
        var window = new AskPassWindow(routeName, prompt);
        window.Closed += (_, _) =>
        {
            if (window.Answer is null)
            {
                finished(1);
                return;
            }

            using (var output = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true, NewLine = "\n" })
            {
                output.WriteLine(window.Answer);
            }

            finished(0);
        };
        window.Show();
        window.Activate();
    }

    private void Finish(string answer)
    {
        Answer = answer;
        Close();
    }
}
