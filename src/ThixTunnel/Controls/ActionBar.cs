using Avalonia;
using Avalonia.Controls;

namespace ThixTunnel.Controls;

/// <summary>
/// The row of buttons below a list or form: left-aligned, in one line, from positive to negative.
/// A button with <c>controls:ActionBar.Separated="True"</c> (delete) gets a clear gap before it.
/// In a narrow window the buttons continue on the next line (order unchanged).
/// </summary>
public sealed class ActionBar : Panel
{
    public static readonly AttachedProperty<bool> SeparatedProperty =
        AvaloniaProperty.RegisterAttached<ActionBar, Control, bool>("Separated");

    public static readonly StyledProperty<double> SpacingProperty = AvaloniaProperty.Register<ActionBar, double>(nameof(Spacing), 8);

    public static readonly StyledProperty<double> SeparationProperty = AvaloniaProperty.Register<ActionBar, double>(nameof(Separation), 40);

    static ActionBar()
    {
        AffectsParentMeasure<ActionBar>(SeparatedProperty);
        AffectsMeasure<ActionBar>(SpacingProperty, SeparationProperty);
    }

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    public double Separation
    {
        get => GetValue(SeparationProperty);
        set => SetValue(SeparationProperty, value);
    }

    public static bool GetSeparated(Control element) => element.GetValue(SeparatedProperty);

    public static void SetSeparated(Control element, bool value) => element.SetValue(SeparatedProperty, value);

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in VisibleChildren())
        {
            child.Measure(Size.Infinity);
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

    private IEnumerable<Control> VisibleChildren() => Children.Where(child => child.IsVisible);

    private sealed class Line
    {
        public List<(Control Child, double X)> Items { get; } = [];

        public double Width { get; set; }

        public double Height { get; set; }
    }
}
