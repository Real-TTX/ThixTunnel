using System.Net;
using Microsoft.Win32;
using ThixTunnel.Core.Models;

namespace ThixTunnel.Core.Business;

/// <summary>
/// The permission checks of the business layer. Every service calls them itself before it changes something,
/// whatever the UI shows or hides.
///
/// An administrator can restrict ThixTunnel with machine policies (HKLM\SOFTWARE\Policies\ThixTunnel, DWORD = 1):
///   LockConfiguration     profiles, routes and settings are read-only
///   DenyNetworkBinding    a tunnel may only listen on loopback addresses (127.0.0.1, ::1, localhost)
///   DisableUpdates        no update check, no update installation
/// </summary>
public static class AccessPolicy
{
    private const string PolicyKeyPath = @"SOFTWARE\Policies\ThixTunnel";

    private static Func<string, bool> _readPolicy = ReadMachinePolicy;

    public static bool IsConfigurationLocked => _readPolicy("LockConfiguration");

    public static bool IsNetworkBindingDenied => _readPolicy("DenyNetworkBinding");

    public static bool AreUpdatesDisabled => _readPolicy("DisableUpdates");

    public static void EnsureConfigurationEditable()
    {
        if (IsConfigurationLocked)
        {
            throw new BusinessException("Die Konfiguration wurde von einem Administrator gesperrt (Richtlinie LockConfiguration).");
        }
    }

    public static void EnsureBindAddressAllowed(string localAddress)
    {
        if (IsNetworkBindingDenied && !IsLoopback(localAddress))
        {
            throw new BusinessException("Nur Loopback-Adressen sind erlaubt (Richtlinie DenyNetworkBinding).", nameof(TunnelRoute.LocalAddress));
        }
    }

    public static void EnsureUpdatesAllowed()
    {
        if (AreUpdatesDisabled)
        {
            throw new BusinessException("Updates wurden von einem Administrator deaktiviert (Richtlinie DisableUpdates).");
        }
    }

    public static bool IsLoopback(string address)
    {
        if (string.Equals(address, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(address.Trim('[', ']'), out var parsed) && IPAddress.IsLoopback(parsed);
    }

    /// <summary>Tests replace the registry with their own answers.</summary>
    internal static void UsePolicyReaderForTests(Func<string, bool>? reader) => _readPolicy = reader ?? ReadMachinePolicy;

    private static bool ReadMachinePolicy(string name)
    {
        using var key = Registry.LocalMachine.OpenSubKey(PolicyKeyPath);
        return key?.GetValue(name) is int value && value == 1;
    }
}
