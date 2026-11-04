using ThixTunnel.Core.Infrastructure;
using ThixTunnel.Core.Models;

namespace ThixTunnel.Core.Business;

/// <summary>
/// Connects and disconnects routes and keeps their state.
///
/// Rules (docs/konzept.md, chapter 6):
///  - A manual connection may ask (password, unknown host key); automatic and repeated attempts never do.
///  - Manual disconnect also ends pending retries; the route stays disconnected for this session.
///  - Interruptions are retried with growing pauses when the route wants it; authentication and host key
///    problems are not retried - they need the user.
///  - <see cref="DisconnectAll"/> ends every ssh process (the job object does the same after a crash).
/// </summary>
public static class TunnelManager
{
    private static readonly object Gate = new();
    private static readonly Dictionary<Guid, Session> Sessions = [];
    private static readonly Dictionary<Guid, TunnelStatus> Statuses = [];
    private static readonly TimeSpan StablePeriod = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan MaximumRetryDelay = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ListenTimeout = TimeSpan.FromSeconds(90);

    /// <summary>Raised on a background thread whenever the state of a route changes.</summary>
    public static event EventHandler<TunnelStatus>? StatusChanged;

    /// <summary>Paused: no automatic connections and no retries until resumed.</summary>
    public static bool AutomationPaused { get; private set; }

    /// <summary>The program ssh calls for prompts (SSH_ASKPASS). Set by the application at start.</summary>
    public static string? AskPassProgram { get; set; }

    public static TunnelStatus GetStatus(Guid routeId)
    {
        lock (Gate)
        {
            return Statuses.GetValueOrDefault(routeId) ?? TunnelStatus.Idle(routeId);
        }
    }

    public static IReadOnlyList<TunnelStatus> GetStatuses()
    {
        lock (Gate)
        {
            return [.. Statuses.Values];
        }
    }

    public static int ConnectedCount
    {
        get
        {
            lock (Gate)
            {
                return Statuses.Values.Count(status => status.State == TunnelState.Connected);
            }
        }
    }

    /// <summary>Connects a route on behalf of the user (prompts allowed).</summary>
    public static void Connect(Guid routeId) => Start(routeId, interactive: true, attempt: 0);

    /// <summary>Disconnects on behalf of the user; ends retries too.</summary>
    public static void Disconnect(Guid routeId)
    {
        Session? session;
        lock (Gate)
        {
            if (!Sessions.Remove(routeId, out session))
            {
                if (Statuses.TryGetValue(routeId, out var status) && status.State != TunnelState.Disconnected)
                {
                    SetStatus(TunnelStatus.Idle(routeId));
                }

                return;
            }

            session.StopRequested = true;
        }

        session.Cancellation.Cancel();
        session.Process?.Dispose();
        SshConfigWriter.Delete(routeId);
        SetStatus(TunnelStatus.Idle(routeId));
        Log.Info($"Route {routeId} disconnected by the user");
    }

    /// <summary>Connects every route with "connect on start-up" (without prompts).</summary>
    public static void ConnectStartupRoutes()
    {
        if (AutomationPaused)
        {
            return;
        }

        foreach (var route in RouteService.List().Where(route => route.ConnectOnStartup))
        {
            if (!GetStatus(route.Id).IsActive)
            {
                Start(route.Id, interactive: false, attempt: 0);
            }
        }
    }

    public static void SetAutomationPaused(bool paused)
    {
        AutomationPaused = paused;
        Log.Info(paused ? "Automation paused" : "Automation resumed");
        if (!paused)
        {
            ConnectStartupRoutes();
            return;
        }

        // Pending retries stop; running tunnels keep running.
        List<Guid> waiting;
        lock (Gate)
        {
            waiting = Statuses.Values.Where(status => status.State == TunnelState.WaitingForRetry).Select(status => status.RouteId).ToList();
        }

        foreach (var routeId in waiting)
        {
            Disconnect(routeId);
        }
    }

    /// <summary>The network came back (or the PC woke up): routes waiting for their next attempt try now.</summary>
    public static void RetryWaitingRoutesNow()
    {
        List<Session> waiting;
        lock (Gate)
        {
            waiting = Sessions.Values.Where(session => session.RetryDelay is not null).ToList();
        }

        foreach (var session in waiting)
        {
            session.RetryDelay?.Cancel();
        }
    }

    /// <summary>Connects without prompts (after an update, like an automatic connection).</summary>
    public static void ConnectAutomatically(Guid routeId) => Start(routeId, interactive: false, attempt: 0);

    /// <summary>Ids of the routes that are connected or trying to be.</summary>
    public static IReadOnlyList<Guid> GetActiveRouteIds()
    {
        lock (Gate)
        {
            return [.. Sessions.Keys];
        }
    }

    /// <summary>Ends every tunnel of this application (also used when the application ends).</summary>
    public static void DisconnectAll()
    {
        List<Guid> routeIds;
        lock (Gate)
        {
            routeIds = [.. Sessions.Keys];
        }

        foreach (var routeId in routeIds)
        {
            Disconnect(routeId);
        }
    }

    private static void Start(Guid routeId, bool interactive, int attempt)
    {
        var route = RouteService.Get(routeId) ?? throw new BusinessException("Die Tunnelroute existiert nicht mehr.");
        var hops = RouteService.GetHops(route);
        if (hops.Count == 0 || hops.Count != route.HopProfileIds.Count)
        {
            throw new BusinessException("Ein SSH-Profil dieser Route fehlt. Bitte die Route bearbeiten.");
        }

        AccessPolicy.EnsureBindAddressAllowed(route.LocalAddress);

        Session session;
        lock (Gate)
        {
            if (Sessions.TryGetValue(routeId, out var existing))
            {
                if (attempt == 0 && existing.Process is null)
                {
                    // waiting for a retry: the user wants it now
                    existing.RetryDelay?.Cancel();
                }

                return;
            }

            session = new Session(routeId);
            Sessions[routeId] = session;
        }

        _ = RunAsync(session, route, hops, interactive, attempt);
    }

    private static async Task RunAsync(Session session, TunnelRoute route, IReadOnlyList<SshProfile> hops, bool interactive, int attempt)
    {
        var token = session.Cancellation.Token;
        while (!token.IsCancellationRequested)
        {
            var result = await RunOnceAsync(session, route, hops, interactive, attempt, token);
            if (token.IsCancellationRequested)
            {
                return;
            }

            if (result.WasStable)
            {
                attempt = 0;
            }

            var mayRetry = route.ReconnectOnFailure && !AutomationPaused && SshErrorClassifier.IsRetryable(result.Problem);
            if (!mayRetry)
            {
                EndSession(session, new TunnelStatus(route.Id, TunnelState.Failed, result.Problem, result.Message, DateTimeOffset.Now));
                return;
            }

            attempt++;
            var delay = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, attempt), MaximumRetryDelay.TotalSeconds));
            SetStatus(new TunnelStatus(route.Id, TunnelState.WaitingForRetry, result.Problem, result.Message,
                DateTimeOffset.Now, DateTimeOffset.Now + delay, attempt), session);
            Log.Info($"Route '{route.Name}': {result.Message}; attempt {attempt} in {delay.TotalSeconds:0} s");

            using (session.RetryDelay = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                try
                {
                    await Task.Delay(delay, session.RetryDelay.Token);
                }
                catch (OperationCanceledException)
                {
                    // cancelled: either "now" (network back) or disconnect - the loop condition decides
                }
            }

            session.RetryDelay = null;

            // Pick up changes to the route made in the meantime.
            route = RouteService.Get(route.Id) ?? route;
            hops = RouteService.GetHops(route);
            interactive = false;
        }
    }

    private static async Task<AttemptResult> RunOnceAsync(Session session, TunnelRoute route, IReadOnlyList<SshProfile> hops,
        bool interactive, int attempt, CancellationToken token)
    {
        var ssh = SshExecutable.Find(SettingsService.Get().SshExecutablePath);
        if (ssh is null)
        {
            return new AttemptResult(TunnelProblem.SshNotFound, SshErrorClassifier.Describe(TunnelProblem.SshNotFound), false);
        }

        if (NetworkProbe.IsListening(route.LocalAddress, route.LocalPort))
        {
            return new AttemptResult(TunnelProblem.LocalPortInUse, $"Lokaler Port {route.LocalPort} ist bereits belegt", false);
        }

        SetStatus(new TunnelStatus(route.Id, TunnelState.Connecting, TunnelProblem.None,
            attempt == 0 ? "Verbindung wird aufgebaut …" : $"Neuer Versuch {attempt} …", DateTimeOffset.Now, Attempt: attempt), session);
        Log.Info($"Route '{route.Name}': connecting via {hops.Count} hop(s), interactive={interactive}");

        SshProcess process;
        try
        {
            process = SshProcess.Start(ssh, route, hops, interactive, AskPassProgram);
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
        {
            Log.Error($"Route '{route.Name}': ssh could not be started", exception);
            return new AttemptResult(TunnelProblem.SshNotFound, $"ssh konnte nicht gestartet werden: {exception.Message}", false);
        }

        session.Process = process;
        DateTimeOffset? connectedSince = null;
        var startedAt = DateTimeOffset.Now;

        try
        {
            while (!process.HasExited)
            {
                if (connectedSince is null && NetworkProbe.IsListening(route.LocalAddress, route.LocalPort))
                {
                    connectedSince = DateTimeOffset.Now;
                    SetStatus(new TunnelStatus(route.Id, TunnelState.Connected, TunnelProblem.None, "Tunnel erfolgreich aufgebaut", connectedSince.Value), session);
                    Log.Info($"Route '{route.Name}': connected on {route.LocalEndpoint}");
                }

                if (connectedSince is null && !interactive && DateTimeOffset.Now - startedAt > ListenTimeout)
                {
                    process.Stop();
                    break;
                }

                // Faster while connecting, slower once the tunnel is up (then only the exit matters).
                var pollInterval = connectedSince is null ? TimeSpan.FromMilliseconds(400) : TimeSpan.FromSeconds(5);
                using var wait = CancellationTokenSource.CreateLinkedTokenSource(token);
                wait.CancelAfter(pollInterval);
                try
                {
                    await process.WaitForExitAsync(wait.Token);
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                }
            }
        }
        catch (OperationCanceledException)
        {
            return new AttemptResult(TunnelProblem.None, "Getrennt", false);
        }
        finally
        {
            session.Process = null;
            process.Dispose();
        }

        var errorOutput = process.ErrorOutput;
        var problem = SshErrorClassifier.Classify(errorOutput);
        if (connectedSince is not null && problem == TunnelProblem.Unknown)
        {
            problem = TunnelProblem.ConnectionLost;
        }

        var detail = SshErrorClassifier.LastLine(errorOutput);
        var message = SshErrorClassifier.Describe(problem);
        Log.Warning($"Route '{route.Name}': ssh ended ({problem}) {detail}");
        if (detail.Length > 0 && problem is TunnelProblem.Unknown or TunnelProblem.HostUnreachable)
        {
            message = $"{message}: {detail}";
        }

        var wasStable = connectedSince is not null && DateTimeOffset.Now - connectedSince.Value > StablePeriod;
        return new AttemptResult(problem, message, wasStable);
    }

    private static void EndSession(Session session, TunnelStatus finalStatus)
    {
        lock (Gate)
        {
            if (Sessions.TryGetValue(session.RouteId, out var current) && current == session)
            {
                Sessions.Remove(session.RouteId);
            }
        }

        SshConfigWriter.Delete(session.RouteId);
        SetStatus(finalStatus, session);
    }

    /// <param name="session">The session reporting; a stopped session cannot overwrite the state any more.</param>
    private static void SetStatus(TunnelStatus status, Session? session = null)
    {
        lock (Gate)
        {
            if (session is not null && (session.StopRequested || session.Cancellation.IsCancellationRequested))
            {
                return;
            }

            Statuses[status.RouteId] = status;
        }

        StatusChanged?.Invoke(null, status);
    }

    private sealed class Session(Guid routeId)
    {
        public Guid RouteId { get; } = routeId;

        public CancellationTokenSource Cancellation { get; } = new();

        public CancellationTokenSource? RetryDelay { get; set; }

        public SshProcess? Process { get; set; }

        public bool StopRequested { get; set; }
    }

    private sealed record AttemptResult(TunnelProblem Problem, string Message, bool WasStable);
}
