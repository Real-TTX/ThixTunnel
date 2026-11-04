using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Infrastructure;
using ThixTunnel.Core.Models;

namespace ThixTunnel.Tests;

/// <summary>Runs only inside scripts/e2e-docker.ps1, which provides the SSH servers, the key and the known_hosts file.</summary>
public sealed class EndToEndFactAttribute : FactAttribute
{
    public EndToEndFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("THIXTUNNEL_E2E_KEY") is null)
        {
            Skip = "End-to-end test: run scripts/e2e-docker.ps1";
        }
    }
}

/// <summary>
/// Real ssh.exe against Docker: local port → hop1 (jump server) → hop2 → web:80 (docs/konzept.md, acceptance criterion 2).
/// </summary>
[Collection(SharedStateCollection.Name)]
[Trait("Category", "E2E")]
public sealed class EndToEndTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
    private readonly TestData _data = new();

    public EndToEndTests()
    {
        SshConfigWriter.KnownHostsFileForTests = Environment.GetEnvironmentVariable("THIXTUNNEL_E2E_KNOWN_HOSTS");
    }

    public void Dispose()
    {
        TunnelManager.DisconnectAll();
        TunnelManager.SetAutomationPaused(false);
        SshConfigWriter.KnownHostsFileForTests = null;
        _data.Dispose();
    }

    [EndToEndFact]
    public async Task TwoHopTunnelForwardsTheLocalPortToTheTarget()
    {
        var route = CreateRoute(sshPortOfHop1: 22221);

        TunnelManager.ConnectAutomatically(route.Id);
        await WaitForAsync(route.Id, TunnelState.Connected);

        using var http = new HttpClient();
        var page = await http.GetStringAsync($"http://127.0.0.1:{route.LocalPort}/");
        Assert.Contains("nginx", page);

        TunnelManager.Disconnect(route.Id);
        Assert.Equal(TunnelState.Disconnected, TunnelManager.GetStatus(route.Id).State);
        await Task.Delay(500);
        Assert.False(NetworkProbe.IsListening("127.0.0.1", route.LocalPort));
    }

    [EndToEndFact]
    public async Task AnUnknownHostKeyIsNeverAcceptedAutomatically()
    {
        var emptyKnownHosts = Path.Combine(_data.Directory, "known_hosts_empty");
        File.WriteAllText(emptyKnownHosts, "");
        SshConfigWriter.KnownHostsFileForTests = emptyKnownHosts;
        var route = CreateRoute(sshPortOfHop1: 22221);

        TunnelManager.ConnectAutomatically(route.Id);
        var status = await WaitForAsync(route.Id, TunnelState.Failed);

        Assert.Equal(TunnelProblem.HostKeyUnknown, status.Problem);
    }

    [EndToEndFact]
    public async Task ManualDisconnectEndsTheRetries()
    {
        var route = CreateRoute(sshPortOfHop1: 22299); // nobody listens there

        TunnelManager.ConnectAutomatically(route.Id);
        await WaitForAsync(route.Id, TunnelState.WaitingForRetry);

        TunnelManager.Disconnect(route.Id);
        await Task.Delay(TimeSpan.FromSeconds(5));

        Assert.Equal(TunnelState.Disconnected, TunnelManager.GetStatus(route.Id).State);
    }

    [EndToEndFact]
    public async Task AnInterruptedTunnelReconnects()
    {
        var route = CreateRoute(sshPortOfHop1: 22221);
        TunnelManager.ConnectAutomatically(route.Id);
        await WaitForAsync(route.Id, TunnelState.Connected);

        Docker("restart", "thixtunnel-e2e-hop2");
        await WaitForAsync(route.Id, TunnelState.WaitingForRetry);
        await WaitForAsync(route.Id, TunnelState.Connected);

        using var http = new HttpClient();
        Assert.Contains("nginx", await http.GetStringAsync($"http://127.0.0.1:{route.LocalPort}/"));
    }

    [EndToEndFact]
    public async Task APasswordProfileGetsItsKeyAtTheFirstManualConnect()
    {
        // The "password dialog" of the test answers with the password of the user "setup" on hop2.
        var askPass = Path.Combine(_data.Directory, "askpass.cmd");
        File.WriteAllText(askPass, "@echo e2e-passwort\r\n");
        TunnelManager.AskPassProgram = askPass;
        var key = Environment.GetEnvironmentVariable("THIXTUNNEL_E2E_KEY")!;
        var hop1 = ProfileService.Save(new SshProfile
        {
            Name = "Sprungserver", HostName = "127.0.0.1", Port = 22221, UserName = "tunnel",
            AuthenticationMethod = SshAuthenticationMethod.KeyFile, KeyFilePath = key,
        });
        var hop2 = ProfileService.Save(new SshProfile
        {
            Name = "Zielserver", HostName = "hop2", UserName = "setup",
            AuthenticationMethod = SshAuthenticationMethod.Password, SetUpKeyOnFirstLogin = true,
        });
        var route = RouteService.Save(new TunnelRoute
        {
            Name = "Mit Schlüssel-Einrichtung", HopProfileIds = [hop1.Id, hop2.Id], LocalPort = FreePort(), TargetHost = "web", TargetPort = 80,
        });

        try
        {
            TunnelManager.Connect(route.Id); // manual: the one time the password is used
            await WaitForAsync(route.Id, TunnelState.Connected);

            var converted = ProfileService.Get(hop2.Id)!;
            Assert.Equal(SshAuthenticationMethod.KeyFile, converted.AuthenticationMethod);
            Assert.False(converted.SetUpKeyOnFirstLogin);
            Assert.True(File.Exists(converted.KeyFilePath));

            // Automatic connects never prompt: this one only works with the key that is now on the server.
            TunnelManager.Disconnect(route.Id);
            TunnelManager.AskPassProgram = null;
            TunnelManager.ConnectAutomatically(route.Id);
            await WaitForAsync(route.Id, TunnelState.Connected);
            using var http = new HttpClient();
            Assert.Contains("nginx", await http.GetStringAsync($"http://127.0.0.1:{route.LocalPort}/"));
        }
        finally
        {
            TunnelManager.AskPassProgram = null;
        }
    }

    private TunnelRoute CreateRoute(int sshPortOfHop1)
    {
        var key = Environment.GetEnvironmentVariable("THIXTUNNEL_E2E_KEY")!;
        var hop1 = ProfileService.Save(new SshProfile
        {
            Name = "Profil 1", HostName = "127.0.0.1", Port = sshPortOfHop1, UserName = "tunnel",
            AuthenticationMethod = SshAuthenticationMethod.KeyFile, KeyFilePath = key,
        });
        var hop2 = ProfileService.Save(new SshProfile
        {
            Name = "Profil 2", HostName = "hop2", UserName = "tunnel",
            AuthenticationMethod = SshAuthenticationMethod.KeyFile, KeyFilePath = key,
        });

        return RouteService.Save(new TunnelRoute
        {
            Name = "Serverzugang",
            HopProfileIds = [hop1.Id, hop2.Id],
            LocalPort = FreePort(),
            TargetHost = "web",
            TargetPort = 80,
            ReconnectOnFailure = true,
        });
    }

    private static async Task<TunnelStatus> WaitForAsync(Guid routeId, TunnelState state)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < Timeout)
        {
            var status = TunnelManager.GetStatus(routeId);
            if (status.State == state)
            {
                return status;
            }

            await Task.Delay(200);
        }

        var last = TunnelManager.GetStatus(routeId);
        throw new TimeoutException($"Expected {state}, still {last.State}: {last.Message}");
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static void Docker(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("docker") { UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        process.WaitForExit();
    }
}
