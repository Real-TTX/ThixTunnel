namespace ThixTunnel.Core.Models;

/// <summary>
/// A named tunnel: an ordered list of SSH profiles and one local port forwarding.
/// Every profile before the last one is a jump server; the target is reached from the view of the last one.
/// </summary>
public sealed class TunnelRoute
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "";

    /// <summary>The SSH hops in connection order.</summary>
    public List<Guid> HopProfileIds { get; set; } = [];

    public string LocalAddress { get; set; } = "127.0.0.1";

    public int LocalPort { get; set; }

    public string TargetHost { get; set; } = "127.0.0.1";

    public int TargetPort { get; set; }

    /// <summary>Connect this route when the application starts.</summary>
    public bool ConnectOnStartup { get; set; }

    /// <summary>Reconnect after an unintended interruption.</summary>
    public bool ReconnectOnFailure { get; set; } = true;

    public string LocalEndpoint => $"{LocalAddress}:{LocalPort}";

    public string TargetEndpoint => $"{TargetHost}:{TargetPort}";

    public TunnelRoute Clone()
    {
        var copy = (TunnelRoute)MemberwiseClone();
        copy.HopProfileIds = [.. HopProfileIds];
        return copy;
    }
}
