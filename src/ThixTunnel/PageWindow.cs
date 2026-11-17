using Avalonia.Controls;

namespace ThixTunnel;

/// <summary>
/// A separate window around one page (route editor, profile editor, settings, about).
/// The size never exceeds the work area, so every window also fits on small screens.
/// </summary>
public sealed class PageWindow : ThemedWindow
{
    public PageWindow(string key, string title, UserControl page, double width, double height)
    {
        Key = key;
        Title = $"{title} – ThixTunnel";
        Content = page;

        var workArea = WorkArea();
        MinWidth = Math.Min(360, workArea.Width);
        MinHeight = Math.Min(320, workArea.Height);
        Width = Math.Min(width, workArea.Width - 16);
        Height = Math.Min(height, workArea.Height - 16);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }

    /// <summary>Identifies the window, so the same content opens only once (e.g. "route:{id}").</summary>
    public string Key { get; }

    /// <summary>Closes the window that hosts the given page (the "Zurück" button of a page).</summary>
    public static void CloseHostOf(Control page) => Ui.WindowOf(page)?.Close();

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        (Content as IDisposable)?.Dispose();
    }
}
