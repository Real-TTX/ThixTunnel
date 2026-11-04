using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace ThixTunnel;

/// <summary>Look of every ThixTunnel window: theme colours and font (inherited by all text) and a dark title bar.</summary>
internal static class NativeWindow
{
    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmCaptionColor = 35;

    public static void ApplyTheme(Window window)
    {
        window.SetResourceReference(Window.BackgroundProperty, "WindowBrush");
        window.SetResourceReference(Window.ForegroundProperty, "TextBrush");
        window.SetResourceReference(Window.FontFamilyProperty, "UiFont");
        window.FontSize = 14;
        TextOptions.SetTextFormattingMode(window, TextFormattingMode.Display);
        window.UseLayoutRounding = true;
        window.SourceInitialized += (_, _) => UseDarkTitleBar(window);
    }

    /// <summary>Dark title bar matching the window (Windows 10 2004+ / Windows 11).</summary>
    private static void UseDarkTitleBar(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var enabled = 1;
        _ = DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref enabled, sizeof(int));
        var captionColor = 0x0026180F; // #0F1826 as 0x00BBGGRR
        _ = DwmSetWindowAttribute(handle, DwmCaptionColor, ref captionColor, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
