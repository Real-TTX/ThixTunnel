using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using MatDesktop;

namespace ThixTunnel.Controls;

/// <summary>
/// The errors of the business layer at their field. A form field is MatDesktop's <see cref="MatFormField"/> (label, hint,
/// error); <c>controls:FormErrors.Field="ServerUrl"</c> names it the way <c>BusinessException.Field</c> does, and
/// <see cref="ShowError"/> finds the right field on a page by that name.
/// </summary>
public sealed class FormErrors
{
    public static readonly AttachedProperty<string?> FieldProperty =
        AvaloniaProperty.RegisterAttached<FormErrors, MatFormField, string?>("Field");

    private FormErrors()
    {
    }

    public static string? GetField(MatFormField element) => element.GetValue(FieldProperty);

    public static void SetField(MatFormField element, string? value) => element.SetValue(FieldProperty, value);

    /// <summary>Clears every error inside <paramref name="root"/>, then shows the message at the field it names. False when no field matched.</summary>
    public static bool ShowError(Control root, string? field, string message)
    {
        var fields = root.GetVisualDescendants().OfType<MatFormField>().ToList();
        foreach (var formField in fields)
        {
            formField.Error = null;
        }

        var target = field is null ? null : fields.FirstOrDefault(formField => string.Equals(GetField(formField), field, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            return false;
        }

        target.Error = message;
        return true;
    }
}
