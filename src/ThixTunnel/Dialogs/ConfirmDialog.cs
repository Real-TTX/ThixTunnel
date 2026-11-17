using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ThixTunnel.Controls;

namespace ThixTunnel.Dialogs;

/// <summary>A dark yes/no question (delete, quit with active tunnels). Buttons from positive to negative.</summary>
public sealed class ConfirmDialog : ThemedWindow
{
    private bool _confirmed;

    private ConfirmDialog(string title, string message, string confirmText, bool isDestructive, bool hasCancel)
    {
        Title = title;
        Width = 460;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var confirm = new Button
        {
            Content = new IconLabel { Icon = Ui.Icon(isDestructive ? "IconDelete" : "IconCheck"), Text = confirmText },
            IsDefault = !isDestructive,
            Theme = Ui.Theme(isDestructive ? "DangerButton" : "PrimaryButton"),
        };
        confirm.Click += (_, _) =>
        {
            _confirmed = true;
            Close();
        };

        var actions = new ActionBar { Children = { confirm } };
        if (hasCancel)
        {
            var cancel = new Button { Content = new IconLabel { Icon = Ui.Icon("IconBack"), Text = "Abbrechen" }, IsCancel = true, IsDefault = isDestructive };
            cancel.Click += (_, _) => Close();
            actions.Children.Add(cancel);
        }

        var heading = new TextBlock { Text = title, FontSize = 18, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap };
        var text = new SelectableTextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 22), Foreground = Ui.Brush("TextMutedBrush") };

        Content = new StackPanel { Margin = new Thickness(24), Children = { heading, text, actions } };
    }

    /// <summary>Asks the question; true when the user confirmed.</summary>
    public static Task<bool> AskAsync(Window? owner, string title, string message, string confirmText, bool isDestructive = false) =>
        ShowAsync(new ConfirmDialog(title, message, confirmText, isDestructive, hasCancel: true), owner);

    /// <summary>A message with only an "OK" button.</summary>
    public static Task ShowMessageAsync(Window? owner, string title, string message) =>
        ShowAsync(new ConfirmDialog(title, message, "OK", isDestructive: false, hasCancel: false), owner);

    private static async Task<bool> ShowAsync(ConfirmDialog dialog, Window? owner)
    {
        if (owner is { IsVisible: true })
        {
            await dialog.ShowDialog(owner);
            return dialog._confirmed;
        }

        // No visible window (tray): an own window in the middle of the screen.
        var closed = new TaskCompletionSource();
        dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        dialog.Topmost = true;
        dialog.ShowInTaskbar = true;
        dialog.Closed += (_, _) => closed.TrySetResult();
        dialog.Show();
        dialog.Activate();
        await closed.Task;
        return dialog._confirmed;
    }
}
