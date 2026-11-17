using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace ThixTunnel.Tray;

public enum TrayState
{
    Idle,
    Connected,
    Busy,
    Error,
}

/// <summary>
/// Draws the tray / menu bar glyph (branding/logo-tray.svg: portal + node, no tile) with a status dot bottom right.
/// The four icons are made once and reused.
/// </summary>
internal static class TrayIconRenderer
{
    private const int Size = 64;

    private static readonly Dictionary<TrayState, WindowIcon> Cache = [];

    public static WindowIcon Get(TrayState state)
    {
        if (!Cache.TryGetValue(state, out var icon))
        {
            icon = Render(state);
            Cache[state] = icon;
        }

        return icon;
    }

    private static WindowIcon Render(TrayState state)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize(Size, Size), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        {
            using var scale = context.PushTransform(Matrix.CreateScale(Size / 256.0, Size / 256.0));

            var portal = new StreamGeometry();
            using (var figure = portal.Open())
            {
                figure.BeginFigure(new Point(44, 224), isFilled: false);
                figure.LineTo(new Point(44, 120));
                figure.ArcTo(new Point(212, 120), new Size(84, 84), 0, isLargeArc: false, SweepDirection.Clockwise);
                figure.LineTo(new Point(212, 224));
                figure.EndFigure(isClosed: false);
            }

            context.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromRgb(0x4D, 0xA3, 0xFF)), 44, lineCap: PenLineCap.Round), portal);
            context.DrawEllipse(new SolidColorBrush(Color.FromRgb(0xE8, 0xEE, 0xF7)), null, new Point(128, 164), 34, 34);

            Color? dot = state switch
            {
                TrayState.Connected => Color.FromRgb(0x4A, 0xDE, 0x80),
                TrayState.Busy => Color.FromRgb(0xFB, 0xBF, 0x24),
                TrayState.Error => Color.FromRgb(0xF8, 0x71, 0x71),
                _ => null,
            };
            if (dot is { } color)
            {
                context.DrawEllipse(new SolidColorBrush(Color.FromRgb(0x11, 0x1C, 0x2D)), null, new Point(198, 198), 58, 58);
                context.DrawEllipse(new SolidColorBrush(color), null, new Point(198, 198), 40, 40);
            }
        }

        using var png = new MemoryStream();
        bitmap.Save(png);
        png.Position = 0;
        return new WindowIcon(png);
    }
}
