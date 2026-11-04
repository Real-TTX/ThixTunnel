using System.Text.Json;
using System.Text.Json.Serialization;
using ThixTunnel.Core.Infrastructure;
using ThixTunnel.Core.Models;

namespace ThixTunnel.Core.Business;

/// <summary>The content of an export file: SSH profiles and the connections (tunnel routes) built from them.</summary>
public sealed class TransferFile
{
    public const string FormatName = "ThixTunnel-Export";

    public string Format { get; set; } = "";

    public int FormatVersion { get; set; } = 1;

    public string AppVersion { get; set; } = "";

    public DateTimeOffset ExportedAt { get; set; }

    public List<SshProfile> Profiles { get; set; } = [];

    public List<TunnelRoute> Routes { get; set; } = [];
}

/// <summary>What an import did. Problems name the entries that were left out and why.</summary>
public sealed record ImportResult(
    int ProfilesAdded,
    int ProfilesReplaced,
    int ProfilesSkipped,
    int RoutesAdded,
    int RoutesReplaced,
    int RoutesSkipped,
    IReadOnlyList<string> Problems)
{
    public string Summary =>
        $"Profile: {ProfilesAdded} neu, {ProfilesReplaced} ersetzt, {ProfilesSkipped} übersprungen · " +
        $"Verbindungen: {RoutesAdded} neu, {RoutesReplaced} ersetzt, {RoutesSkipped} übersprungen";
}

/// <summary>
/// Export and import of SSH profiles and connections as a JSON file - to move ThixTunnel to another PC,
/// share a set of tunnels with colleagues or keep a backup. Passwords and passphrases are never stored, so an export
/// holds no secrets (key files are referenced by path only, not copied).
///
/// Import rules: an entry with the same ID - or else the same name - already exists: it is replaced when
/// <c>replaceExisting</c> is set, otherwise kept (the imported connections then use the existing profile).
/// Everything goes through the same checks as saving; an import is written in one step or not at all.
/// </summary>
public static class TransferService
{
    public const string FileFilter = "ThixTunnel-Export (*.thixtunnel.json)|*.thixtunnel.json|JSON-Dateien (*.json)|*.json|Alle Dateien (*.*)|*.*";
    public const string DefaultFileName = "ThixTunnel-Verbindungen.thixtunnel.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Writes all profiles and connections to <paramref name="path"/>; returns how many of each.</summary>
    public static (int Profiles, int Routes) Export(string path)
    {
        var file = new TransferFile
        {
            Format = TransferFile.FormatName,
            AppVersion = AppInfo.Version,
            ExportedAt = DateTimeOffset.Now,
            Profiles = [.. ProfileService.List()],
            Routes = [.. RouteService.List()],
        };

        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(file, SerializerOptions));
        File.Move(temporary, path, overwrite: true);
        Log.Info($"Exported {file.Profiles.Count} profiles and {file.Routes.Count} routes to {path}");
        return (file.Profiles.Count, file.Routes.Count);
    }

    /// <summary>Reads an export file without changing anything (to show what it contains).</summary>
    public static TransferFile Read(string path)
    {
        TransferFile? file;
        try
        {
            file = JsonSerializer.Deserialize<TransferFile>(File.ReadAllText(path), SerializerOptions);
        }
        catch (JsonException)
        {
            file = null;
        }

        if (file is null || file.Format != TransferFile.FormatName)
        {
            throw new BusinessException("Die Datei ist keine ThixTunnel-Exportdatei.");
        }

        if (file.FormatVersion > 1)
        {
            throw new BusinessException("Die Exportdatei stammt aus einer neueren ThixTunnel-Version. Bitte ThixTunnel aktualisieren.");
        }

        return file;
    }

    public static ImportResult Import(string path, bool replaceExisting)
    {
        AccessPolicy.EnsureConfigurationEditable();
        var file = Read(path);

        ImportResult? result = null;
        DataStore.Update(data => result = Merge(data, file, replaceExisting));
        Log.Info($"Imported {path}: {result!.Summary}");
        return result;
    }

    private static ImportResult Merge(AppData data, TransferFile file, bool replaceExisting)
    {
        var problems = new List<string>();
        int profilesAdded = 0, profilesReplaced = 0, profilesSkipped = 0;
        int routesAdded = 0, routesReplaced = 0, routesSkipped = 0;

        // Imported profile ID -> ID of the profile the imported connections have to use.
        var profileIds = new Dictionary<Guid, Guid>();
        foreach (var imported in file.Profiles)
        {
            SshProfile profile;
            try
            {
                profile = ProfileService.Normalize(imported, requireKeyFile: false);
            }
            catch (BusinessException exception)
            {
                problems.Add($"Profil „{imported.Name}“: {exception.Message}");
                profilesSkipped++;
                continue;
            }

            var existing = data.Profiles.FirstOrDefault(candidate => candidate.Id == profile.Id) ??
                           data.Profiles.FirstOrDefault(candidate => SameName(candidate.Name, profile.Name));
            if (existing is not null && !replaceExisting)
            {
                profileIds[imported.Id] = existing.Id;
                profilesSkipped++;
                continue;
            }

            if (existing is not null)
            {
                profile.Id = existing.Id;
            }

            if (data.Profiles.Any(other => other.Id != profile.Id && SameName(other.Name, profile.Name)))
            {
                problems.Add($"Profil „{profile.Name}“: ein anderes Profil trägt bereits diesen Namen.");
                profilesSkipped++;
                continue;
            }

            if (profile.AuthenticationMethod == SshAuthenticationMethod.KeyFile && !File.Exists(profile.KeyFilePath))
            {
                problems.Add($"Profil „{profile.Name}“: Schlüsseldatei {profile.KeyFilePath} fehlt auf diesem PC – bitte im Profil anpassen.");
            }

            Upsert(data.Profiles, profile, candidate => candidate.Id);
            profileIds[imported.Id] = profile.Id;
            if (existing is null)
            {
                profilesAdded++;
            }
            else
            {
                profilesReplaced++;
            }
        }

        foreach (var imported in file.Routes)
        {
            var hops = imported.HopProfileIds.Select(id => profileIds.TryGetValue(id, out var mapped) ? mapped : (Guid?)null).ToList();
            if (hops.Any(id => id is null))
            {
                problems.Add($"Verbindung „{imported.Name}“: ein SSH-Profil fehlt in der Datei.");
                routesSkipped++;
                continue;
            }

            TunnelRoute route;
            try
            {
                route = RouteService.Normalize(imported);
            }
            catch (BusinessException exception)
            {
                problems.Add($"Verbindung „{imported.Name}“: {exception.Message}");
                routesSkipped++;
                continue;
            }

            route.HopProfileIds = hops.Select(id => id!.Value).ToList();
            var existing = data.Routes.FirstOrDefault(candidate => candidate.Id == route.Id) ??
                           data.Routes.FirstOrDefault(candidate => SameName(candidate.Name, route.Name));
            if (existing is not null && !replaceExisting)
            {
                routesSkipped++;
                continue;
            }

            if (existing is not null)
            {
                route.Id = existing.Id;
            }

            var portOwner = data.Routes.FirstOrDefault(other => other.Id != route.Id && other.LocalPort == route.LocalPort &&
                                                                string.Equals(other.LocalAddress, route.LocalAddress, StringComparison.OrdinalIgnoreCase));
            if (portOwner is not null || data.Routes.Any(other => other.Id != route.Id && SameName(other.Name, route.Name)))
            {
                problems.Add(portOwner is not null
                    ? $"Verbindung „{route.Name}“: lokaler Port {route.LocalPort} wird bereits von „{portOwner.Name}“ verwendet."
                    : $"Verbindung „{route.Name}“: eine andere Verbindung trägt bereits diesen Namen.");
                routesSkipped++;
                continue;
            }

            Upsert(data.Routes, route, candidate => candidate.Id);
            if (existing is null)
            {
                routesAdded++;
            }
            else
            {
                routesReplaced++;
            }
        }

        return new ImportResult(profilesAdded, profilesReplaced, profilesSkipped, routesAdded, routesReplaced, routesSkipped, problems);
    }

    private static bool SameName(string first, string second) => string.Equals(first.Trim(), second.Trim(), StringComparison.CurrentCultureIgnoreCase);

    private static void Upsert<T>(List<T> items, T item, Func<T, Guid> id)
    {
        var index = items.FindIndex(candidate => id(candidate) == id(item));
        if (index >= 0)
        {
            items[index] = item;
        }
        else
        {
            items.Add(item);
        }
    }
}
