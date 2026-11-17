using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;

namespace ThixTunnel.Controls;

/// <summary>
/// One input of a form: label above, the input, an optional hint and an error below (look: Themes/Controls.axaml).
/// <see cref="Field"/> names the property the business layer reports errors for (BusinessException.Field).
/// </summary>
public sealed class FormField : HeaderedContentControl
{
    public static readonly StyledProperty<string> HintProperty = AvaloniaProperty.Register<FormField, string>(nameof(Hint), "");

    public static readonly StyledProperty<string> ErrorProperty = AvaloniaProperty.Register<FormField, string>(nameof(Error), "");

    public static readonly StyledProperty<string> FieldProperty = AvaloniaProperty.Register<FormField, string>(nameof(Field), "");

    public string Hint
    {
        get => GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    public string Error
    {
        get => GetValue(ErrorProperty);
        set => SetValue(ErrorProperty, value);
    }

    public string Field
    {
        get => GetValue(FieldProperty);
        set => SetValue(FieldProperty, value);
    }

    /// <summary>Shows the message at the field named <paramref name="field"/> and clears all others; returns false when no field matches.</summary>
    public static bool ShowError(ILogical root, string? field, string message)
    {
        var found = false;
        foreach (var formField in root.GetLogicalDescendants().OfType<FormField>())
        {
            var matches = !string.IsNullOrEmpty(field) && formField.Field == field;
            formField.Error = matches ? message : "";
            found |= matches;
        }

        return found;
    }

    public static void ClearErrors(ILogical root)
    {
        foreach (var formField in root.GetLogicalDescendants().OfType<FormField>())
        {
            formField.Error = "";
        }
    }
}
