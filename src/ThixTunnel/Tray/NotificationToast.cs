using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace ThixTunnel.Tray;

/// <summary>
/// A short notification next to the tray (Windows: bottom right, macOS: top right below the menu bar).
/// Disappears after a few seconds; a click opens ThixTunnel. Never takes the keyboard focus.
/// </summary>
internal sealed class NotificationToast : ThemedWindow
{
    private static readonly List<NotificationToast> Open = [];

    private NotificationToast(string title, string text, bool isWarning, Action onClick)
    {
        Width = 360;
        SizeToContent = SizeToContent.Height;
        SystemDecorations = SystemDecorations.None;
        Topmost = true;
        ShowActivated = false;
        ShowInTaskbar = false;
        CanResize = false;
        Background = Ui.Brush("TileBrush");

        var icon = new TextBlock
        {
            Text = Ui.Icon(isWarning ? "IconWarning" : "IconInfo"),
            FontFamily = Ui.IconFont,
            FontSize = 18,
            Foreground = Ui.Brush(isWarning ? "WarningBrush" : "AccentHoverBrush"),
            Margin = new Thickness(0, 1, 12, 0),
            VerticalAlignment = VerticalAlignment.Top,
        };
        DockPanel.SetDock(icon, Dock.Left);
        var texts = new StackPanel
        {
            Children =
            {
                new TextBlock { Text = title, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = Ui.Brush("TextMutedBrush"), Margin = new Thickness(0, 3, 0, 0), MaxHeight = 120 },
            },
        };

        Content = new Border
        {
            BorderBrush = Ui.Brush("BorderBrush"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16, 14),
            Child = new DockPanel { Children = { icon, texts } },
        };

        PointerPressed += (_, args) =>
        {
            if (args.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                Close();
                onClick();
            }
        };
        Opened += (_, _) => PlaceInCorner();
        Closed += (_, _) =>
        {
            Open.Remove(this);
            RestackAll();
        };
        DispatcherTimer.RunOnce(Close, TimeSpan.FromSeconds(7));
    }

    public static void Show(string title, string text, bool isWarning, Action onClick)
    {
        var toast = new NotificationToast(title, text, isWarning, onClick);
        Open.Add(toast);
        toast.Show();
    }

    private static void RestackAll()
    {
        foreach (var toast in Open)
        {
            toast.PlaceInCorner();
        }
    }

    private void PlaceInCorner()
    {
        var screen = Screens.Primary;
        if (screen is null)
        {
            return;
        }

        var area = screen.WorkingArea;
        var scaling = screen.Scaling;
        var width = (int)(Bounds.Width * scaling);
        var offset = Open.TakeWhile(toast => toast != this).Sum(toast => (int)((toast.Bounds.Height + 10) * scaling));
        var margin = (int)(12 * scaling);
        var height = (int)(Bounds.Height * scaling);
        Position = OperatingSystem.IsMacOS()
            ? new PixelPoint(area.Right - width - margin, area.Y + margin + offset)
            : new PixelPoint(area.Right - width - margin, area.Bottom - height - margin - offset);
    }
}
