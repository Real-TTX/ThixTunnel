using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;

namespace ThixTunnel.Controls;

/// <summary>Icon + text, the content of most buttons. Takes the foreground of its button (inherited).</summary>
/// <example>&lt;controls:IconLabel Icon="{StaticResource IconSave}" Text="Speichern" /&gt;</example>
public sealed class IconLabel : StackPanel
{
    public static readonly StyledProperty<string> IconProperty = AvaloniaProperty.Register<IconLabel, string>(nameof(Icon), "");

    public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<IconLabel, string>(nameof(Text), "");

    private readonly TextBlock _icon = new() { FontSize = 16, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _text = new() { VerticalAlignment = VerticalAlignment.Center };

    static IconLabel()
    {
        IconProperty.Changed.AddClassHandler<IconLabel>((label, _) => label.Refresh());
        TextProperty.Changed.AddClassHandler<IconLabel>((label, _) => label.Refresh());
    }

    public IconLabel()
    {
        Orientation = Orientation.Horizontal;
        _icon.FontFamily = Ui.IconFont;
        AutomationProperties.SetAccessibilityView(_icon, AccessibilityView.Raw);
        Children.Add(_icon);
        Children.Add(_text);
        Refresh();
    }

    public string Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Screen readers and UI automation read the button by this text.</summary>
    public override string ToString() => Text;

    private void Refresh()
    {
        _icon.Text = Icon;
        _icon.IsVisible = !string.IsNullOrEmpty(Icon);
        _icon.Margin = string.IsNullOrEmpty(Text) ? new Thickness(0) : new Thickness(0, 0, 8, 0);
        _text.Text = Text;
        _text.IsVisible = !string.IsNullOrEmpty(Text);
    }
}
