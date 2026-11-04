using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ThixTunnel.Controls;

/// <summary>Icon + text, the content of most buttons. Takes the foreground of its button.</summary>
public sealed class IconLabel : StackPanel
{
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(string), typeof(IconLabel), new PropertyMetadata("", (sender, _) => ((IconLabel)sender).Refresh()));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(IconLabel), new PropertyMetadata("", (sender, _) => ((IconLabel)sender).Refresh()));

    private readonly TextBlock _icon = new() { Margin = new Thickness(0, 0, 8, 0), FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _text = new() { VerticalAlignment = VerticalAlignment.Center };

    public IconLabel()
    {
        Orientation = Orientation.Horizontal;
        _icon.SetResourceReference(TextBlock.FontFamilyProperty, "IconFont");
        foreach (var block in new[] { _icon, _text })
        {
            block.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding(nameof(Control.Foreground))
            {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(Control), 1),
            });
        }

        Children.Add(_icon);
        Children.Add(_text);
    }

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private void Refresh()
    {
        _icon.Text = Icon;
        _icon.Visibility = string.IsNullOrEmpty(Icon) ? Visibility.Collapsed : Visibility.Visible;
        _icon.Margin = string.IsNullOrEmpty(Text) ? new Thickness(0) : new Thickness(0, 0, 8, 0);
        _text.Text = Text;
        _text.Visibility = string.IsNullOrEmpty(Text) ? Visibility.Collapsed : Visibility.Visible;
    }
}
