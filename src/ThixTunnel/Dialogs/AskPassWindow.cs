using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ThixTunnel.Controls;
using ThixTunnel.Core.Infrastructure;

namespace ThixTunnel.Dialogs;

/// <summary>
/// The SSH_ASKPASS dialog. ssh starts ThixTunnel.exe with the prompt as argument; the answer goes to standard
/// output (straight to ssh). Two kinds: a secret (password, passphrase) or a yes/no question (unknown host key).
/// </summary>
public sealed class AskPassWindow : Window
{
    private readonly PasswordBox _secret = new();

    private AskPassWindow(string routeName, string prompt)
    {
        var isConfirmation = AskPassProtocol.IsConfirmation(prompt);
        Title = isConfirmation ? "ThixTunnel – Hostschlüssel bestätigen" : "ThixTunnel – Anmeldung";
        Width = isConfirmation ? 620 : 460;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;
        NativeWindow.ApplyTheme(this);

        var heading = new TextBlock
        {
            Text = isConfirmation ? "Unbekannter Hostschlüssel" : "Anmeldung erforderlich",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
        };
        var route = new TextBlock { Text = $"Tunnel „{routeName}“", Margin = new Thickness(0, 2, 0, 14) };
        route.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");

        var promptText = new TextBlock { Text = prompt.Trim(), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 14) };
        if (isConfirmation)
        {
            promptText.SetResourceReference(StyleProperty, "MonoText");
            promptText.FontSize = 12.5;
        }

        var panel = new StackPanel { Margin = new Thickness(24), Children = { heading, route, promptText } };

        Button confirm;
        if (isConfirmation)
        {
            var warning = new InfoBar();
            warning.Show("Nur bestätigen, wenn der Fingerabdruck mit dem des Servers übereinstimmt. Der Schlüssel wird danach in known_hosts gespeichert.", InfoSeverity.Warning);
            panel.Children.Add(warning);
            confirm = new Button { Content = new IconLabel { Icon = (string)FindResource("IconCheck"), Text = "Vertrauen und verbinden" } };
            confirm.Click += (_, _) => Finish("yes");
        }
        else
        {
            panel.Children.Add(_secret);
            confirm = new Button { Content = new IconLabel { Icon = (string)FindResource("IconConnect"), Text = "Anmelden" }, IsDefault = true };
            confirm.Click += (_, _) => Finish(_secret.Password);
            Loaded += (_, _) => _secret.Focus();
        }

        confirm.SetResourceReference(StyleProperty, "PrimaryButton");
        var cancel = new Button { Content = new IconLabel { Icon = (string)FindResource("IconBack"), Text = "Abbrechen" }, IsCancel = true };
        cancel.Click += (_, _) => Close();
        panel.Children.Add(new ActionBar { Margin = new Thickness(0, 16, 0, 0), Children = { confirm, cancel } });
        Content = panel;
    }

    /// <summary>The answer for ssh; null when cancelled.</summary>
    public string? Answer { get; private set; }

    /// <summary>Shows the dialog and writes the answer; returns the exit code for ssh (0 = answered).</summary>
    public static int Run(string routeName, string prompt)
    {
        var window = new AskPassWindow(routeName, prompt);
        window.ShowDialog();
        if (window.Answer is null)
        {
            return 1;
        }

        using var output = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true, NewLine = "\n" };
        output.WriteLine(window.Answer);
        return 0;
    }

    private void Finish(string answer)
    {
        Answer = answer;
        Close();
    }
}
