using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ThixTunnel.Controls;

/// <summary>One switchable option: title, a short description and the toggle switch on the right.</summary>
public sealed class SettingToggle : Border
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(SettingToggle), new PropertyMetadata("", (sender, _) => ((SettingToggle)sender).Refresh()));

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(SettingToggle), new PropertyMetadata("", (sender, _) => ((SettingToggle)sender).Refresh()));

    public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(
        nameof(IsChecked), typeof(bool), typeof(SettingToggle),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (sender, _) => ((SettingToggle)sender).Refresh()));

    private readonly TextBlock _title = new() { FontSize = 15 };
    private readonly TextBlock _description = new() { Margin = new Thickness(0, 3, 0, 0) };
    private readonly CheckBox _switch = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(24, 0, 0, 0) };

    public SettingToggle()
    {
        Padding = new Thickness(0, 12, 0, 12);
        BorderThickness = new Thickness(0, 0, 0, 1);
        SetResourceReference(BorderBrushProperty, "BorderBrush");
        _description.SetResourceReference(StyleProperty, "MutedText");
        _switch.SetResourceReference(StyleProperty, "ToggleSwitch");
        _switch.Click += (_, _) =>
        {
            IsChecked = _switch.IsChecked == true;
            Changed?.Invoke(this, EventArgs.Empty);
        };

        var grid = new Grid { Background = System.Windows.Media.Brushes.Transparent };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var texts = new StackPanel { Children = { _title, _description } };
        Grid.SetColumn(_switch, 1);
        grid.Children.Add(texts);
        grid.Children.Add(_switch);
        grid.MouseLeftButtonUp += (_, args) =>
        {
            if (args.OriginalSource is not ToggleButton && _switch.IsEnabled)
            {
                IsChecked = !IsChecked;
                Changed?.Invoke(this, EventArgs.Empty);
            }
        };
        Child = grid;
    }

    /// <summary>Raised when the user switches.</summary>
    public event EventHandler? Changed;

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public bool IsChecked
    {
        get => (bool)GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    private void Refresh()
    {
        _title.Text = Title;
        _description.Text = Description;
        _description.Visibility = string.IsNullOrEmpty(Description) ? Visibility.Collapsed : Visibility.Visible;
        _switch.IsChecked = IsChecked;
        System.Windows.Automation.AutomationProperties.SetName(_switch, Title);
    }
}
