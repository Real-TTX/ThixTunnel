using ThixTunnel.Core.Models;

namespace ThixTunnel.Core.Business;

/// <summary>Shared checks for user input. Values end up in an ssh configuration, so they must not contain anything that changes its meaning.</summary>
internal static class InputRules
{
    public static string RequireText(string? value, string field, string label, int maximumLength = 100)
    {
        var text = (value ?? "").Trim();
        if (text.Length == 0)
        {
            throw new BusinessException($"{label} ist erforderlich.", field);
        }

        if (text.Length > maximumLength)
        {
            throw new BusinessException($"{label} darf höchstens {maximumLength} Zeichen lang sein.", field);
        }

        return text;
    }

    /// <summary>A host name, IP address, or user name: no spaces, quotes, or a leading dash (it would become an ssh option).</summary>
    public static string RequireToken(string? value, string field, string label)
    {
        var text = RequireText(value, field, label, 253);
        if (text.StartsWith('-') || text.Any(character => char.IsWhiteSpace(character) || character is '"' or '\'' or '#' or ',' or '%'))
        {
            throw new BusinessException($"{label} enthält ungültige Zeichen.", field);
        }

        return text;
    }

    public static int RequirePort(int value, string field, string label)
    {
        if (value is < 1 or > 65535)
        {
            throw new BusinessException($"{label} muss zwischen 1 und 65535 liegen.", field);
        }

        return value;
    }

    public static bool Matches(string searchText, params string?[] values) =>
        string.IsNullOrWhiteSpace(searchText) ||
        values.Any(value => value?.Contains(searchText.Trim(), StringComparison.CurrentCultureIgnoreCase) == true);
}
