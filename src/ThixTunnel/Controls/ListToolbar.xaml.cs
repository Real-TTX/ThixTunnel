using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ThixTunnel.Core.Models;

namespace ThixTunnel.Controls;

/// <summary>A choice of a filter or sort combo box: the value for the business layer and the text for the user.</summary>
public sealed record ListOption(string Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Search, filter and sort above a list. Several toolbars may live on one page; each has its own state.</summary>
public partial class ListToolbar : UserControl
{
    private readonly DispatcherTimer _searchDelay = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private SortDirection _direction = SortDirection.Ascending;
    private bool _isInitializing;

    public ListToolbar()
    {
        InitializeComponent();
        _searchDelay.Tick += (_, _) =>
        {
            _searchDelay.Stop();
            RaiseQueryChanged();
        };
    }

    /// <summary>Raised whenever search text, filter or sort changes.</summary>
    public event EventHandler<ListQuery>? QueryChanged;

    public string SearchPlaceholder
    {
        get => Placeholder.Text;
        set => Placeholder.Text = value;
    }

    public ListQuery Query => new(
        SearchBox.Text,
        (FilterBox.SelectedItem as ListOption)?.Value ?? "",
        (SortBox.SelectedItem as ListOption)?.Value ?? "",
        _direction);

    /// <summary>Sets the choices; the first one is selected.</summary>
    public void Configure(IReadOnlyList<ListOption> filters, IReadOnlyList<ListOption> sortFields)
    {
        _isInitializing = true;
        FilterBox.ItemsSource = filters;
        FilterBox.SelectedIndex = filters.Count > 0 ? 0 : -1;
        FilterArea.Visibility = filters.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        SortBox.ItemsSource = sortFields;
        SortBox.SelectedIndex = sortFields.Count > 0 ? 0 : -1;
        _isInitializing = false;
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        Placeholder.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        _searchDelay.Stop();
        _searchDelay.Start();
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) => RaiseQueryChanged();

    /// <summary>Below this width the filter and sort move to a second row.</summary>
    private const double NarrowWidth = 560;

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var isNarrow = e.NewSize.Width < NarrowWidth;
        if (e.PreviousSize.Width > 0 && isNarrow == e.PreviousSize.Width < NarrowWidth)
        {
            return;
        }

        var columns = Layout.ColumnDefinitions;
        if (isNarrow)
        {
            columns[1].Width = new GridLength(1, GridUnitType.Star);
            columns[2].Width = GridLength.Auto;
            columns[3].Width = new GridLength(0);
            Place(SearchArea, row: 0, column: 0, span: 3, new Thickness(0));
            Place(FilterArea, row: 1, column: 0, span: 1, new Thickness(0, 8, 0, 0));
            Place(SortArea, row: 1, column: 1, span: 1, new Thickness(8, 8, 0, 0));
            Place(DirectionButton, row: 1, column: 2, span: 1, new Thickness(6, 8, 0, 0));
            FilterBox.MinWidth = SortBox.MinWidth = 0;
            return;
        }

        columns[1].Width = columns[2].Width = columns[3].Width = GridLength.Auto;
        Place(SearchArea, row: 0, column: 0, span: 1, new Thickness(0));
        Place(FilterArea, row: 0, column: 1, span: 1, new Thickness(12, 0, 0, 0));
        Place(SortArea, row: 0, column: 2, span: 1, new Thickness(12, 0, 0, 0));
        Place(DirectionButton, row: 0, column: 3, span: 1, new Thickness(6, 0, 0, 0));
        FilterBox.MinWidth = 150;
        SortBox.MinWidth = 140;
    }

    private static void Place(FrameworkElement element, int row, int column, int span, Thickness margin)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        Grid.SetColumnSpan(element, span);
        element.Margin = margin;
    }

    private void OnDirectionClick(object sender, RoutedEventArgs e)
    {
        _direction = _direction == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending;
        DirectionButton.Content = FindResource(_direction == SortDirection.Ascending ? "IconUp" : "IconDown");
        DirectionButton.ToolTip = _direction == SortDirection.Ascending ? "Aufsteigend" : "Absteigend";
        RaiseQueryChanged();
    }

    private void RaiseQueryChanged()
    {
        if (!_isInitializing)
        {
            QueryChanged?.Invoke(this, Query);
        }
    }
}
