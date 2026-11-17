using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace ThixTunnel.Controls;

/// <summary>One switchable option: title, a short description and the toggle switch on the right.</summary>
public sealed class SettingToggle : Border
{
    public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<SettingToggle, string>(nameof(Title), "");

    public static readonly StyledProperty<string> DescriptionProperty = AvaloniaProperty.Register<SettingToggle, string>(nameof(Description), "");

    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<SettingToggle, bool>(nameof(IsChecked), defaultBindingMode: BindingMode.TwoWay);

    private readonly TextBlock _title = new() { FontSize = 15, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _description = new() { Margin = new Thickness(0, 3, 0, 0) };
    private readonly ToggleSwitch _switch = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(24, 0, 0, 0),
        OnContent = null,
        OffContent = null,
        MinWidth = 0,
    };

    private bool _isRefreshing;

    static SettingToggle()
    {
        TitleProperty.Changed.AddClassHandler<SettingToggle>((toggle, _) => toggle.Refresh());
        DescriptionProperty.Changed.AddClassHandler<SettingToggle>((toggle, _) => toggle.Refresh());
        IsCheckedProperty.Changed.AddClassHandler<SettingToggle>((toggle, _) => toggle.Refresh());
    }

    public SettingToggle()
    {
        Padding = new Thickness(0, 12, 0, 12);
        BorderThickness = new Thickness(0, 0, 0, 1);
        BorderBrush = Ui.Brush("BorderBrush");
        _description.Theme = Ui.Theme("MutedText");
        _switch.IsCheckedChanged += (_, _) =>
        {
            if (_isRefreshing)
            {
                return;
            }

            IsChecked = _switch.IsChecked == true;
            Changed?.Invoke(this, EventArgs.Empty);
        };

        var grid = new Grid { Background = Brushes.Transparent, ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var texts = new StackPanel { Children = { _title, _description } };
        Grid.SetColumn(_switch, 1);
        grid.Children.Add(texts);
        grid.Children.Add(_switch);

        // A click on the text switches too.
        texts.Background = Brushes.Transparent;
        texts.PointerReleased += (_, args) =>
        {
            if (args.InitialPressMouseButton == MouseButton.Left && _switch.IsEnabled && IsEffectivelyEnabled)
            {
                IsChecked = !IsChecked;
                Changed?.Invoke(this, EventArgs.Empty);
            }
        };
        Child = grid;
        Refresh();
    }

    /// <summary>Raised when the user switches.</summary>
    public event EventHandler? Changed;

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public bool IsChecked
    {
        get => GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    private void Refresh()
    {
        _isRefreshing = true;
        _title.Text = Title;
        _description.Text = Description;
        _description.IsVisible = !string.IsNullOrEmpty(Description);
        _switch.IsChecked = IsChecked;
        AutomationProperties.SetName(_switch, Title);
        _isRefreshing = false;
    }
}
