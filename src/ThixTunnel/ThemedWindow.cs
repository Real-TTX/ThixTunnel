using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace ThixTunnel;

/// <summary>
/// Base of every ThixTunnel window: theme colours and font (inherited by all text), the app icon, and on Windows a
/// title bar in the window colour.
/// </summary>
public class ThemedWindow : Window
{
    private static WindowIcon? _appIcon;

    public ThemedWindow()
    {
        Background = Ui.Brush("WindowBrush");
        Foreground = Ui.Brush("TextBrush");
        FontFamily = (Avalonia.Media.FontFamily)Ui.Resource("UiFont");
        FontSize = 14;
        Icon = AppIcon;
        UseLayoutRounding = true;
        Opened += (_, _) => UseDarkTitleBar();
    }

    protected override Type StyleKeyOverride => typeof(Window);

    public static WindowIcon AppIcon => _appIcon ??= new WindowIcon(AssetLoader.Open(new Uri("avares://ThixTunnel/Assets/thixtunnel.ico")));

    public static Bitmap Logo() => new(AssetLoader.Open(new Uri("avares://ThixTunnel/Assets/logo-256.png")));

    /// <summary>The usable area of the screen the window is on (or the primary screen); never larger than the screen.</summary>
    public Rect WorkArea()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null)
        {
            return new Rect(0, 0, 1280, 800);
        }

        var area = screen.WorkingArea;
        return new Rect(area.X / screen.Scaling, area.Y / screen.Scaling, area.Width / screen.Scaling, area.Height / screen.Scaling);
    }

    /// <summary>Brings the window to the front, also when it was minimized or hidden.</summary>
    public void BringToFront()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    /// <summary>Windows 11: caption colour = window colour (Windows 10 2004+: at least dark).</summary>
    private void UseDarkTitleBar()
    {
        if (!OperatingSystem.IsWindows() || TryGetPlatformHandle()?.Handle is not { } handle)
        {
            return;
        }

        var enabled = 1;
        _ = DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int)); // DWMWA_USE_IMMERSIVE_DARK_MODE
        var captionColor = 0x0026180F; // #0F1826 as 0x00BBGGRR
        _ = DwmSetWindowAttribute(handle, 35, ref captionColor, sizeof(int)); // DWMWA_CAPTION_COLOR
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
