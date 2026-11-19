using System.Runtime.Versioning;
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
        if (OperatingSystem.IsWindows())
        {
            InstallService.UseLocationsForTests(null);
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\ThixTunnelTests", throwOnMissingSubKey: false);
        }

        AutostartService.UseMemoryEntryForTests(null);
        InstallerOptions.UseConfigForTests(null);
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
    public void TheEmbeddedInstallerJsonIsWhatTheManifestPublishes()
    {
        var config = InstallerOptions.Config;

        Assert.NotNull(config.DesktopShortcut);
        Assert.NotNull(config.Autostart);
        Assert.NotNull(config.AutomaticUpdates);
        Assert.Empty(config.FileTypes); // the export ends in ".json": no file type of its own
        Assert.Equal<(bool?, bool?, bool?)>((false, true, true), (InstallerOptions.Defaults.DesktopShortcut, InstallerOptions.Defaults.Autostart, InstallerOptions.Defaults.AutomaticUpdates));
    }

    [Fact]
    public void TheCommandLineCarriesTheInstallOptionsAndRefusesWhatItDoesNotKnow()
    {
        var request = InstallerOptions.Parse(["--quiet", "--desktop-shortcut=yes", "--autostart=no", "--automatic-updates=ja", "--start"]);
        Assert.Equal(SetupAction.Install, request.Action);
        Assert.True(request.Quiet);
        Assert.True(request.Start);
        Assert.Equal<(bool?, bool?, bool?)>((true, false, true), (request.Choices.DesktopShortcut, request.Choices.Autostart, request.Choices.AutomaticUpdates));
        Assert.Null(request.Choices.FileTypes);
        Assert.Equal(["--desktop-shortcut=yes", "--autostart=no", "--automatic-updates=yes"], InstallerOptions.ToArguments(request.Choices));

        Assert.Equal(SetupAction.Update, InstallerOptions.Parse(["--update"]).Action);
        Assert.Equal(new InstallChoices(), InstallerOptions.Parse(["--update"]).Choices);
        Assert.Empty(InstallerOptions.Parse(["--configure", "--file-types=none"]).Choices.FileTypes!);
        Assert.Empty(InstallerOptions.Parse(["--quiet", "--file-types=default"]).Choices.FileTypes!);
        Assert.Equal(SetupAction.Configure, InstallerOptions.Parse(["--configure", "--autostart=yes"]).Action);
        Assert.Equal(SetupAction.Uninstall, InstallerOptions.Parse(["--uninstall", "--quiet"]).Action);

        // the start at sign-in ("--autostart" without a value) and the tray start stay what they were
        Assert.Equal(SetupAction.None, InstallerOptions.Parse(["--autostart"]).Action);
        Assert.Equal(SetupAction.None, InstallerOptions.Parse(["--tray"]).Action);

        Assert.Throws<BusinessException>(() => InstallerOptions.Parse(["--quiet", "--autostart=vielleicht"]));
        Assert.Throws<BusinessException>(() => InstallerOptions.Parse(["--quiet", "--file-types=.json"]));
        Assert.Throws<BusinessException>(() => InstallerOptions.Parse(["--quiet", "--color=blue"]));
        Assert.Throws<BusinessException>(() => InstallerOptions.Parse(["--tray", "--autostart=yes"]));
        Assert.Throws<BusinessException>(() => InstallerOptions.Parse(["--uninstall", "--desktop-shortcut=no"]));

        // an option the app does not offer is refused
        InstallerOptions.UseConfigForTests(InstallerOptions.Config with { DesktopShortcut = null });
        Assert.Throws<BusinessException>(() => InstallerOptions.Parse(["--quiet", "--desktop-shortcut=yes"]));
    }

    [WindowsFact]
    [SupportedOSPlatform("windows")]
    public void TheSetupAssistantAsksWhatInstallerJsonOffers()
    {
        var options = ThixTunnel.Setup.ThixTunnelSetup.Options().ToList();

        Assert.Equal(["autostart:True", "shortcut:False", "updates:True"], options.Select(option => $"{option.Key}:{option.IsOn}"));
        var choices = ThixTunnel.Setup.ThixTunnelSetup.Choices(new Dictionary<string, bool> { ["autostart"] = false, ["shortcut"] = true, ["updates"] = true });
        Assert.Equal(["--desktop-shortcut=yes", "--autostart=no", "--automatic-updates=yes", "--file-types=none"], InstallerOptions.ToArguments(choices));
    }

    [WindowsFact]
    [SupportedOSPlatform("windows")]
    public void SetupInstallsUpdatesAndUninstallsWithoutTraces()
    {
        var programFolder = Path.Combine(_data.Directory, "Programs", "ThixTunnel");
        var shortcuts = Path.Combine(_data.Directory, "StartMenu");
        InstallService.UseLocationsForTests(programFolder, shortcuts, TestUninstallKey);
        var setup = Path.Combine(_data.Directory, "Downloads", "ThixTunnelSetup.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(setup)!);
        File.WriteAllText(setup, "version 1");

        // the autostart entry lives in memory: a test never writes the HKCU Run value
        AutostartService.UseMemoryEntryForTests(false);

        // first install: what is not given comes from installer.json (automatic updates on)
        var steps = new List<string>();
        InstallService.Install(setup, new InstallChoices(DesktopShortcut: true, Autostart: false), steps.Add);

        Assert.True(InstallService.IsInstalled);
        Assert.Equal("version 1", File.ReadAllText(InstallService.InstalledExecutable));
        Assert.True(File.Exists(InstallService.StartMenuShortcut));
        Assert.True(InstallService.HasDesktopShortcut);
        Assert.False(AutostartService.IsEnabled());
        Assert.True(SettingsService.Get().InstallUpdatesAutomatically);
        Assert.Contains("Installation abgeschlossen", steps);
        using (var key = Registry.CurrentUser.OpenSubKey(TestUninstallKey))
        {
            Assert.Equal($"\"{InstallService.InstalledExecutable}\" --uninstall", key?.GetValue("UninstallString"));
        }

        // Update: new program file, shortcuts and choices stay.
        File.WriteAllText(setup, "version 2");
        steps.Clear();
        InstallService.Install(setup, null, steps.Add);
        Assert.Equal("version 2", File.ReadAllText(InstallService.InstalledExecutable));
        Assert.True(InstallService.HasDesktopShortcut);
        Assert.False(AutostartService.IsEnabled());
        Assert.True(SettingsService.Get().InstallUpdatesAutomatically);
        Assert.Contains("Update abgeschlossen", steps);

        // An update with options ("--update --desktop-shortcut=no --autostart=yes") changes just these.
        InstallService.Install(setup, InstallerOptions.Parse(["--update", "--desktop-shortcut=no", "--autostart=yes"]).Choices, _ => { });
        Assert.False(InstallService.HasDesktopShortcut);
        Assert.True(AutostartService.IsEnabled());
        Assert.True(SettingsService.Get().InstallUpdatesAutomatically);
        var current = InstallerOptions.Current();
        Assert.Equal<(bool?, bool?, bool?)>((false, true, true), (current.DesktopShortcut, current.Autostart, current.AutomaticUpdates));

        var removeLater = InstallService.Uninstall(deleteUserData: false, _ => { });

        Assert.False(removeLater);
        Assert.False(AutostartService.IsEnabled());
        Assert.False(InstallService.IsInstalled);
        Assert.False(Directory.Exists(programFolder));
        Assert.False(File.Exists(InstallService.StartMenuShortcut));
        Assert.False(InstallService.HasDesktopShortcut);
        Assert.Null(Registry.CurrentUser.OpenSubKey(TestUninstallKey));
    }
}
