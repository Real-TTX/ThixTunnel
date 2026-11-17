using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using MatDesktop;

namespace ThixTunnel.Controls;

/// <summary>One row of an <see cref="EntityList"/>: what it shows; <see cref="Id"/> comes back when it is opened.</summary>
public sealed record EntityRow(Guid Id, string Icon, string Title, string Subtitle)
{
    /// <summary>Small labels after the title ("Auto-Verbindung", "Passwort").</summary>
    public IReadOnlyList<string> Badges { get; init; } = [];

    /// <summary>The state colour of the icon tile ("Mat.Success", "Mat.Warning", "Mat.Danger", "Neutral"); null = the accent.</summary>
    public string? StateToken { get; init; }

    /// <summary>The state in a word ("Verbunden", "Fehler"), as a badge in the colour of <see cref="StateToken"/>: the colour never stands alone.</summary>
    public string? StateWord { get; init; }

    /// <summary>The colour of the subtitle (an error in red); null = muted.</summary>
    public string? SubtitleToken { get; init; }

    /// <summary>A third line (the hops of a connection).</summary>
    public string? Detail { get; init; }

    /// <summary>Something at the right of the row (a button); it does not open the row.</summary>
    public Func<Control>? Trailing { get; init; }

    /// <summary>The tool tip of the row (everything about it).</summary>
    public string? ToolTip { get; init; }
}

/// <summary>
/// The list of a list page (connections, profiles): rows as cards in MatDesktop's list (MatList + MatCardRow, the chosen
/// row a tint with the accent line, COLOR-08), the number of entries below, a text when nothing is there. Enter, a double
/// click or the chevron open a row (<see cref="Opened"/>). One code base for every list; several on a page work.
/// </summary>
public sealed class EntityList : StackPanel
{
    private readonly ListBox _list;
    private readonly TextBlock _empty;
    private readonly TextBlock _count;
    private readonly bool _showOpenButton;
    private readonly string _openName;
    private List<Control> _items = [];

    /// <param name="emptyText">Shown when no row is there.</param>
    /// <param name="showOpenButton">A chevron at the right of every row (lists whose rows open an edit page).</param>
    /// <param name="openName">What opening does ("bearbeiten"): the name of the chevron for screen readers.</param>
    public EntityList(string emptyText, bool showOpenButton = true, string openName = "öffnen")
    {
        Spacing = 8;
        _showOpenButton = showOpenButton;
        _openName = openName;
        _list = new ListBox { SelectionMode = SelectionMode.Single }.Themed("MatList");
        _list.Use(ItemsControl.ItemContainerThemeProperty, "MatCardRow");
        _empty = new TextBlock { Text = emptyText, Margin = new Thickness(4, 16), TextWrapping = TextWrapping.Wrap }.Themed("MatText.Muted");
        _count = new TextBlock { Margin = new Thickness(4, 0, 0, 0) }.Themed("MatText.Faint");
        Children.Add(_list);
        Children.Add(_empty);
        Children.Add(_count);
        _list.DoubleTapped += (_, e) =>
        {
            if (e.Source is Visual source && IsInsideButton(source))
            {
                return;
            }

            OpenSelected();
        };
        _list.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                OpenSelected();
            }
        };
        _list.SelectionChanged += (_, _) => SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A row was opened (its id).</summary>
    public event EventHandler<Guid>? Opened;

    /// <summary>Another row was chosen.</summary>
    public event EventHandler? SelectionChanged;

    public ListBox List => _list;

    /// <summary>The text when nothing is there (it may change with the filter).</summary>
    public string EmptyText
    {
        get => _empty.Text ?? "";
        set => _empty.Text = value;
    }

    public Guid? SelectedId => (_list.SelectedItem as Control)?.Tag as Guid?;

    /// <summary>The titles of the rows shown (tests).</summary>
    public IReadOnlyList<string> Titles => _items.Select(item => AutomationProperties.GetName(item) ?? "").ToList();

    /// <summary>Shows the rows; the selection stays on the same id when it is still there (else the first row).</summary>
    public void Show(IReadOnlyList<EntityRow> rows, int total)
    {
        var selected = SelectedId;
        _items = rows.Select(Build).ToList();
        _list.ItemsSource = _items;
        _empty.IsVisible = rows.Count == 0;
        _list.IsVisible = rows.Count > 0;
        _count.IsVisible = total > 0;
        _count.Text = rows.Count == total ? $"{total} {(total == 1 ? "Eintrag" : "Einträge")}" : $"{rows.Count} von {total} Einträgen";
        var index = selected is null ? -1 : rows.ToList().FindIndex(row => row.Id == selected);
        _list.SelectedIndex = index >= 0 ? index : rows.Count > 0 ? 0 : -1;
    }

    /// <summary>Chooses the row of an id and scrolls it into view.</summary>
    public void Select(Guid id)
    {
        var item = _items.FirstOrDefault(candidate => candidate.Tag is Guid tag && tag == id);
        if (item is not null)
        {
            _list.SelectedItem = item;
            _list.ScrollIntoView(item);
        }
    }

    private static bool IsInsideButton(Visual source)
    {
        for (Visual? visual = source; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is Button)
            {
                return true;
            }

            if (visual is ListBoxItem)
            {
                return false;
            }
        }

        return false;
    }

    private void OpenSelected()
    {
        if (SelectedId is { } id)
        {
            Opened?.Invoke(this, id);
        }
    }

    private Control Build(EntityRow row)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), Tag = row.Id, MinHeight = 46, Background = Brushes.Transparent };
        var (tint, glyph) = row.StateToken switch
        {
            null => ("Mat.AccentSoft", "Mat.AccentText"),
            "Neutral" => ("Mat.Surface3", "Mat.TextMuted"),
            var state => ($"{state}Bg", $"{state}Text"),
        };
        var leading = new Border
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(9),
            Margin = new Thickness(0, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new MatIcon { Icon = row.Icon, Size = 18, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }.Use(MatIcon.ForegroundProperty, glyph),
        }.Use(Border.BackgroundProperty, tint);

        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        title.Children.Add(new TextBlock { Text = row.Title, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
        if (row.StateWord is not null)
        {
            var word = new TextBlock { Text = row.StateWord }.Use(TextBlock.ForegroundProperty, glyph);
            title.Children.Add(new Border { Child = word, VerticalAlignment = VerticalAlignment.Center }.Themed("MatBadge.Soft").Use(Border.BackgroundProperty, tint));
        }
        foreach (var badge in row.Badges)
        {
            title.Children.Add(new Border { Child = new TextBlock { Text = badge }, VerticalAlignment = VerticalAlignment.Center }.Themed("MatBadge.Soft"));
        }

        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(title);
        if (row.Subtitle.Length > 0)
        {
            var subtitle = new TextBlock { Text = row.Subtitle, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1 }.Themed("MatText.Muted");
            if (row.SubtitleToken is not null)
            {
                subtitle.Use(TextBlock.ForegroundProperty, row.SubtitleToken);
            }

            text.Children.Add(subtitle);
        }

        if (!string.IsNullOrEmpty(row.Detail))
        {
            text.Children.Add(new TextBlock { Text = row.Detail, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1 }.Themed("MatText.Faint"));
        }

        Grid.SetColumn(text, 1);
        grid.Children.Add(leading);
        grid.Children.Add(text);
        if (row.Trailing?.Invoke() is { } trailing)
        {
            trailing.Margin = new Thickness(12, 0, 0, 0);
            trailing.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(trailing, 2);
            grid.Children.Add(trailing);
        }

        if (_showOpenButton)
        {
            var open = new Button { Content = new MatIcon { Icon = "chevron-right", Size = 16 }, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }.Themed("MatButton.Icon");
            AutomationProperties.SetName(open, $"{row.Title} {_openName}");
            Avalonia.Controls.ToolTip.SetTip(open, char.ToUpperInvariant(_openName[0]) + _openName[1..]);
            open.Click += (_, _) => Opened?.Invoke(this, row.Id);
            Grid.SetColumn(open, 3);
            grid.Children.Add(open);
        }

        if (row.ToolTip is not null)
        {
            Avalonia.Controls.ToolTip.SetTip(text, row.ToolTip);
            Avalonia.Controls.ToolTip.SetShowDelay(text, 600);
        }

        AutomationProperties.SetName(grid, row.Title);
        return grid;
    }
}
