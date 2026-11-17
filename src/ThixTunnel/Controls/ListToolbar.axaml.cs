using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
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
    /// <summary>Below this width the filter and sort move to a second row.</summary>
    private const double NarrowWidth = 560;

    private readonly DispatcherTimer _searchDelay = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private SortDirection _direction = SortDirection.Ascending;
    private bool _isInitializing;
    private bool? _isNarrow;

    public ListToolbar()
    {
        InitializeComponent();
        _searchDelay.Tick += (_, _) =>
        {
            _searchDelay.Stop();
            RaiseQueryChanged();
        };
        SearchBox.TextChanged += (_, _) =>
        {
            _searchDelay.Stop();
            _searchDelay.Start();
        };
        FilterBox.SelectionChanged += (_, _) => RaiseQueryChanged();
        SortBox.SelectionChanged += (_, _) => RaiseQueryChanged();
        DirectionButton.Click += (_, _) => ToggleDirection();
        SizeChanged += (_, args) => ApplyWidth(args.NewSize.Width);
    }

    /// <summary>Raised whenever search text, filter or sort changes.</summary>
    public event EventHandler<ListQuery>? QueryChanged;

    public string SearchPlaceholder
    {
        get => SearchBox.Watermark ?? "";
        set => SearchBox.Watermark = value;
    }

    public ListQuery Query => new(
        SearchBox.Text ?? "",
        (FilterBox.SelectedItem as ListOption)?.Value ?? "",
        (SortBox.SelectedItem as ListOption)?.Value ?? "",
        _direction);

    /// <summary>Sets the choices; the first one is selected.</summary>
    public void Configure(IReadOnlyList<ListOption> filters, IReadOnlyList<ListOption> sortFields)
    {
        _isInitializing = true;
        FilterBox.ItemsSource = filters;
        FilterBox.SelectedIndex = filters.Count > 0 ? 0 : -1;
        FilterArea.IsVisible = filters.Count > 0;
        SortBox.ItemsSource = sortFields;
        SortBox.SelectedIndex = sortFields.Count > 0 ? 0 : -1;
        _isInitializing = false;
    }

    private void ApplyWidth(double width)
    {
        var isNarrow = width < NarrowWidth;
        if (_isNarrow == isNarrow)
        {
            return;
        }

        _isNarrow = isNarrow;
        var columns = Layout.ColumnDefinitions;
        if (isNarrow)
        {
            columns[1].Width = new GridLength(1, GridUnitType.Star);
            columns[2].Width = GridLength.Auto;
            columns[3].Width = new GridLength(0);
            Place(SearchBox, row: 0, column: 0, span: 3, new Thickness(0));
            Place(FilterArea, row: 1, column: 0, span: 1, new Thickness(0, 8, 0, 0));
            Place(SortArea, row: 1, column: 1, span: 1, new Thickness(8, 8, 0, 0));
            Place(DirectionButton, row: 1, column: 2, span: 1, new Thickness(6, 8, 0, 0));
            FilterBox.MinWidth = SortBox.MinWidth = 0;
            return;
        }

        columns[1].Width = columns[2].Width = columns[3].Width = GridLength.Auto;
        Place(SearchBox, row: 0, column: 0, span: 1, new Thickness(0));
        Place(FilterArea, row: 0, column: 1, span: 1, new Thickness(12, 0, 0, 0));
        Place(SortArea, row: 0, column: 2, span: 1, new Thickness(12, 0, 0, 0));
        Place(DirectionButton, row: 0, column: 3, span: 1, new Thickness(6, 0, 0, 0));
        FilterBox.MinWidth = 150;
        SortBox.MinWidth = 140;
    }

    private static void Place(Control element, int row, int column, int span, Thickness margin)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        Grid.SetColumnSpan(element, span);
        element.Margin = margin;
    }

    private void ToggleDirection()
    {
        _direction = _direction == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending;
        DirectionButton.Content = Ui.Icon(_direction == SortDirection.Ascending ? "IconUp" : "IconDown");
        ToolTip.SetTip(DirectionButton, _direction == SortDirection.Ascending ? "Aufsteigend" : "Absteigend");
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
