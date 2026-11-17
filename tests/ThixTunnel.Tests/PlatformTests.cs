using ThixTunnel.Core.Business;
using ThixTunnel.Core.Infrastructure;

namespace ThixTunnel.Tests;

/// <summary>A test of something only Windows has (registry, setup); skipped elsewhere.</summary>
public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "Windows only";
        }
    }
}

/// <summary>A test of something only macOS has (LaunchAgent, /usr/bin/ssh); skipped elsewhere.</summary>
public sealed class MacFactAttribute : FactAttribute
{
    public MacFactAttribute()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Skip = "macOS only";
        }
    }
}

/// <summary>What differs between Windows and macOS: update packages, the Mac app bundle, autostart, ssh.</summary>
[Collection(SharedStateCollection.Name)]
public sealed class PlatformTests : IDisposable
{
    private readonly TestData _data = new();

    public void Dispose() => _data.Dispose();

    [Fact]
    public void EveryPlatformFindsItsOwnUpdatePackage()
    {
        var manifest = new UpdateManifest("0.2.50-20261105", "release", 50, "ThixTunnelSetup.exe", "aa", DateTimeOffset.Now, new()
        {
            ["win-x64"] = new UpdatePackage("ThixTunnelSetup.exe", "aa"),
            ["osx-arm64"] = new UpdatePackage("ThixTunnel-macOS-arm64.zip", "bb"),
            ["osx-x64"] = new UpdatePackage("ThixTunnel-macOS-x64.zip", "cc"),
        });

        Assert.Equal("ThixTunnelSetup.exe", UpdateService.PackageFor(manifest, "win-x64")?.File);
        Assert.Equal("bb", UpdateService.PackageFor(manifest, "osx-arm64")?.Sha256);
        Assert.Equal("ThixTunnel-macOS-x64.zip", UpdateService.PackageFor(manifest, "osx-x64")?.File);
        Assert.Null(UpdateService.PackageFor(manifest, "linux-x64"));
    }

    [Fact]
    public void AnOldManifestOnlyServesWindows()
    {
        var manifest = new UpdateManifest("0.1.5-20261104", "release", 5, "ThixTunnelSetup.exe", "aa", DateTimeOffset.Now);

        Assert.Equal(new UpdatePackage("ThixTunnelSetup.exe", "aa"), UpdateService.PackageFor(manifest, "win-x64"));
        Assert.Null(UpdateService.PackageFor(manifest, "osx-arm64"));
    }

    [Theory]
    [InlineData("ThixTunnelSetup.exe", true)]
    [InlineData("ThixTunnel-macOS-arm64.zip", true)]
    [InlineData("../ThixTunnel.zip", false)]
    [InlineData("updates/ThixTunnel.zip", false)]
    [InlineData("install.sh", false)]
    [InlineData("", false)]
    public void OnlyPlainPackageNamesAreDownloaded(string fileName, bool isSafe) =>
        Assert.Equal(isSafe, UpdateService.IsSafeFileName(fileName));

    [Fact]
    public void TheAppBundleIsFoundFromTheProgramInside()
    {
        var bundle = Path.Combine(_data.Directory, "Applications", "ThixTunnel.app");
        var executable = Path.Combine(bundle, "Contents", "MacOS", "ThixTunnel");

        Assert.Equal(bundle, MacAppBundle.BundleOf(executable));
        Assert.Null(MacAppBundle.BundleOf(Path.Combine(_data.Directory, "bin", "Debug", "ThixTunnel")));
        Assert.Null(MacAppBundle.BundleOf(null));
    }

    [Fact]
    public void TheReplacementScriptQuotesItsPathsAndKeepsTheOldAppOnFailure()
    {
        var script = MacAppBundle.ReplacementScript(4711, "/Applications/Thix Tunnel's.app", "/tmp/new/ThixTunnel.app", "/tmp/update.log");

        Assert.Contains("BUNDLE='/Applications/Thix Tunnel'\\''s.app'", script);
        Assert.Contains("while kill -0 4711", script);
        Assert.Contains("mv \"$BUNDLE.old\" \"$BUNDLE\"", script);
        Assert.Contains("open \"$BUNDLE\" --args --tray", script);
    }

    [Fact]
    public void TheLaunchAgentStartsThixTunnelInTheBackgroundAtSignIn()
    {
        var plist = AutostartService.LaunchAgent("/Applications/Thix & Tunnel.app/Contents/MacOS/ThixTunnel");

        Assert.Contains("<string>/Applications/Thix &amp; Tunnel.app/Contents/MacOS/ThixTunnel</string>", plist);
        Assert.Contains($"<string>{AutostartService.StartupArgument}</string>", plist);
        Assert.Contains("<key>RunAtLoad</key>", plist);
    }

    [MacFact]
    public void TheAutostartIsALaunchAgentOnTheMac()
    {
        Assert.False(AutostartService.IsEnabled());

        AutostartService.Enable("/Applications/ThixTunnel.app/Contents/MacOS/ThixTunnel");
        Assert.True(AutostartService.IsEnabled());
        Assert.StartsWith(_data.Directory, AutostartService.LaunchAgentFile);

        AutostartService.Disable();
        Assert.False(AutostartService.IsEnabled());
    }

    [MacFact]
    public void TheMacUsesTheSystemOpenSsh()
    {
        Assert.Equal("/usr/bin/ssh", SshExecutable.Find(""));
        Assert.Equal("/usr/bin/ssh-keygen", SshExecutable.Sibling("/usr/bin/ssh", "ssh-keygen"));
    }

    [WindowsFact]
    public void WindowsToolsHaveTheirExtension() =>
        Assert.EndsWith("ssh-keygen.exe", SshExecutable.Sibling(@"C:\Windows\System32\OpenSSH\ssh.exe", "ssh-keygen"));
}
