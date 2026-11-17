using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MatDesktop;

namespace ThixTunnel.Controls;

public enum InfoSeverity
{
    Info,
    Success,
    Warning,
    Error,
}

/// <summary>
/// A message in a tinted box with an icon (results, hints, errors without a field), like MatUi's alert (mat-alert): the
/// tint of the state behind it, the icon in the text colour of the state, the message in the text colour. Hidden until shown.
/// </summary>
public sealed class InfoBar : Border
{
    private readonly MatIcon _icon = new() { Size = 18, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 1, 10, 0) };
    private readonly SelectableTextBlock _text = new() { TextWrapping = TextWrapping.Wrap };

    public InfoBar()
    {
        IsVisible = false;
        this.Use(CornerRadiusProperty, "Mat.Radius");
        Padding = new Thickness(MatTokens.Size.Space4, MatTokens.Size.Space3);
        Margin = new Thickness(0, 0, 0, 12);
        _text.Use(SelectableTextBlock.ForegroundProperty, "Mat.Text");
        var dock = new DockPanel();
        DockPanel.SetDock(_icon, Dock.Left);
        dock.Children.Add(_icon);
        dock.Children.Add(_text);
        Child = dock;
    }

    /// <summary>The message shown (tests).</summary>
    public string? Text => IsVisible ? _text.Text : null;

    public InfoSeverity Severity { get; private set; }

    public void Show(string message, InfoSeverity severity)
    {
        var (state, icon) = severity switch
        {
            InfoSeverity.Success => ("Success", "check-circle"),
            InfoSeverity.Warning => ("Warning", "alert"),
            InfoSeverity.Error => ("Danger", "x-circle"),
            _ => ("Info", "info"),
        };
        Severity = severity;
        this.Use(BackgroundProperty, $"Mat.{state}Bg");
        _icon.Use(MatIcon.ForegroundProperty, $"Mat.{state}Text");
        _icon.Icon = icon;
        _text.Text = message;
        IsVisible = true;
    }

    public void Hide() => IsVisible = false;
}
