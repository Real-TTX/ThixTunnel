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
        foreach (var child in VisibleChildren())
        {
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        }

        var lines = Layout(availableSize.Width);
        var width = lines.Count == 0 ? 0 : lines.Max(line => line.Width);
        return new Size(width, lines.Sum(line => line.Height) + Math.Max(0, lines.Count - 1) * Spacing);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double y = 0;
        foreach (var line in Layout(finalSize.Width))
        {
            foreach (var (child, x) in line.Items)
            {
                child.Arrange(new Rect(x, y, child.DesiredSize.Width, line.Height));
            }

            y += line.Height + Spacing;
        }

        return finalSize;
    }

    /// <summary>
    /// In one line while it fits; in a narrow window the buttons continue on the next line (order unchanged).
    /// </summary>
    private List<Line> Layout(double availableWidth)
    {
        var lines = new List<Line>();
        var current = new Line();
        foreach (var child in VisibleChildren())
        {
            var gap = current.Items.Count == 0 ? 0 : GetSeparated(child) ? Separation : Spacing;
            if (current.Items.Count > 0 && current.Width + gap + child.DesiredSize.Width > availableWidth)
            {
                lines.Add(current);
                current = new Line();
                gap = 0;
            }

            current.Items.Add((child, current.Width + gap));
            current.Width += gap + child.DesiredSize.Width;
            current.Height = Math.Max(current.Height, child.DesiredSize.Height);
        }

        if (current.Items.Count > 0)
        {
            lines.Add(current);
        }

        return lines;
    }

    private IEnumerable<UIElement> VisibleChildren() => InternalChildren.Cast<UIElement>().Where(child => child.Visibility != Visibility.Collapsed);

    private sealed class Line
    {
        public List<(UIElement Child, double X)> Items { get; } = [];

        public double Width { get; set; }

        public double Height { get; set; }
    }
}
