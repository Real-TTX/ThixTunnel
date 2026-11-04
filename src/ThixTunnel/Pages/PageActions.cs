using System.Windows;
using ThixTunnel.Controls;
using ThixTunnel.Core.Infrastructure;
using ThixTunnel.Core.Models;

namespace ThixTunnel.Pages;

/// <summary>Runs a user action against the business layer and shows its error at the right place.</summary>
internal static class PageActions
{
    /// <summary>Returns true when the action succeeded. A BusinessException goes to its form field, or to the info bar.</summary>
    public static bool Run(Action action, InfoBar messageBar, DependencyObject? form = null)
    {
        if (form is not null)
        {
            FormField.ClearErrors(form);
        }

        messageBar.Clear();
        try
        {
            action();
            return true;
        }
        catch (BusinessException exception)
        {
            var shownAtField = form is not null && FormField.ShowError(form, exception.Field, exception.Message);
            messageBar.Show(shownAtField ? "Bitte die markierten Angaben prüfen." : exception.Message, InfoSeverity.Error);
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.Error("Action failed", exception);
            messageBar.Show($"Speichern nicht möglich: {exception.Message}", InfoSeverity.Error);
            return false;
        }
    }

    public static int ParsePort(string text) => int.TryParse(text.Trim(), out var port) ? port : 0;
}
