using System.Windows;
using System.Windows.Controls;

namespace ThixTunnel.Controls;

/// <summary>The SSH hops of a route as numbered chips with arrows: [1 Profil 1] → [2 Profil 2] → [Ziel 127.0.0.1:4453].</summary>
public sealed class HopChain : WrapPanel
{
    public static readonly DependencyProperty HopsProperty = DependencyProperty.Register(
        nameof(Hops), typeof(IReadOnlyList<string>), typeof(HopChain), new PropertyMetadata(null, (sender, _) => ((HopChain)sender).Refresh()));

    public static readonly DependencyProperty TargetProperty = DependencyProperty.Register(
        nameof(Target), typeof(string), typeof(HopChain), new PropertyMetadata("", (sender, _) => ((HopChain)sender).Refresh()));

    public IReadOnlyList<string>? Hops
    {
        get => (IReadOnlyList<string>?)GetValue(HopsProperty);
        set => SetValue(HopsProperty, value);
    }

    /// <summary>Optional last chip: the forwarding target.</summary>
    public string Target
    {
        get => (string)GetValue(TargetProperty);
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
            Child = new TextBlock { Text = number, FontSize = 13, FontWeight = FontWeights.SemiBold },
        };
        numberBadge.SetResourceReference(Border.BackgroundProperty, "SelectionBrush");

        var chip = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 6, 12, 6),
            Margin = new Thickness(0, 0, 0, 8),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children = { numberBadge, new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center } },
            },
        };
        chip.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        return chip;
    }

    private static TextBlock Arrow()
    {
        var arrow = new TextBlock { Margin = new Thickness(12, 0, 12, 8), VerticalAlignment = VerticalAlignment.Center, FontSize = 14 };
        arrow.SetResourceReference(TextBlock.FontFamilyProperty, "IconFont");
        arrow.SetResourceReference(TextBlock.TextProperty, "IconArrowRight");
        arrow.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");
        return arrow;
    }
}
