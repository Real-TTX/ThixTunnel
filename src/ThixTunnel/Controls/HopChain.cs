using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MatDesktop;

namespace ThixTunnel.Controls;

/// <summary>
/// The SSH hops of a connection as numbered chips with arrows: [1 Gateway] → [2 Datenbankserver] → [Ziel 127.0.0.1:5432].
/// Colours from the tokens; several chains on one page work (no shared state).
/// </summary>
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

        if (string.IsNullOrEmpty(Target))
        {
            return;
        }

        if (hops.Count > 0)
        {
            Children.Add(Arrow());
        }

        Children.Add(Chip("Ziel", Target));
    }

    private static Border Chip(string number, string text)
    {
        var numberBadge = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(7, 1, 7, 2),
            Margin = new Thickness(0, 0, 8, 0),
            Child = new TextBlock { Text = number, FontWeight = FontWeight.SemiBold }.Use(TextBlock.ForegroundProperty, "Mat.AccentText"),
        }.Use(Border.BackgroundProperty, "Mat.AccentSoft");

        return new Border
        {
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 5, 12, 5),
            Margin = new Thickness(0, 0, 0, 8),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children = { numberBadge, new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center } },
            },
        }.Use(Border.BorderBrushProperty, "Mat.Border").Use(Border.CornerRadiusProperty, "Mat.Radius").Use(Border.BackgroundProperty, "Mat.Surface2");
    }

    private static MatIcon Arrow() =>
        new MatIcon { Icon = "arrow-right", Size = 16, Margin = new Thickness(10, 0, 10, 8), VerticalAlignment = VerticalAlignment.Center }
            .Use(MatIcon.ForegroundProperty, "Mat.TextMuted");
}
