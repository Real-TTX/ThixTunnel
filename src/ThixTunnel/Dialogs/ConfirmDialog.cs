using System.Windows;
using System.Windows.Controls;
using ThixTunnel.Controls;

namespace ThixTunnel.Dialogs;

/// <summary>A dark yes/no question (delete, quit with active tunnels). Buttons from positive to negative.</summary>
public sealed class ConfirmDialog : Window
{
    private ConfirmDialog(string title, string message, string confirmText, bool isDestructive)
    {
        Title = title;
        Width = 460;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        NativeWindow.ApplyTheme(this);

        var confirm = new Button { Content = new IconLabel { Icon = (string)FindResource(isDestructive ? "IconDelete" : "IconCheck"), Text = confirmText }, IsDefault = !isDestructive };
        confirm.SetResourceReference(StyleProperty, isDestructive ? "DangerButton" : "PrimaryButton");
        confirm.Click += (_, _) => DialogResult = true;

        var cancel = new Button { Content = new IconLabel { Icon = (string)FindResource("IconBack"), Text = "Abbrechen" }, IsCancel = true, IsDefault = isDestructive };

        var heading = new TextBlock { Text = title, FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) };
        var text = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 22) };
        text.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");

        Content = new StackPanel
        {
            Margin = new Thickness(24),
            Children = { heading, text, new ActionBar { Children = { confirm, cancel } } },
        };
    }

    public static bool Ask(Window? owner, string title, string message, string confirmText, bool isDestructive = false)
    {
        var dialog = new ConfirmDialog(title, message, confirmText, isDestructive);
        if (owner is { IsVisible: true })
        {
            dialog.Owner = owner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            dialog.Topmost = true;
        }

        return dialog.ShowDialog() == true;
    }
}
