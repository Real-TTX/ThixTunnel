using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace ThixTunnel.Controls;

/// <summary>Breadcrumb (optional), page title and subtitle at the top of every page.</summary>
public sealed class PageHeader : StackPanel
{
    public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<PageHeader, string>(nameof(Title), "");

    public static readonly StyledProperty<string> SubtitleProperty = AvaloniaProperty.Register<PageHeader, string>(nameof(Subtitle), "");

    public static readonly StyledProperty<string> ParentTitleProperty = AvaloniaProperty.Register<PageHeader, string>(nameof(ParentTitle), "");

    private readonly StackPanel _breadcrumb = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
    private readonly Button _parentLink = new() { FontSize = 14 };
    private readonly TextBlock _currentCrumb = new();
    private readonly TextBlock _title = new();
    private readonly TextBlock _subtitle = new() { Margin = new Thickness(0, 4, 0, 0), FontSize = 15, TextWrapping = TextWrapping.Wrap };

    static PageHeader()
    {
        TitleProperty.Changed.AddClassHandler<PageHeader>((header, _) => header.Refresh());
        SubtitleProperty.Changed.AddClassHandler<PageHeader>((header, _) => header.Refresh());
        ParentTitleProperty.Changed.AddClassHandler<PageHeader>((header, _) => header.Refresh());
    }

    public PageHeader()
    {
        Margin = new Thickness(0, 0, 0, 20);
        _parentLink.Theme = Ui.Theme("LinkButton");
        _parentLink.Click += (_, _) => ParentClicked?.Invoke(this, EventArgs.Empty);
        _currentCrumb.Foreground = Ui.Brush("TextMutedBrush");
        _breadcrumb.Children.Add(_parentLink);
        _breadcrumb.Children.Add(new TextBlock { Text = "  /  ", Foreground = Ui.Brush("TextMutedBrush") });
        _breadcrumb.Children.Add(_currentCrumb);

        _title.Theme = Ui.Theme("PageTitle");
        _subtitle.Foreground = Ui.Brush("TextMutedBrush");
        Children.Add(_breadcrumb);
        Children.Add(_title);
        Children.Add(_subtitle);
        Refresh();
    }

    /// <summary>The user clicked the parent entry of the breadcrumb.</summary>
    public event EventHandler? ParentClicked;

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    /// <summary>Title of the page above; empty = no breadcrumb.</summary>
    public string ParentTitle
    {
        get => GetValue(ParentTitleProperty);
        set => SetValue(ParentTitleProperty, value);
    }

    /// <summary>The last entry of the breadcrumb (e.g. the name of the edited route).</summary>
    public string CurrentCrumb
    {
        get => _currentCrumb.Text ?? "";
        set => _currentCrumb.Text = value;
    }

    private void Refresh()
    {
        _title.Text = Title;
        _subtitle.Text = Subtitle;
        _subtitle.IsVisible = !string.IsNullOrEmpty(Subtitle);
        _parentLink.Content = ParentTitle;
        _breadcrumb.IsVisible = !string.IsNullOrEmpty(ParentTitle);
    }
}
