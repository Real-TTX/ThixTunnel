using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using MatDesktop;
using ThixTunnel.Core.Models;

namespace ThixTunnel.Controls;

/// <summary>A choice of a filter or sort box: the value for the business layer and the text for the user.</summary>
public sealed record ListOption(string Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// The toolbar above every list (owner's rule): a search text, a filter, a sorting and its direction, in one row. The list
/// page reads <see cref="Query"/> (the business layer filters and sorts) and draws again on <see cref="Changed"/>.
/// Searching runs on every key (the lists are small and in memory). Several toolbars on one page work: no state outside
/// the instance.
/// </summary>
public sealed class ListToolbar : Grid
{
    private readonly TextBox _search;
    private readonly ComboBox _filter;
    private readonly ComboBox _sort;
    private readonly Button _direction;
    private SortDirection _sortDirection = SortDirection.Ascending;

    public ListToolbar(string searchHint, IReadOnlyList<ListOption> filters, IReadOnlyList<ListOption> sortings)
    {
        ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto");
        Margin = new Thickness(0, 0, 0, 12);
        _search = new TextBox { Watermark = searchHint, HorizontalAlignment = HorizontalAlignment.Stretch }.Themed("MatSearchBox");
        AutomationProperties.SetName(_search, searchHint);
        _filter = new ComboBox { ItemsSource = filters, SelectedIndex = 0, MinWidth = 160, Margin = new Thickness(10, 0, 0, 0), IsVisible = filters.Count > 1 };
        AutomationProperties.SetName(_filter, "Filter");
        ToolTip.SetTip(_filter, "Filter");
        _sort = new ComboBox { ItemsSource = sortings, SelectedIndex = 0, MinWidth = 150, Margin = new Thickness(10, 0, 0, 0), IsVisible = sortings.Count > 1 };
        AutomationProperties.SetName(_sort, "Sortierung");
        ToolTip.SetTip(_sort, "Sortierung");
        _direction = new Button { Margin = new Thickness(6, 0, 0, 0), IsVisible = sortings.Count > 1, VerticalAlignment = VerticalAlignment.Center }.Themed("MatButton.Icon");
        _direction.Click += (_, _) =>
        {
            _sortDirection = _sortDirection == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending;
            RefreshDirection();
            Changed?.Invoke(this, EventArgs.Empty);
        };
        RefreshDirection();

        // the search fills its column up to 420 px, left-aligned (a MaxWidth on the box itself would centre it)
        var searchCell = new Grid { ColumnDefinitions = { new ColumnDefinition(1, GridUnitType.Star) { MaxWidth = 420 }, new ColumnDefinition(0, GridUnitType.Star) } };
        searchCell.Children.Add(_search);
        Children.Add(searchCell);
        SetColumn(_filter, 1);
        SetColumn(_sort, 2);
        SetColumn(_direction, 3);
        Children.Add(_filter);
        Children.Add(_sort);
        Children.Add(_direction);

        _search.TextChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        _filter.SelectionChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        _sort.SelectionChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Search, filter, sorting or its direction changed.</summary>
    public event EventHandler? Changed;

    /// <summary>What the business layer needs to filter and sort the list.</summary>
    public ListQuery Query => new(
        _search.Text?.Trim() ?? "",
        (_filter.SelectedItem as ListOption)?.Value ?? "",
        (_sort.SelectedItem as ListOption)?.Value ?? "",
        _sortDirection);

    public TextBox SearchBox => _search;

    public ComboBox FilterBox => _filter;

    public ComboBox SortBox => _sort;

    private void RefreshDirection()
    {
        var ascending = _sortDirection == SortDirection.Ascending;
        _direction.Content = new MatIcon { Icon = ascending ? "arrow-up" : "arrow-down", Size = 16 };
        var name = ascending ? "Aufsteigend sortiert" : "Absteigend sortiert";
        ToolTip.SetTip(_direction, name);
        AutomationProperties.SetName(_direction, name);
    }
}
