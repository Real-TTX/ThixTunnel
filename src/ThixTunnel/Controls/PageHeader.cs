using System.Windows;
using System.Windows.Controls;

namespace ThixTunnel.Controls;

/// <summary>Breadcrumb (optional), page title and subtitle at the top of every page.</summary>
public sealed class PageHeader : StackPanel
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(PageHeader), new PropertyMetadata("", (sender, _) => ((PageHeader)sender).Refresh()));

    public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(
        nameof(Subtitle), typeof(string), typeof(PageHeader), new PropertyMetadata("", (sender, _) => ((PageHeader)sender).Refresh()));

    public static readonly DependencyProperty ParentTitleProperty = DependencyProperty.Register(
        nameof(ParentTitle), typeof(string), typeof(PageHeader), new PropertyMetadata("", (sender, _) => ((PageHeader)sender).Refresh()));

    private readonly StackPanel _breadcrumb = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
    private readonly Button _parentLink = new() { Padding = new Thickness(0) };
    private readonly TextBlock _currentCrumb = new();
    private readonly TextBlock _title = new();
    private readonly TextBlock _subtitle = new() { Margin = new Thickness(0, 4, 0, 0), FontSize = 15, TextWrapping = TextWrapping.Wrap };

    public PageHeader()
    {
        Margin = new Thickness(0, 0, 0, 20);
        _parentLink.SetResourceReference(StyleProperty, "LinkButton");
        _parentLink.FontSize = 14;
        _parentLink.Click += (_, _) => ParentClicked?.Invoke(this, EventArgs.Empty);
        _currentCrumb.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");
        var separator = new TextBlock { Text = "  /  " };
        separator.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");
        _breadcrumb.Children.Add(_parentLink);
        _breadcrumb.Children.Add(separator);
        _breadcrumb.Children.Add(_currentCrumb);

        _title.SetResourceReference(StyleProperty, "PageTitle");
        _subtitle.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");
        Children.Add(_breadcrumb);
        Children.Add(_title);
        Children.Add(_subtitle);
        Refresh();
    }

    /// <summary>The user clicked the parent entry of the breadcrumb.</summary>
    public event EventHandler? ParentClicked;

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    /// <summary>Title of the page above; empty = no breadcrumb.</summary>
    public string ParentTitle
    {
        get => (string)GetValue(ParentTitleProperty);
        set => SetValue(ParentTitleProperty, value);
    }

    /// <summary>The last entry of the breadcrumb (e.g. the name of the edited route).</summary>
    public string CurrentCrumb
    {
        get => _currentCrumb.Text;
        set => _currentCrumb.Text = value;
    }

    private void Refresh()
    {
        _title.Text = Title;
        _subtitle.Text = Subtitle;
        _subtitle.Visibility = string.IsNullOrEmpty(Subtitle) ? Visibility.Collapsed : Visibility.Visible;
        _parentLink.Content = ParentTitle;
        _breadcrumb.Visibility = string.IsNullOrEmpty(ParentTitle) ? Visibility.Collapsed : Visibility.Visible;
    }
}
