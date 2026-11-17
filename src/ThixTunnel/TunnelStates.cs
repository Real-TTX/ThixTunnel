using MatDesktop;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Models;

namespace ThixTunnel;

/// <summary>
/// The state of a tunnel and of ThixTunnel as a whole in words, colours and MatDesktop's states (TRAY-02): one place for the
/// list, the title bar, the tray and Info.
/// </summary>
internal static class TunnelStates
{
    /// <summary>"Verbunden", "Verbindet …", "Wartet", "Fehler", "Getrennt".</summary>
    public static string Word(TunnelStatus status) => status.State switch
    {
        TunnelState.Connected => "Verbunden",
        TunnelState.Connecting => "Verbindet …",
        TunnelState.WaitingForRetry => "Wartet",
        TunnelState.Failed => "Fehler",
        _ => "Getrennt",
    };

    /// <summary>The colour of a tunnel's state: "Mat.Success", "Mat.Warning", "Mat.Danger" or "Neutral".</summary>
    public static string Token(TunnelStatus status) => status.State switch
    {
        TunnelState.Connected => "Mat.Success",
        TunnelState.Connecting or TunnelState.WaitingForRetry => "Mat.Warning",
        TunnelState.Failed => "Mat.Danger",
        _ => "Neutral",
    };

    /// <summary>The second line of a connection: the endpoints - or, while something is going on, what is going on.</summary>
    public static string Detail(TunnelRoute route, TunnelStatus status)
    {
        var endpoints = $"{route.LocalEndpoint} → {route.TargetEndpoint}";
        return status.State switch
        {
            TunnelState.WaitingForRetry when status.NextAttempt is { } next => $"{status.Message} – nächster Versuch um {next.ToLocalTime():HH:mm:ss}",
            TunnelState.Connecting or TunnelState.WaitingForRetry or TunnelState.Failed when status.Message.Length > 0 => status.Message,
            _ => endpoints,
        };
    }

    /// <summary>"Gateway → Datenbankserver".</summary>
    public static string Chain(IReadOnlyList<SshProfile> hops) =>
        hops.Count == 0 ? "Keine SSH-Hops" : string.Join(" → ", hops.Select(hop => hop.Name));

    /// <summary>ThixTunnel as a whole: red when a tunnel failed, amber while one connects or waits, green when one is connected.</summary>
    public static MatState Overall()
    {
        var statuses = TunnelManager.GetStatuses();
        if (statuses.Any(status => status.State == TunnelState.Failed))
        {
            return MatState.Custom("Fehler", "--mat-danger", MatStateMark.Cross);
        }

        if (TunnelManager.AutomationPaused)
        {
            return MatState.Custom("Automatik pausiert", "--mat-warning", MatStateMark.Pause);
        }

        if (statuses.Any(status => status.State is TunnelState.Connecting or TunnelState.WaitingForRetry))
        {
            return MatState.Custom("Verbindet", "--mat-warning", MatStateMark.Ring);
        }

        return statuses.Any(status => status.State == TunnelState.Connected)
            ? MatState.Custom("Verbunden", "--mat-success", MatStateMark.None)
            : RouteService.List().Count == 0
                ? MatState.NotSetUp
                : MatState.Custom("Kein Tunnel aktiv", "--mat-text-muted", MatStateMark.Dot);
    }

    /// <summary>"2 Tunnel aktiv · 1 Fehler" (title bar, tray tooltip, Info).</summary>
    public static string Summary()
    {
        var statuses = TunnelManager.GetStatuses();
        var connected = statuses.Count(status => status.State == TunnelState.Connected);
        var failed = statuses.Count(status => status.State == TunnelState.Failed);
        var waiting = statuses.Count(status => status.State is TunnelState.Connecting or TunnelState.WaitingForRetry);
        var text = connected switch
        {
            0 => "Kein Tunnel aktiv",
            1 => "1 Tunnel aktiv",
            _ => $"{connected} Tunnel aktiv",
        };
        if (waiting > 0)
        {
            text += $" · {waiting} wartet";
        }

        if (failed > 0)
        {
            text += failed == 1 ? " · 1 Fehler" : $" · {failed} Fehler";
        }

        return text;
    }
}
