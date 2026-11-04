namespace ThixTunnel.Core.Models;

public enum TunnelState
{
    Disconnected = 0,
    Connecting = 1,
    Connected = 2,

    /// <summary>Interrupted; the next attempt is scheduled.</summary>
    WaitingForRetry = 3,

    /// <summary>Stopped with an error; the user has to act.</summary>
    Failed = 4,
}

public enum TunnelProblem
{
    None = 0,
    AuthenticationRequired = 1,
    HostKeyUnknown = 2,
    HostKeyChanged = 3,
    HostUnreachable = 4,
    LocalPortInUse = 5,
    ForwardingRefused = 6,
    SshNotFound = 7,
    ConnectionLost = 8,
    Unknown = 9,
}

/// <summary>Snapshot of the state of one route (immutable).</summary>
public sealed record TunnelStatus(
    Guid RouteId,
    TunnelState State,
    TunnelProblem Problem,
    string Message,
    DateTimeOffset Since,
    DateTimeOffset? NextAttempt = null,
    int Attempt = 0)
{
    public static TunnelStatus Idle(Guid routeId) =>
        new(routeId, TunnelState.Disconnected, TunnelProblem.None, "Getrennt", DateTimeOffset.Now);

    public bool IsActive => State is TunnelState.Connecting or TunnelState.Connected or TunnelState.WaitingForRetry;
}
