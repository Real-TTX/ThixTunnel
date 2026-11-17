using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ThixTunnel.Controls;
using ThixTunnel.Core.Infrastructure;

namespace ThixTunnel.Dialogs;

/// <summary>
/// The SSH_ASKPASS dialog. ssh starts the ThixTunnel program with the prompt as argument; the answer goes to standard
/// output (straight to ssh). Two kinds: a secret (password, passphrase) or a yes/no question (unknown host key).
/// </summary>
public sealed class AskPassWindow : ThemedWindow
{
    private readonly TextBox _secret = new() { PasswordChar = '●' };

    private AskPassWindow(string routeName, string prompt)
    {
        Avalonia.Automation.AutomationProperties.SetName(_secret, "Passwort");
        var isConfirmation = AskPassProtocol.IsConfirmation(prompt);
        Title = isConfirmation ? "ThixTunnel – Hostschlüssel bestätigen" : "ThixTunnel – Anmeldung";
        Width = isConfirmation ? 620 : 460;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;

        var heading = new TextBlock
        {
            Text = isConfirmation ? "Unbekannter Hostschlüssel" : "Anmeldung erforderlich",
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
        };
        var route = new TextBlock { Text = $"Tunnel „{routeName}“", Margin = new Thickness(0, 2, 0, 14), Foreground = Ui.Brush("TextMutedBrush") };
        var promptText = new SelectableTextBlock { Text = prompt.Trim(), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 14) };
        if (isConfirmation)
        {
            promptText.Theme = Ui.Theme("MonoText");
            promptText.FontSize = 12.5;
        }

        var panel = new StackPanel { Margin = new Thickness(24), Children = { heading, route, promptText } };

        Button confirm;
        if (isConfirmation)
        {
            var warning = new InfoBar();
            warning.Show("Nur bestätigen, wenn der Fingerabdruck mit dem des Servers übereinstimmt. Der Schlüssel wird danach in known_hosts gespeichert.", InfoSeverity.Warning);
            panel.Children.Add(warning);
            confirm = new Button { Content = new IconLabel { Icon = Ui.Icon("IconCheck"), Text = "Vertrauen und verbinden" } };
            confirm.Click += (_, _) => Finish("yes");
        }
        else
        {
            panel.Children.Add(_secret);
            confirm = new Button { Content = new IconLabel { Icon = Ui.Icon("IconConnect"), Text = "Anmelden" }, IsDefault = true };
            confirm.Click += (_, _) => Finish(_secret.Text ?? "");
            Opened += (_, _) => _secret.Focus();
        }

        confirm.Theme = Ui.Theme("PrimaryButton");
        var cancel = new Button { Content = new IconLabel { Icon = Ui.Icon("IconBack"), Text = "Abbrechen" }, IsCancel = true };
        cancel.Click += (_, _) => Close();
        panel.Children.Add(new ActionBar { Margin = new Thickness(0, 16, 0, 0), Children = { confirm, cancel } });
        Content = panel;
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
