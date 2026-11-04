using ThixTunnel.Core.Infrastructure;
using ThixTunnel.Core.Models;

namespace ThixTunnel.Core.Business;

/// <summary>Tunnel routes: list, read, save, delete.</summary>
public static class RouteService
{
    public const string FilterAll = "";
    public const string FilterActive = "active";
    public const string FilterInactive = "inactive";
    public const string FilterProblem = "problem";
    public const string FilterAutoConnect = "auto";

    public const string SortByName = "name";
    public const string SortByLocalPort = "port";
    public const string SortByStatus = "status";

    public static IReadOnlyList<TunnelRoute> List(ListQuery? query = null)
    {
        query ??= new ListQuery();
        var data = DataStore.Read();
        var profileNames = data.Profiles.ToDictionary(profile => profile.Id, profile => profile.Name);

        var routes = data.Routes
            .Where(route => InputRules.Matches(query.SearchText,
                [route.Name, route.LocalEndpoint, route.TargetEndpoint, .. route.HopProfileIds.Select(id => profileNames.GetValueOrDefault(id))]))
            .Where(route => query.Filter switch
            {
                FilterActive => TunnelManager.GetStatus(route.Id).IsActive,
                FilterInactive => !TunnelManager.GetStatus(route.Id).IsActive,
                FilterProblem => TunnelManager.GetStatus(route.Id).Problem != TunnelProblem.None,
                FilterAutoConnect => route.ConnectOnStartup,
                _ => true,
            });

        Func<TunnelRoute, object> sortKey = query.SortBy switch
        {
            SortByLocalPort => route => route.LocalPort,
            SortByStatus => route => -(int)TunnelManager.GetStatus(route.Id).State,
            _ => route => route.Name,
        };

        var sorted = query.Direction == SortDirection.Descending
            ? routes.OrderByDescending(sortKey)
            : routes.OrderBy(sortKey);
        return sorted.ThenBy(route => route.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public static TunnelRoute? Get(Guid id) => DataStore.Read().Routes.FirstOrDefault(route => route.Id == id);

    /// <summary>The profiles of the route in connection order; missing profiles are left out.</summary>
    public static IReadOnlyList<SshProfile> GetHops(TunnelRoute route)
    {
        var profiles = DataStore.Read().Profiles.ToDictionary(profile => profile.Id);
        return route.HopProfileIds.Where(profiles.ContainsKey).Select(id => profiles[id]).ToList();
    }

    /// <summary>The equivalent ssh command line, for the details view.</summary>
    public static string DescribeCommand(TunnelRoute route) => SshCommandBuilder.DescribeCommand(route, GetHops(route));

    /// <summary>Validates and stores a new or changed route; returns the stored version.</summary>
    public static TunnelRoute Save(TunnelRoute input)
    {
        AccessPolicy.EnsureConfigurationEditable();

        var route = Normalize(input);
        DataStore.Update(data =>
        {
            var missing = route.HopProfileIds.Where(id => data.Profiles.All(profile => profile.Id != id)).ToList();
            if (missing.Count > 0)
            {
                throw new BusinessException("Ein ausgewähltes SSH-Profil existiert nicht mehr.", nameof(TunnelRoute.HopProfileIds));
            }

            if (data.Routes.Any(other => other.Id != route.Id && string.Equals(other.Name, route.Name, StringComparison.CurrentCultureIgnoreCase)))
            {
                throw new BusinessException("Eine Tunnelroute mit diesem Namen existiert bereits.", nameof(TunnelRoute.Name));
            }

            var portOwner = data.Routes.FirstOrDefault(other => other.Id != route.Id && other.LocalPort == route.LocalPort &&
                string.Equals(other.LocalAddress, route.LocalAddress, StringComparison.OrdinalIgnoreCase));
            if (portOwner is not null)
            {
                throw new BusinessException($"Der lokale Port wird bereits von „{portOwner.Name}“ verwendet.", nameof(TunnelRoute.LocalPort));
            }

            var index = data.Routes.FindIndex(existing => existing.Id == route.Id);
            if (index >= 0)
            {
                data.Routes[index] = route;
            }
            else
            {
                data.Routes.Add(route);
            }
        });

        return route;
    }

    /// <summary>Checks and cleans the fields of a route (save and import).</summary>
    internal static TunnelRoute Normalize(TunnelRoute input)
    {
        var route = input.Clone();
        route.Name = InputRules.RequireText(route.Name, nameof(TunnelRoute.Name), "Name");
        route.LocalAddress = InputRules.RequireToken(route.LocalAddress, nameof(TunnelRoute.LocalAddress), "Lokale Adresse");
        route.LocalPort = InputRules.RequirePort(route.LocalPort, nameof(TunnelRoute.LocalPort), "Lokaler Port");
        route.TargetHost = InputRules.RequireToken(route.TargetHost, nameof(TunnelRoute.TargetHost), "Zielhost");
        route.TargetPort = InputRules.RequirePort(route.TargetPort, nameof(TunnelRoute.TargetPort), "Zielport");
        AccessPolicy.EnsureBindAddressAllowed(route.LocalAddress);

        if (route.HopProfileIds.Count == 0)
        {
            throw new BusinessException("Mindestens ein SSH-Hop ist erforderlich.", nameof(TunnelRoute.HopProfileIds));
        }

        if (route.HopProfileIds.Count > 10)
        {
            throw new BusinessException("Höchstens 10 SSH-Hops sind möglich.", nameof(TunnelRoute.HopProfileIds));
        }

        return route;
    }

    /// <summary>Disconnects and deletes a route.</summary>
    public static void Delete(Guid id)
    {
        AccessPolicy.EnsureConfigurationEditable();

        TunnelManager.Disconnect(id);
        DataStore.Update(data => data.Routes.RemoveAll(route => route.Id == id));
    }
}
