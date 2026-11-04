using ThixTunnel.Core.Business;
using ThixTunnel.Core.Infrastructure;
using ThixTunnel.Core.Models;

namespace ThixTunnel.Tests;

[Collection(SharedStateCollection.Name)]
public sealed class BusinessLayerTests : IDisposable
{
    private readonly TestData _data = new();

    public void Dispose() => _data.Dispose();

    private static SshProfile SaveProfile(string name, string host = "gateway.example.com") =>
        ProfileService.Save(new SshProfile { Name = name, HostName = host, UserName = "tunneluser" });

    private static TunnelRoute SaveRoute(string name, int localPort, params SshProfile[] hops) =>
        RouteService.Save(new TunnelRoute
        {
            Name = name,
            HopProfileIds = hops.Select(hop => hop.Id).ToList(),
            LocalPort = localPort,
            TargetHost = "127.0.0.1",
            TargetPort = 4453,
        });

    [Fact]
    public void ProfilesAndRoutesSurviveARestart()
    {
        var gateway = SaveProfile("Profil 1");
        var server = SaveProfile("Profil 2", "10.0.0.24");
        var route = SaveRoute("Serverzugang", 4455, gateway, server);

        DataStore.Reset(); // as if the application had been restarted

        var loaded = RouteService.Get(route.Id);
        Assert.NotNull(loaded);
        Assert.Equal([gateway.Id, server.Id], loaded.HopProfileIds);
        Assert.Equal(["Profil 1", "Profil 2"], RouteService.GetHops(loaded).Select(hop => hop.Name));
        Assert.True(File.Exists(AppPaths.DataFile));
    }

    [Theory]
    [InlineData("", "host", "user", 22, nameof(SshProfile.Name))]
    [InlineData("A", "", "user", 22, nameof(SshProfile.HostName))]
    [InlineData("A", "host name", "user", 22, nameof(SshProfile.HostName))]
    [InlineData("A", "-oProxyCommand=calc", "user", 22, nameof(SshProfile.HostName))]
    [InlineData("A", "host", "us\"er", 22, nameof(SshProfile.UserName))]
    [InlineData("A", "host", "user", 0, nameof(SshProfile.Port))]
    [InlineData("A", "host", "user", 70000, nameof(SshProfile.Port))]
    public void InvalidProfilesAreRejectedAtTheirField(string name, string host, string user, int port, string field)
    {
        var exception = Assert.Throws<BusinessException>(() =>
            ProfileService.Save(new SshProfile { Name = name, HostName = host, UserName = user, Port = port }));

        Assert.Equal(field, exception.Field);
    }

    [Fact]
    public void KeyFileMustExist()
    {
        var exception = Assert.Throws<BusinessException>(() => ProfileService.Save(new SshProfile
        {
            Name = "A",
            HostName = "host",
            UserName = "user",
            AuthenticationMethod = SshAuthenticationMethod.KeyFile,
            KeyFilePath = Path.Combine(_data.Directory, "missing"),
        }));

        Assert.Equal(nameof(SshProfile.KeyFilePath), exception.Field);
    }

    [Fact]
    public void ProfileNamesAreUnique()
    {
        SaveProfile("Gateway");

        var exception = Assert.Throws<BusinessException>(() => SaveProfile("gateway"));
        Assert.Equal(nameof(SshProfile.Name), exception.Field);
    }

    [Fact]
    public void AProfileInUseCannotBeDeleted()
    {
        var gateway = SaveProfile("Gateway");
        SaveRoute("Serverzugang", 4455, gateway);

        var exception = Assert.Throws<BusinessException>(() => ProfileService.Delete(gateway.Id));
        Assert.Contains("Serverzugang", exception.Message);
    }

    [Fact]
    public void TwoRoutesCannotShareALocalPort()
    {
        var gateway = SaveProfile("Gateway");
        SaveRoute("Erste", 4455, gateway);

        var exception = Assert.Throws<BusinessException>(() => SaveRoute("Zweite", 4455, gateway));
        Assert.Equal(nameof(TunnelRoute.LocalPort), exception.Field);
    }

    [Fact]
    public void ARouteNeedsAtLeastOneHop()
    {
        var exception = Assert.Throws<BusinessException>(() => SaveRoute("Leer", 4455));
        Assert.Equal(nameof(TunnelRoute.HopProfileIds), exception.Field);
    }

    [Fact]
    public void ListSearchesFiltersAndSorts()
    {
        var gateway = SaveProfile("Gateway");
        var database = SaveProfile("Datenbank", "db.internal");
        SaveRoute("Staging", 8080, gateway);
        SaveRoute("Datenbank", 15432, gateway, database);
        var auto = SaveRoute("Serverzugang", 4455, gateway);
        auto.ConnectOnStartup = true;
        RouteService.Save(auto);

        Assert.Equal(["Datenbank", "Serverzugang", "Staging"], RouteService.List().Select(route => route.Name));
        Assert.Equal(["Serverzugang", "Staging", "Datenbank"],
            RouteService.List(new ListQuery(SortBy: RouteService.SortByLocalPort)).Select(route => route.Name));
        Assert.Equal(["Datenbank"], RouteService.List(new ListQuery("15432")).Select(route => route.Name));
        Assert.Equal(["Serverzugang"], RouteService.List(new ListQuery(Filter: RouteService.FilterAutoConnect)).Select(route => route.Name));
        Assert.Equal(["Datenbank"], ProfileService.List(new ListQuery(SearchText: "db.internal")).Select(profile => profile.Name));
    }

    [Fact]
    public void ALockedConfigurationRejectsEveryChange()
    {
        var gateway = SaveProfile("Gateway");
        AccessPolicy.UsePolicyReaderForTests(name => name == "LockConfiguration");

        Assert.Throws<BusinessException>(() => SaveProfile("Neu"));
        Assert.Throws<BusinessException>(() => ProfileService.Delete(gateway.Id));
        Assert.Throws<BusinessException>(() => SaveRoute("Route", 4455, gateway));
        Assert.Throws<BusinessException>(() => SettingsService.Save(new AppSettings(), "ThixTunnel.exe"));
    }

    [Fact]
    public void NetworkBindingCanBeRestrictedToLoopback()
    {
        var gateway = SaveProfile("Gateway");
        AccessPolicy.UsePolicyReaderForTests(name => name == "DenyNetworkBinding");

        var route = new TunnelRoute { Name = "Offen", HopProfileIds = [gateway.Id], LocalAddress = "0.0.0.0", LocalPort = 4455, TargetPort = 4453 };
        var exception = Assert.Throws<BusinessException>(() => RouteService.Save(route));
        Assert.Equal(nameof(TunnelRoute.LocalAddress), exception.Field);

        route.LocalAddress = "localhost";
        RouteService.Save(route);
    }

    [Fact]
    public void UpdatesAreOnlyOfferedForTheSameChannelAndAHigherBuild()
    {
        var manifest = new UpdateManifest("0.1.43-20261104", "release", 43, "ThixTunnelSetup.exe", "00", DateTimeOffset.Now);

        Assert.True(UpdateService.IsNewer(manifest, "release", 42));
        Assert.False(UpdateService.IsNewer(manifest, "release", 43));
        Assert.False(UpdateService.IsNewer(manifest, "nightly", 12));
    }
}
