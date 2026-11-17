using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using ThixTunnel.Core.Models;

namespace ThixTunnel.Controls;

/// <summary>The pill with a coloured dot and the state as text ("Verbunden", "Getrennt", ...).</summary>
public sealed class StatusBadge : Border
{
    public static readonly StyledProperty<TunnelStatus?> StatusProperty = AvaloniaProperty.Register<StatusBadge, TunnelStatus?>(nameof(Status));

    private readonly Ellipse _dot = new() { Width = 9, Height = 9, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _text = new() { FontSize = 13, VerticalAlignment = VerticalAlignment.Center };

    static StatusBadge()
    {
        StatusProperty.Changed.AddClassHandler<StatusBadge>((badge, _) => badge.Refresh());
    }

    public StatusBadge()
    {
        CornerRadius = new CornerRadius(12);
        Padding = new Thickness(10, 4, 12, 4);
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Center;
        Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { _dot, _text } };
        Refresh();
    }

    public TunnelStatus? Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public static string Label(TunnelStatus? status) => status?.State switch
    {
        TunnelState.Connected => "Verbunden",
        TunnelState.Connecting => "Verbindet …",
        TunnelState.WaitingForRetry => "Wartet",
        TunnelState.Failed => "Fehler",
        _ => "Getrennt",
    };

    /// <summary>Resource keys of dot and background colour for a state (shared with the tray).</summary>
    public static (string Dot, string Background) BrushKeys(TunnelStatus? status) => status?.State switch
    {
        TunnelState.Connected => ("SuccessBrush", "SuccessSoftBrush"),
        TunnelState.Connecting or TunnelState.WaitingForRetry => ("WarningBrush", "WarningSoftBrush"),
        TunnelState.Failed => ("DangerBrush", "DangerSoftBrush"),
        _ => ("TextMutedBrush", "NeutralSoftBrush"),
    };

    private void Refresh()
    {
        var (dot, background) = BrushKeys(Status);
        _dot.Fill = Ui.Brush(dot);
        Background = Ui.Brush(background);
        _text.Text = Label(Status);
        ToolTip.SetTip(this, string.IsNullOrEmpty(Status?.Message) ? null : Status.Message);
    }
}
