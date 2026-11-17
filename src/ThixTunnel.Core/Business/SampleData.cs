using ThixTunnel.Core.Models;

namespace ThixTunnel.Core.Business;

/// <summary>
/// The sample setup of the snapshots and the UI tests: three SSH profiles and four connections of a typical user
/// ("anna"), saved through the business layer like the UI would, and the states the pictures show. Only ever written into a
/// data folder of its own (snapshot, tests) - never into the real one.
/// </summary>
public static class SampleData
{
    public static readonly Guid GatewayId = new("6f1c2a52-0c4e-4a8e-9a43-0d6d3c1f0a01");
    public static readonly Guid DatabaseId = new("6f1c2a52-0c4e-4a8e-9a43-0d6d3c1f0a02");
    public static readonly Guid BuildServerId = new("6f1c2a52-0c4e-4a8e-9a43-0d6d3c1f0a03");

    public static readonly Guid PostgresRouteId = new("2b7e4d10-5f3a-4c61-8d2e-7a9b0c1d2e01");
    public static readonly Guid GrafanaRouteId = new("2b7e4d10-5f3a-4c61-8d2e-7a9b0c1d2e02");
    public static readonly Guid JenkinsRouteId = new("2b7e4d10-5f3a-4c61-8d2e-7a9b0c1d2e03");
    public static readonly Guid RedisRouteId = new("2b7e4d10-5f3a-4c61-8d2e-7a9b0c1d2e04");

    /// <summary>Saves the profiles and connections.</summary>
    public static void Create()
    {
        ProfileService.Save(new SshProfile
        {
            Id = GatewayId, Name = "Gateway", HostName = "gateway.example.com", UserName = "anna",
            AuthenticationMethod = SshAuthenticationMethod.Agent, Description = "Sprungserver im Rechenzentrum",
        });
        ProfileService.Save(new SshProfile
        {
            Id = DatabaseId, Name = "Datenbankserver", HostName = "10.0.4.12", UserName = "anna",
            AuthenticationMethod = SshAuthenticationMethod.Agent,
        });
        ProfileService.Save(new SshProfile
        {
            Id = BuildServerId, Name = "Build-Server", HostName = "build.example.com", Port = 2222, UserName = "anna",
            AuthenticationMethod = SshAuthenticationMethod.Password, SetUpKeyOnFirstLogin = true,
        });

        RouteService.Save(new TunnelRoute
        {
            Id = PostgresRouteId, Name = "Postgres Produktion", HopProfileIds = [GatewayId, DatabaseId],
            LocalPort = 5433, TargetHost = "127.0.0.1", TargetPort = 5432, ConnectOnStartup = true,
        });
        RouteService.Save(new TunnelRoute
        {
            Id = GrafanaRouteId, Name = "Grafana", HopProfileIds = [GatewayId],
            LocalPort = 3000, TargetHost = "grafana.internal", TargetPort = 3000, ConnectOnStartup = true,
        });
        RouteService.Save(new TunnelRoute
        {
            Id = JenkinsRouteId, Name = "Jenkins", HopProfileIds = [BuildServerId],
            LocalPort = 8080, TargetHost = "127.0.0.1", TargetPort = 8080,
        });
        RouteService.Save(new TunnelRoute
        {
            Id = RedisRouteId, Name = "Redis Staging", HopProfileIds = [GatewayId, DatabaseId],
            LocalPort = 6380, TargetHost = "127.0.0.1", TargetPort = 6379,
        });
    }

    /// <summary>The states of the pictures: two tunnels connected, one waits for its next attempt, one failed.</summary>
    public static IReadOnlyList<TunnelStatus> Statuses(DateTimeOffset now) =>
    [
        new(PostgresRouteId, TunnelState.Connected, TunnelProblem.None, "Verbunden", now.AddMinutes(-42)),
        new(GrafanaRouteId, TunnelState.Connected, TunnelProblem.None, "Verbunden", now.AddMinutes(-42)),
        new(RedisRouteId, TunnelState.WaitingForRetry, TunnelProblem.ConnectionLost, "Verbindung unterbrochen", now.AddSeconds(-20), now.AddSeconds(40), 2),
        new(JenkinsRouteId, TunnelState.Failed, TunnelProblem.AuthenticationRequired, "Anmeldung fehlgeschlagen – Passwort beim manuellen Verbinden eingeben.", now.AddMinutes(-5)),
    ];
}
