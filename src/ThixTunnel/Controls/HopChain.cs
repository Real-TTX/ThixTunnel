using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace ThixTunnel.Controls;

/// <summary>The SSH hops of a route as numbered chips with arrows: [1 Profil 1] → [2 Profil 2] → [Ziel 127.0.0.1:4453].</summary>
public sealed class HopChain : WrapPanel
{
    public static readonly StyledProperty<IReadOnlyList<string>?> HopsProperty =
        AvaloniaProperty.Register<HopChain, IReadOnlyList<string>?>(nameof(Hops));

    public static readonly StyledProperty<string> TargetProperty = AvaloniaProperty.Register<HopChain, string>(nameof(Target), "");

    static HopChain()
    {
        HopsProperty.Changed.AddClassHandler<HopChain>((chain, _) => chain.Refresh());
        TargetProperty.Changed.AddClassHandler<HopChain>((chain, _) => chain.Refresh());
    }

    public IReadOnlyList<string>? Hops
    {
        get => GetValue(HopsProperty);
        set => SetValue(HopsProperty, value);
    }

    /// <summary>Optional last chip: the forwarding target.</summary>
    public string Target
    {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    private void Refresh()
    {
        Children.Clear();
        var hops = Hops ?? [];
        for (var index = 0; index < hops.Count; index++)
        {
            if (index > 0)
            {
                Children.Add(Arrow());
            }

            Children.Add(Chip((index + 1).ToString(), hops[index]));
        }

        if (!string.IsNullOrEmpty(Target))
        {
            if (hops.Count > 0)
            {
                Children.Add(Arrow());
            }

            Children.Add(Chip("Ziel", Target));
        }
    }

    private static Border Chip(string number, string text)
    {
        var numberBadge = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(7, 1, 7, 2),
            Margin = new Thickness(0, 0, 10, 0),
            Background = Ui.Brush("SelectionBrush"),
            Child = new TextBlock { Text = number, FontSize = 13, FontWeight = FontWeight.SemiBold },
        };

        return new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = Ui.Brush("BorderBrush"),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 6, 12, 6),
            Margin = new Thickness(0, 0, 0, 8),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children = { numberBadge, new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center } },
            },
        };
    }

    private static TextBlock Arrow() => new()
    {
        Margin = new Thickness(12, 0, 12, 8),
        VerticalAlignment = VerticalAlignment.Center,
        FontSize = 14,
        FontFamily = Ui.IconFont,
        Text = Ui.Icon("IconArrowRight"),
        Foreground = Ui.Brush("TextMutedBrush"),
    };
}
