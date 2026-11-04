using Microsoft.Win32;
using ThixTunnel.Core.Business;
using ThixTunnel.Core.Infrastructure;
using ThixTunnel.Core.Models;

namespace ThixTunnel.Tests;

[Collection(SharedStateCollection.Name)]
public sealed class TransferAndInstallTests : IDisposable
{
    private const string TestUninstallKey = @"Software\ThixTunnelTests\Uninstall";

    private readonly TestData _data = new();

    public void Dispose()
    {
        InstallService.UseLocationsForTests(null);
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\ThixTunnelTests", throwOnMissingSubKey: false);
        _data.Dispose();
    }

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

    /// <summary>Exports the current data and switches to a fresh, empty data folder (as if on another PC).</summary>
    private TestData ExportAndStartOver(out string file)
    {
        file = Path.Combine(_data.Directory, "export.thixtunnel.json");
        TransferService.Export(file);
        return new TestData(); // switches to its own empty data folder
    }

    [Fact]
    public void ExportAndImportMoveProfilesAndConnectionsToAnotherPc()
    {
        var gateway = SaveProfile("Gateway");
        var server = SaveProfile("Server", "10.0.0.24");
        SaveRoute("Serverzugang", 4455, gateway, server);

        using var otherPc = ExportAndStartOver(out var file);
        var result = TransferService.Import(file, replaceExisting: false);

        Assert.Equal((2, 1), (result.ProfilesAdded, result.RoutesAdded));
        Assert.Empty(result.Problems);
        var route = Assert.Single(RouteService.List());
        Assert.Equal(["Gateway", "Server"], RouteService.GetHops(route).Select(hop => hop.Name));
    }

    [Fact]
    public void ImportKeepsExistingEntriesUnlessReplacingIsChosen()
    {
        var gateway = SaveProfile("Gateway");
        SaveRoute("Serverzugang", 4455, gateway);
        var file = Path.Combine(_data.Directory, "export.thixtunnel.json");
        TransferService.Export(file);

        // Changed locally after the export.
        var changed = ProfileService.Get(gateway.Id)!;
        changed.HostName = "changed.example.com";
        ProfileService.Save(changed);

        var kept = TransferService.Import(file, replaceExisting: false);
        Assert.Equal((0, 1, 0, 1), (kept.ProfilesAdded, kept.ProfilesSkipped, kept.RoutesAdded, kept.RoutesSkipped));
        Assert.Equal("changed.example.com", ProfileService.Get(gateway.Id)!.HostName);

        var replaced = TransferService.Import(file, replaceExisting: true);
        Assert.Equal((1, 1), (replaced.ProfilesReplaced, replaced.RoutesReplaced));
        Assert.Equal("gateway.example.com", ProfileService.Get(gateway.Id)!.HostName);
        Assert.Single(ProfileService.List());
    }

    [Fact]
    public void ImportedConnectionsUseTheExistingProfileWithTheSameName()
    {
        var gateway = SaveProfile("Gateway");
        SaveRoute("Serverzugang", 4455, gateway);

        using var otherPc = ExportAndStartOver(out var file);
        var localGateway = SaveProfile("Gateway", "gateway.local"); // same name, other ID

        var result = TransferService.Import(file, replaceExisting: false);

        Assert.Equal(1, result.RoutesAdded);
        Assert.Equal([localGateway.Id], Assert.Single(RouteService.List()).HopProfileIds);
    }

    [Fact]
    public void ImportReportsConnectionsWhosePortIsTaken()
    {
        var gateway = SaveProfile("Gateway");
        SaveRoute("Serverzugang", 4455, gateway);

        using var otherPc = ExportAndStartOver(out var file);
        SaveRoute("Lokal", 4455, SaveProfile("Anderes"));

        var result = TransferService.Import(file, replaceExisting: false);

        Assert.Equal(1, result.RoutesSkipped);
        Assert.Contains(result.Problems, problem => problem.Contains("4455"));
    }

    [Fact]
    public void ImportRejectsOtherFiles()
    {
        var file = Path.Combine(_data.Directory, "other.json");
        File.WriteAllText(file, """{ "something": "else" }""");

        var exception = Assert.Throws<BusinessException>(() => TransferService.Import(file, replaceExisting: false));
        Assert.Contains("keine ThixTunnel-Exportdatei", exception.Message);
    }

    [Fact]
    public void ImportIsBlockedByTheLockPolicy()
    {
        SaveProfile("Gateway");
        var file = Path.Combine(_data.Directory, "export.thixtunnel.json");
        TransferService.Export(file);
        AccessPolicy.UsePolicyReaderForTests(name => name == "LockConfiguration");

        Assert.Throws<BusinessException>(() => TransferService.Import(file, replaceExisting: true));
    }

    [Fact]
    public void SetupInstallsUpdatesAndUninstallsWithoutTraces()
    {
        var programFolder = Path.Combine(_data.Directory, "Programs", "ThixTunnel");
        var shortcuts = Path.Combine(_data.Directory, "StartMenu");
        InstallService.UseLocationsForTests(programFolder, shortcuts, TestUninstallKey);
        var setup = Path.Combine(_data.Directory, "Downloads", "ThixTunnelSetup.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(setup)!);
        File.WriteAllText(setup, "version 1");

        InstallService.Install(setup, new InstallOptions(StartWithWindows: false, DesktopShortcut: true), _ => { });

        Assert.True(InstallService.IsInstalled);
        Assert.Equal("version 1", File.ReadAllText(InstallService.InstalledExecutable));
        Assert.True(File.Exists(InstallService.StartMenuShortcut));
        Assert.True(InstallService.HasDesktopShortcut);
        using (var key = Registry.CurrentUser.OpenSubKey(TestUninstallKey))
        {
            Assert.Equal($"\"{InstallService.InstalledExecutable}\" --uninstall", key?.GetValue("UninstallString"));
        }

        // Update: new program file, shortcuts stay.
        File.WriteAllText(setup, "version 2");
        InstallService.Install(setup, options: null, _ => { });
        Assert.Equal("version 2", File.ReadAllText(InstallService.InstalledExecutable));
        Assert.True(InstallService.HasDesktopShortcut);

        var removeLater = InstallService.Uninstall(deleteUserData: false, _ => { });

        Assert.False(removeLater);
        Assert.False(InstallService.IsInstalled);
        Assert.False(Directory.Exists(programFolder));
        Assert.False(File.Exists(InstallService.StartMenuShortcut));
        Assert.False(InstallService.HasDesktopShortcut);
        Assert.Null(Registry.CurrentUser.OpenSubKey(TestUninstallKey));
    }
}
