using System.Windows.Controls;
using ThixTunnel.Core.Business;
using ThixTunnel.Pages;

namespace ThixTunnel;

/// <summary>
/// Opens the separate windows next to the connection list. Every content opens once: asking again brings
/// the open window to the front. UI only - the data goes through the business layer in the pages.
/// </summary>
internal static class AppWindows
{
    private static readonly Dictionary<string, PageWindow> OpenWindows = [];

    /// <summary>Routes or profiles were saved or deleted; open lists and editors refresh.</summary>
    public static event EventHandler? DataChanged;

    public static void NotifyDataChanged() => DataChanged?.Invoke(null, EventArgs.Empty);

    public static void ShowRouteEditor(Guid? routeId)
    {
        var route = routeId is null ? null : RouteService.Get(routeId.Value);
        Show($"route:{routeId?.ToString() ?? "new"}", route?.Name ?? "Neue Tunnelroute", 640, 660, () => new TunnelEditPage(routeId));
    }


    public static void ShowProfileEditor(Guid? profileId)
    {
        var profile = profileId is null ? null : ProfileService.Get(profileId.Value);
        Show($"profile:{profileId?.ToString() ?? "new"}", profile?.Name ?? "Neues SSH-Profil", 600, 640, () => new ProfileEditPage(profileId));
    }

    public static void ShowSettings(int tabIndex = 0)
    {
        if (OpenWindows.TryGetValue("settings", out var open) && open.Content is SettingsPage page)
        {
            page.SelectTab(tabIndex);
        }

        Show("settings", "Einstellungen", 860, 680, () => new SettingsPage(tabIndex));
    }

    public static void ShowAbout() => Show("about", "Über", 560, 540, () => new AboutPage());

    /// <summary>Closes every separate window (end of the application).</summary>
    public static void CloseAll()
    {
        foreach (var window in OpenWindows.Values.ToList())
        {
            window.Close();
        }
    }

    private static void Show(string key, string title, double width, double height, Func<UserControl> createPage)
    {
        if (OpenWindows.TryGetValue(key, out var open))
        {
            open.BringToFront();
            return;
        }

        var window = new PageWindow(key, title, createPage(), width, height);
        window.Closed += (_, _) => OpenWindows.Remove(key);
        OpenWindows[key] = window;
        window.Show();
        window.Activate();
    }
}
