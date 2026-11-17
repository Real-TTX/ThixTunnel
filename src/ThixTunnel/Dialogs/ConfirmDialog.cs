using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MatDesktop;
using ThixTunnel.Controls;

namespace ThixTunnel.Dialogs;

/// <summary>A yes/no question (delete, sign out). Buttons from positive to negative; Esc cancels.</summary>
public sealed class ConfirmDialog : MatWindow
{
    private bool _confirmed;

    private ConfirmDialog(string title, string message, string confirmText, bool isDestructive, bool hasCancel)
    {
        Title = title;
        Width = 440;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        this.Use(BackgroundProperty, "Mat.Surface");

        var confirm = new Button
        {
            Content = new IconLabel { Icon = isDestructive ? "trash" : "check", Text = confirmText },
            IsDefault = !isDestructive,
            Theme = Ui.Theme(isDestructive ? "MatButton.Danger" : "MatButton.Primary"),
        };
        confirm.Click += (_, _) =>
        {
            _confirmed = true;
            Close();
        };

        var actions = new ActionBar { Children = { confirm } };
        if (hasCancel)
        {
            var cancel = new Button { Content = new IconLabel { Icon = "x", Text = "Abbrechen" }, IsCancel = true, IsDefault = isDestructive };
            cancel.Click += (_, _) => Close();
            actions.Children.Add(cancel);
        }

        var heading = new TextBlock { Text = title, FontSize = MatTokens.Type.FsLg, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap };
        var text = new SelectableTextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 22) }.Use(SelectableTextBlock.ForegroundProperty, "Mat.TextMuted");

        Content = new StackPanel { Margin = new Thickness(24, 22), Children = { heading, text, actions } };
        Opened += (_, _) => (hasCancel && isDestructive ? actions.Children[1] : confirm).Focus();
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
