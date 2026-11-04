using System.Windows;
using System.Windows.Controls;

namespace ThixTunnel.Controls;

/// <summary>
/// The row of buttons below a list or form: left-aligned, in one line, from positive to negative.
/// A button with <c>ActionBar.Separated="True"</c> (delete) gets a clear gap before it.
/// </summary>
/// <example>
/// &lt;controls:ActionBar&gt;
///   &lt;Button Content="Speichern" /&gt; &lt;Button Content="Zurück" /&gt;
///   &lt;Button Content="Löschen" controls:ActionBar.Separated="True" /&gt;
/// &lt;/controls:ActionBar&gt;
/// </example>
public sealed class ActionBar : Panel
{
    public static readonly DependencyProperty SeparatedProperty = DependencyProperty.RegisterAttached(
        "Separated", typeof(bool), typeof(ActionBar),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsParentArrange | FrameworkPropertyMetadataOptions.AffectsParentMeasure));

    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing), typeof(double), typeof(ActionBar), new FrameworkPropertyMetadata(8.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty SeparationProperty = DependencyProperty.Register(
        nameof(Separation), typeof(double), typeof(ActionBar), new FrameworkPropertyMetadata(40.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    public double Separation
    {
        get => (double)GetValue(SeparationProperty);
        set => SetValue(SeparationProperty, value);
    }

    public static bool GetSeparated(UIElement element) => (bool)element.GetValue(SeparatedProperty);

    public static void SetSeparated(UIElement element, bool value) => element.SetValue(SeparatedProperty, value);

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = 0;
        double height = 0;
        foreach (var child in VisibleChildren())
        {
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            width += GapBefore(child, width) + child.DesiredSize.Width;
            height = Math.Max(height, child.DesiredSize.Height);
        }

        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = 0;
        foreach (var child in VisibleChildren())
        {
            x += GapBefore(child, x);
            child.Arrange(new Rect(x, 0, child.DesiredSize.Width, finalSize.Height));
            x += child.DesiredSize.Width;
        }

        return finalSize;
    }

    private IEnumerable<UIElement> VisibleChildren() => InternalChildren.Cast<UIElement>().Where(child => child.Visibility != Visibility.Collapsed);

    private double GapBefore(UIElement child, double position) =>
        position == 0 ? 0 : GetSeparated(child) ? Separation : Spacing;
}
