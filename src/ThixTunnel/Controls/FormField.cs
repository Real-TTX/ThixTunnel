using System.Windows;
using System.Windows.Controls;

namespace ThixTunnel.Controls;

/// <summary>
/// One input of a form: label above, the input, an optional hint and an error below.
/// <see cref="Field"/> names the property the business layer reports errors for (BusinessException.Field).
/// </summary>
public sealed class FormField : HeaderedContentControl
{
    public static readonly DependencyProperty HintProperty = DependencyProperty.Register(
        nameof(Hint), typeof(string), typeof(FormField), new PropertyMetadata(""));

    public static readonly DependencyProperty ErrorProperty = DependencyProperty.Register(
        nameof(Error), typeof(string), typeof(FormField), new PropertyMetadata(""));

    public static readonly DependencyProperty FieldProperty = DependencyProperty.Register(
        nameof(Field), typeof(string), typeof(FormField), new PropertyMetadata(""));

    public string Hint
    {
        get => (string)GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    public string Error
    {
        get => (string)GetValue(ErrorProperty);
        set => SetValue(ErrorProperty, value);
    }

    public string Field
    {
        get => (string)GetValue(FieldProperty);
        set => SetValue(FieldProperty, value);
    }

    /// <summary>Shows the message at the field named <paramref name="field"/> and clears all others; returns false when no field matches.</summary>
    public static bool ShowError(DependencyObject root, string? field, string message)
    {
        var found = false;
        foreach (var formField in FindAll(root))
        {
            var matches = !string.IsNullOrEmpty(field) && formField.Field == field;
            formField.Error = matches ? message : "";
            found |= matches;
        }

        return found;
    }

    public static void ClearErrors(DependencyObject root)
    {
        foreach (var formField in FindAll(root))
        {
            formField.Error = "";
        }
    }

    private static IEnumerable<FormField> FindAll(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (child is FormField field)
            {
                yield return field;
            }

            foreach (var nested in FindAll(child))
            {
                yield return nested;
            }
        }
    }
}
