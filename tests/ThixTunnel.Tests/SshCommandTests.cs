using ThixTunnel.Core.Infrastructure;
using ThixTunnel.Core.Models;

namespace ThixTunnel.Tests;

public sealed class SshCommandTests
{
    private static readonly SshProfile Gateway = new() { Name = "Profil 1", HostName = "gateway.example.com", UserName = "tunneluser" };
    private static readonly SshProfile Server = new() { Name = "Profil 2", HostName = "10.0.0.24", UserName = "tunneluser", Port = 2222 };

    private static TunnelRoute ExampleRoute() => new()
    {
        Name = "Serverzugang",
        LocalAddress = "127.0.0.1",
        LocalPort = 4455,
        TargetHost = "127.0.0.1",
        TargetPort = 4453,
    };

    [Fact]
    public void TwoHopsUseTheFirstAsJumpHostAndTheLastAsTarget()
    {
        var arguments = SshCommandBuilder.BuildArguments(ExampleRoute(), 2, "route.conf");

        Assert.Equal(["-F", "route.conf", "-N", "-L", "127.0.0.1:4455:127.0.0.1:4453", "-J", "thixhop1", "thixhop2"], arguments);
    }

    [Fact]
    public void OneHopHasNoJumpHost()
    {
        var arguments = SshCommandBuilder.BuildArguments(ExampleRoute(), 1, "route.conf");

        Assert.DoesNotContain("-J", arguments);
        Assert.Equal("thixhop1", arguments[^1]);
    }

    [Fact]
    public void ThreeHopsChainTwoJumpHosts()
    {
        var arguments = SshCommandBuilder.BuildArguments(ExampleRoute(), 3, "route.conf");

        Assert.Equal("thixhop1,thixhop2", arguments[arguments.ToList().IndexOf("-J") + 1]);
        Assert.Equal("thixhop3", arguments[^1]);
    }

    [Fact]
    public void Ipv6AddressesGetBrackets()
    {
        var route = ExampleRoute();
        route.LocalAddress = "::1";
        route.TargetHost = "fd00::5";

        var arguments = SshCommandBuilder.BuildArguments(route, 1, "route.conf");

        Assert.Contains("[::1]:4455:[fd00::5]:4453", arguments);
    }

    [Fact]
    public void DescribedCommandShowsRealAddresses()
    {
        var command = SshCommandBuilder.DescribeCommand(ExampleRoute(), [Gateway, Server]);

        Assert.Equal("ssh -N -J tunneluser@gateway.example.com -L 127.0.0.1:4455:127.0.0.1:4453 tunneluser@10.0.0.24:2222", command);
    }

    [Fact]
    public void ConfigurationHasOneHostBlockPerHop()
    {
        var config = SshConfigWriter.Build([Gateway, Server], interactive: false);

        Assert.Contains("Host thixhop1", config);
        Assert.Contains("HostName gateway.example.com", config);
        Assert.Contains("Host thixhop2", config);
        Assert.Contains("Port 2222", config);
    }

    [Fact]
    public void AutomaticConnectionsNeverPrompt()
    {
        var config = SshConfigWriter.Build([Gateway], interactive: false);

        Assert.Contains("BatchMode yes", config);
        Assert.Contains("StrictHostKeyChecking yes", config);
        Assert.Contains("ExitOnForwardFailure yes", config);
    }

    [Fact]
    public void ManualConnectionsMayAskForUnknownHostKeys()
    {
        var config = SshConfigWriter.Build([Gateway], interactive: true);

        Assert.Contains("BatchMode no", config);
        Assert.Contains("StrictHostKeyChecking ask", config);
    }

    [Fact]
    public void KeyFileIsQuotedWithForwardSlashes()
    {
        var profile = Gateway.Clone();
        profile.AuthenticationMethod = SshAuthenticationMethod.KeyFile;
        profile.KeyFilePath = @"C:\Users\Max Muster\.ssh\id_ed25519";

        var config = SshConfigWriter.Build([profile], interactive: false);

        Assert.Contains("IdentityFile \"C:/Users/Max Muster/.ssh/id_ed25519\"", config);
        Assert.Contains("IdentitiesOnly yes", config);
    }
}
