using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using MatDesktop;

namespace ThixTunnel.Controls;

/// <summary>
/// Icon and text side by side: the content of most buttons ("Speichern", "Installieren" ...). <see cref="Icon"/> is the name of
/// an icon of MatDesktop's sprite (DESK-09). <see cref="IsIconFilled"/> fills it (a chosen favourite, a pinned entry):
/// MatIcon.IsFilled, the same geometry. <see cref="IsTextVisible"/> false leaves the icon alone; the button's tool tip then
/// names it.
/// </summary>
public sealed class IconLabel : StackPanel
{
    public static readonly StyledProperty<string?> IconProperty = AvaloniaProperty.Register<IconLabel, string?>(nameof(Icon));

    public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<IconLabel, string>(nameof(Text), "");

    public static readonly StyledProperty<bool> IsIconFilledProperty = AvaloniaProperty.Register<IconLabel, bool>(nameof(IsIconFilled));

    public static readonly StyledProperty<bool> IsTextVisibleProperty = AvaloniaProperty.Register<IconLabel, bool>(nameof(IsTextVisible), true);

    public static readonly StyledProperty<double> IconSizeProperty = AvaloniaProperty.Register<IconLabel, double>(nameof(IconSize), 16);

    private readonly MatIcon _icon = new();
    private readonly TextBlock _text = new() { VerticalAlignment = VerticalAlignment.Center };

    static IconLabel()
    {
        IconProperty.Changed.AddClassHandler<IconLabel>((label, _) => label.Refresh());
        TextProperty.Changed.AddClassHandler<IconLabel>((label, _) => label.Refresh());
        IsIconFilledProperty.Changed.AddClassHandler<IconLabel>((label, _) => label.Refresh());
        IsTextVisibleProperty.Changed.AddClassHandler<IconLabel>((label, _) => label.Refresh());
        IconSizeProperty.Changed.AddClassHandler<IconLabel>((label, _) => label.Refresh());
    }

    public IconLabel()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 8;
        Children.Add(_icon);
        Children.Add(_text);
        Refresh();
    }

    /// <summary>The name of the icon in the sprite ("copy", "trash" ...).</summary>
    public string? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool IsIconFilled
    {
        get => GetValue(IsIconFilledProperty);
        set => SetValue(IsIconFilledProperty, value);
    }

    public bool IsTextVisible
    {
        get => GetValue(IsTextVisibleProperty);
        set => SetValue(IsTextVisibleProperty, value);
    }

    public double IconSize
    {
        get => GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    private void Refresh()
    {
        _icon.Icon = Icon;
        _icon.Size = IconSize;
        _icon.IsFilled = IsIconFilled;
        _icon.IsVisible = !string.IsNullOrEmpty(Icon);
        _text.Text = Text;
        _text.IsVisible = IsTextVisible && Text.Length > 0;
    }
}
