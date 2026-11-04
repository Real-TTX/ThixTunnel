namespace ThixTunnel.Core.Models;

/// <summary>Everything ThixTunnel stores for a user: one JSON file.</summary>
public sealed class AppData
{
    public int SchemaVersion { get; set; } = 1;

    public List<SshProfile> Profiles { get; set; } = [];

    public List<TunnelRoute> Routes { get; set; } = [];

    public AppSettings Settings { get; set; } = new();
}
