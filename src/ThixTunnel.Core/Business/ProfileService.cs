using ThixTunnel.Core.Infrastructure;
using ThixTunnel.Core.Models;

namespace ThixTunnel.Core.Business;

/// <summary>SSH profiles: list, read, save, delete.</summary>
public static class ProfileService
{
    public const string FilterAll = "";
    public const string FilterAgent = "agent";
    public const string FilterKeyFile = "keyfile";
    public const string FilterPassword = "password";
    public const string FilterUnused = "unused";

    public const string SortByName = "name";
    public const string SortByHost = "host";
    public const string SortByUsage = "usage";

    public static IReadOnlyList<SshProfile> List(ListQuery? query = null)
    {
        query ??= new ListQuery();
        var data = DataStore.Read();
        var usage = CountUsage(data);

        var profiles = data.Profiles
            .Where(profile => InputRules.Matches(query.SearchText, profile.Name, profile.HostName, profile.UserName, profile.Description))
            .Where(profile => query.Filter switch
            {
                FilterAgent => profile.AuthenticationMethod == SshAuthenticationMethod.Agent,
                FilterKeyFile => profile.AuthenticationMethod == SshAuthenticationMethod.KeyFile,
                FilterPassword => profile.AuthenticationMethod == SshAuthenticationMethod.Password,
                FilterUnused => usage.GetValueOrDefault(profile.Id) == 0,
                _ => true,
            });

        Func<SshProfile, object> sortKey = query.SortBy switch
        {
            SortByHost => profile => profile.HostName,
            SortByUsage => profile => usage.GetValueOrDefault(profile.Id),
            _ => profile => profile.Name,
        };

        var sorted = query.Direction == SortDirection.Descending
            ? profiles.OrderByDescending(sortKey)
            : profiles.OrderBy(sortKey);
        return sorted.ThenBy(profile => profile.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public static SshProfile? Get(Guid id) => DataStore.Read().Profiles.FirstOrDefault(profile => profile.Id == id);

    /// <summary>Number of routes that use each profile.</summary>
    public static IReadOnlyDictionary<Guid, int> GetUsage() => CountUsage(DataStore.Read());

    /// <summary>Names of the routes that use the profile.</summary>
    public static IReadOnlyList<string> GetRouteNamesUsing(Guid profileId) =>
        DataStore.Read().Routes.Where(route => route.HopProfileIds.Contains(profileId)).Select(route => route.Name).ToList();

    /// <summary>Validates and stores a new or changed profile; returns the stored version.</summary>
    public static SshProfile Save(SshProfile input)
    {
        AccessPolicy.EnsureConfigurationEditable();

        var profile = input.Clone();
        profile.Name = InputRules.RequireText(profile.Name, nameof(SshProfile.Name), "Name");
        profile.HostName = InputRules.RequireToken(profile.HostName, nameof(SshProfile.HostName), "Server");
        profile.UserName = InputRules.RequireToken(profile.UserName, nameof(SshProfile.UserName), "Benutzer");
        profile.Port = InputRules.RequirePort(profile.Port, nameof(SshProfile.Port), "SSH-Port");
        profile.Description = (profile.Description ?? "").Trim();

        if (profile.AuthenticationMethod == SshAuthenticationMethod.KeyFile)
        {
            profile.KeyFilePath = InputRules.RequireText(profile.KeyFilePath, nameof(SshProfile.KeyFilePath), "Schlüsseldatei", 400);
            if (!File.Exists(profile.KeyFilePath))
            {
                throw new BusinessException("Die Schlüsseldatei wurde nicht gefunden.", nameof(SshProfile.KeyFilePath));
            }
        }
        else
        {
            profile.KeyFilePath = "";
        }

        DataStore.Update(data =>
        {
            if (data.Profiles.Any(other => other.Id != profile.Id && string.Equals(other.Name, profile.Name, StringComparison.CurrentCultureIgnoreCase)))
            {
                throw new BusinessException("Ein Profil mit diesem Namen existiert bereits.", nameof(SshProfile.Name));
            }

            var index = data.Profiles.FindIndex(existing => existing.Id == profile.Id);
            if (index >= 0)
            {
                data.Profiles[index] = profile;
            }
            else
            {
                data.Profiles.Add(profile);
            }
        });

        return profile;
    }

    /// <summary>Deletes a profile. A profile that is still part of a route cannot be deleted.</summary>
    public static void Delete(Guid id)
    {
        AccessPolicy.EnsureConfigurationEditable();

        var routeNames = GetRouteNamesUsing(id);
        if (routeNames.Count > 0)
        {
            throw new BusinessException($"Das Profil wird noch verwendet von: {string.Join(", ", routeNames)}.");
        }

        DataStore.Update(data => data.Profiles.RemoveAll(profile => profile.Id == id));
    }

    private static Dictionary<Guid, int> CountUsage(AppData data) =>
        data.Routes.SelectMany(route => route.HopProfileIds.Distinct())
            .GroupBy(id => id)
            .ToDictionary(group => group.Key, group => group.Count());
}
