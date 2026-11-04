using System.Windows;
using System.Windows.Controls;

namespace ThixTunnel.Controls;

public enum InfoSeverity
{
    Info,
    Success,
    Warning,
    Error,
}

/// <summary>A message line with an icon (saved, error, hint). Hidden while it has no text.</summary>
public sealed class InfoBar : Border
{
    private readonly TextBlock _icon = new() { Margin = new Thickness(0, 1, 12, 0), FontSize = 16, VerticalAlignment = VerticalAlignment.Top };
    private readonly TextBlock _text = new() { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };

    public InfoBar()
    {
        CornerRadius = new CornerRadius(8);
        BorderThickness = new Thickness(1);
        Padding = new Thickness(14, 10, 14, 10);
        Margin = new Thickness(0, 0, 0, 16);
        _icon.SetResourceReference(TextBlock.FontFamilyProperty, "IconFont");
        Child = new DockPanel { Children = { _icon, _text } };
        DockPanel.SetDock(_icon, Dock.Left);
        Visibility = Visibility.Collapsed;
    }

    public string Text
    {
        get => _text.Text;
        set => Show(value, InfoSeverity.Info);
    }

    public void Show(string message, InfoSeverity severity)
    {
        _text.Text = message;
        var (icon, brush, background) = severity switch
        {
            InfoSeverity.Success => ("IconCheck", "SuccessBrush", "SuccessSoftBrush"),
            InfoSeverity.Warning => ("IconWarning", "WarningBrush", "WarningSoftBrush"),
            InfoSeverity.Error => ("IconWarning", "DangerBrush", "DangerSoftBrush"),
            _ => ("IconInfo", "AccentHoverBrush", "NeutralSoftBrush"),
        };
        _icon.SetResourceReference(TextBlock.TextProperty, icon);
        _icon.SetResourceReference(TextBlock.ForegroundProperty, brush);
        SetResourceReference(BackgroundProperty, background);
        SetResourceReference(BorderBrushProperty, background);
        Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
    }

    public void Clear() => Show("", InfoSeverity.Info);
}
