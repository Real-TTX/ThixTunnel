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
        FilterBox.Visibility = filters.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
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
