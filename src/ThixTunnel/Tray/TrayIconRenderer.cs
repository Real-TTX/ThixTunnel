using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace ThixTunnel.Tray;

public enum TrayState
{
    Idle,
    Connected,
    Busy,
    Error,
}

/// <summary>
/// Draws the tray glyph (branding/logo-tray.svg: portal + node, no tile) with a status dot bottom right.
/// The four icons are made once and reused.
/// </summary>
internal static class TrayIconRenderer
{
    private static readonly Dictionary<TrayState, Icon> Cache = [];

    public static Icon Get(TrayState state)
    {
        if (!Cache.TryGetValue(state, out var icon))
        {
            icon = Render(state, Forms.SystemInformation.SmallIconSize.Width);
            Cache[state] = icon;
        }

        return icon;
    }

    private static Icon Render(TrayState state, int size)
    {
        using var bitmap = new Bitmap(size, size);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.Clear(Color.Transparent);
            graphics.ScaleTransform(size / 256f, size / 256f);

            using var portal = new GraphicsPath();
            portal.AddLine(44, 224, 44, 120);
            portal.AddArc(44, 36, 168, 168, 180, 180);
            portal.AddLine(212, 120, 212, 224);
            using var pen = new Pen(Color.FromArgb(0x4D, 0xA3, 0xFF), 44) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            graphics.DrawPath(pen, portal);
            using var node = new SolidBrush(Color.FromArgb(0xE8, 0xEE, 0xF7));
            graphics.FillEllipse(node, 128 - 34, 164 - 34, 68, 68);

            var dotColor = state switch
            {
                TrayState.Connected => Color.FromArgb(0x4A, 0xDE, 0x80),
                TrayState.Busy => Color.FromArgb(0xFB, 0xBF, 0x24),
                TrayState.Error => Color.FromArgb(0xF8, 0x71, 0x71),
                _ => Color.Empty,
            };
            if (dotColor != Color.Empty)
            {
                using var ring = new SolidBrush(Color.FromArgb(0x11, 0x1C, 0x2D));
                graphics.FillEllipse(ring, 140, 140, 116, 116);
                using var dot = new SolidBrush(dotColor);
                graphics.FillEllipse(dot, 158, 158, 80, 80);
            }
        }

        var handle = bitmap.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(handle).Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);
}
