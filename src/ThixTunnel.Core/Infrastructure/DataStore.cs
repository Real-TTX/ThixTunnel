using System.Text.Json;
using System.Text.Json.Serialization;
using ThixTunnel.Core.Models;

namespace ThixTunnel.Core.Infrastructure;

/// <summary>
/// Reads and writes the user's JSON file. Only the business layer uses this class.
/// Writes go to a temporary file first and replace the old one, so a crash never leaves half a file.
/// </summary>
internal static class DataStore
{
    private static readonly object Gate = new();
    private static AppData? _cache;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Gives a deep copy; changes have to go through <see cref="Update"/>.</summary>
    public static AppData Read()
    {
        lock (Gate)
        {
            _cache ??= Load();
            return Copy(_cache);
        }
    }

    /// <summary>Changes the data in one step and saves it.</summary>
    public static void Update(Action<AppData> change)
    {
        lock (Gate)
        {
            var data = Copy(_cache ??= Load());
            change(data);
            Save(data);
            _cache = data;
        }
    }

    /// <summary>Drops the cache (tests, or after the file was replaced from outside).</summary>
    public static void Reset()
    {
        lock (Gate)
        {
            _cache = null;
        }
    }

    private static AppData Load()
    {
        var path = AppPaths.DataFile;
        if (!File.Exists(path))
        {
            return new AppData();
        }

        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<AppData>(stream, SerializerOptions) ?? new AppData();
        }
        catch (JsonException)
        {
            // Keep the broken file for the user instead of overwriting it with an empty configuration.
            File.Copy(path, path + $".broken-{DateTime.Now:yyyyMMdd-HHmmss}", overwrite: true);
            return new AppData();
        }
    }

    private static void Save(AppData data)
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);
        var path = AppPaths.DataFile;
        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(data, SerializerOptions));
        File.Move(temporaryPath, path, overwrite: true);
    }

    private static AppData Copy(AppData data) =>
        JsonSerializer.Deserialize<AppData>(JsonSerializer.Serialize(data, SerializerOptions), SerializerOptions)!;
}
