using ThixTunnel.Core.Business;

namespace ThixTunnel.Core.Infrastructure;

/// <summary>
/// For the snapshots of the app and the UI tests: while the scope lasts, no machine policy is read and the autostart entry
/// lives in memory - nothing of the machine (HKCU Run, LaunchAgent, HKLM policies) is read or written. The data folder must
/// already be a fresh one of its own (THIXTUNNEL_DATA_DIR or the tests' folder); the cached data is read again from it.
/// </summary>
public static class SandboxScope
{
    public static IDisposable Begin(bool autostartEnabled = true)
    {
        AccessPolicy.UsePolicyReaderForTests(_ => false);
        AutostartService.UseMemoryEntryForTests(autostartEnabled);
        DataStore.Reset();
        return new Scope();
    }

    private sealed class Scope : IDisposable
    {
        public void Dispose()
        {
            DataStore.Reset();
            AutostartService.UseMemoryEntryForTests(null);
            AccessPolicy.UsePolicyReaderForTests(null);
        }
    }
}
