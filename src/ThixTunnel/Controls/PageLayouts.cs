using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using MatDesktop;
using ThixTunnel.Core.Models;

namespace ThixTunnel.Controls;

/// <summary>
/// The list page of an entity (owner's rule): the title, a line about it, a message, THE toolbar (search, filter, sort), the
/// list and the actions below it, left-aligned from positive to negative, delete with a gap. Without a title it sits in a
/// tab of the settings.
/// </summary>
public sealed class ListLayout : StackPanel
{
    public ListLayout(string? title, string? description, ListToolbar toolbar, EntityList list)
    {
        Toolbar = toolbar;
        List = list;
        if (title is not null)
        {
            Children.Add(new TextBlock { Text = title }.Themed("MatText.PageTitle"));
        }

        if (description is not null)
        {
            Children.Add(new TextBlock { Text = description, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Thickness(0, title is null ? 0 : 4, 0, 14) }.Themed("MatText.Muted"));
        }

        Children.Add(Message);
        Children.Add(toolbar);
        Children.Add(list);
        Children.Add(Actions);
    }

    public InfoBar Message { get; } = new();

    public ListToolbar Toolbar { get; }

    public EntityList List { get; }

    public ActionBar Actions { get; } = new() { Margin = new Thickness(0, 14, 0, 0) };

    /// <summary>Adds a button below the list; <paramref name="separated"/> puts the gap before it (delete).</summary>
    public Button AddAction(string icon, string text, Action click, string? theme = null, bool separated = false)
    {
        var button = Ui.Button(icon, text, theme, click);
        ActionBar.SetSeparated(button, separated);
        Actions.Children.Add(button);
        return button;
    }

    /// <summary>Runs an action of the list; a business error shows above the list.</summary>
    public bool Run(Action action)
    {
        Message.Hide();
        try
        {
            action();
            return true;
        }
        catch (BusinessException exception)
        {
            Message.Show(exception.Message, InfoSeverity.Error);
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Core.Infrastructure.Log.Error("List action failed", exception);
            Message.Show(exception.Message, InfoSeverity.Error);
            return false;
        }
    }
}

/// <summary>
/// The create / change page of an entity (owner's rule): the title, a message, tabs for its areas, and one row of buttons below,
/// positive to negative: Speichern, Zurück … (gap) … Löschen. Errors of the business layer appear at their field
/// (<see cref="ShowError"/>, its tab is chosen) or above the tabs.
/// </summary>
public sealed class EditLayout : StackPanel
{
    public EditLayout(string title, string? description = null)
    {
        TitleText = new TextBlock { Text = title }.Themed("MatText.PageTitle");
        Children.Add(TitleText);
        if (description is not null)
        {
            Children.Add(new TextBlock { Text = description, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) }.Themed("MatText.Muted"));
        }

        Message.Margin = new Thickness(0, 14, 0, 0);
        Children.Add(Message);
        Tabs.Margin = new Thickness(0, 12, 0, 0);
        Children.Add(Tabs);
        Actions.Margin = new Thickness(0, 18, 0, 0);
        Children.Add(Actions);
    }

    public TextBlock TitleText { get; }

    public InfoBar Message { get; } = new();

    public TabControl Tabs { get; } = new();

    public ActionBar Actions { get; } = new();

    public void AddTab(string header, Control content)
    {
        var tab = new TabItem { Header = header, Content = new StackPanel { Margin = new Thickness(0, 12, 0, 0), Children = { content } } };
        Avalonia.Automation.AutomationProperties.SetName(tab, header);
        Tabs.Items.Add(tab);
    }

    /// <summary>Adds a button to the row below; <paramref name="separated"/> puts the gap before it (delete).</summary>
    public Button AddAction(string icon, string text, Action click, string? theme = null, bool separated = false)
    {
        var button = Ui.Button(icon, text, theme, click);
        ActionBar.SetSeparated(button, separated);
        Actions.Children.Add(button);
        return button;
    }

    /// <summary>Runs a change against the business layer; true when it worked, else the error is shown at its field.</summary>
    public bool Run(Action action)
    {
        ClearErrors();
        try
        {
            action();
            return true;
        }
        catch (BusinessException exception)
        {
            ShowError(exception);
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Core.Infrastructure.Log.Error("Saving failed", exception);
            Message.Show($"Speichern nicht möglich: {exception.Message}", InfoSeverity.Error);
            return false;
        }
    }

    /// <summary>A business error at its field (its tab is chosen), else above the tabs.</summary>
    public void ShowError(BusinessException exception)
    {
        ClearErrors();
        var target = exception.Field is null
            ? null
            : Fields().FirstOrDefault(formField => string.Equals(FormErrors.GetField(formField), exception.Field, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            Message.Show(exception.Message, InfoSeverity.Error);
            return;
        }

        target.Error = exception.Message;
        Message.Show("Bitte die markierte Angabe prüfen.", InfoSeverity.Error);
        foreach (var tab in Tabs.Items.OfType<TabItem>())
        {
            if (tab.Content is Control content && content.GetLogicalDescendants().Contains(target))
            {
                Tabs.SelectedItem = tab;
            }
        }
    }

    public void ClearErrors()
    {
        foreach (var formField in Fields())
        {
            formField.Error = null;
        }

        Message.Hide();
    }

    /// <summary>The fields of every tab (also those not shown now: the logical tree has them).</summary>
    private IEnumerable<MatFormField> Fields() => this.GetLogicalDescendants().OfType<MatFormField>().Distinct();
}
