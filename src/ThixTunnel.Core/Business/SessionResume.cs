using System.Text.Json;
using ThixTunnel.Core.Infrastructure;

namespace ThixTunnel.Core.Business;

/// <summary>
/// An update restarts ThixTunnel. The routes that were active before are remembered here and connected again
/// after the restart (without prompts), so an automatic update does not silently drop manually started tunnels.
/// </summary>
public static class SessionResume
{
    private static string FilePath => Path.Combine(AppPaths.LocalDirectory, "resume-routes.json");

    public static void RememberActiveRoutes()
    {
        Directory.CreateDirectory(AppPaths.LocalDirectory);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(TunnelManager.GetActiveRouteIds()));
    }

    /// <summary>Connects the remembered routes once and forgets them.</summary>
    public static void RestoreRememberedRoutes()
    {
        if (!File.Exists(FilePath))
        {
            return;
        }

        List<Guid> routeIds;
        try
        {
            routeIds = JsonSerializer.Deserialize<List<Guid>>(File.ReadAllText(FilePath)) ?? [];
        }
        catch (JsonException)
        {
            routeIds = [];
        }

        File.Delete(FilePath);
        foreach (var routeId in routeIds.Where(id => RouteService.Get(id) is not null))
        {
            try
            {
                TunnelManager.ConnectAutomatically(routeId);
            }
            catch (Models.BusinessException exception)
            {
                Log.Warning($"Route {routeId} not restored after update: {exception.Message}");
            }
        }
    }
}
