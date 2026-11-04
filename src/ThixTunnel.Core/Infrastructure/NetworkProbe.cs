using System.Net;
using System.Net.NetworkInformation;

namespace ThixTunnel.Core.Infrastructure;

/// <summary>Looks at local TCP listeners: is a port free, and is our forwarding listening yet?</summary>
internal static class NetworkProbe
{
    /// <summary>True when something listens on the port of the given local address (or on all addresses).</summary>
    public static bool IsListening(string localAddress, int port)
    {
        var listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
        if (!TryParseLocalAddress(localAddress, out var address))
        {
            return listeners.Any(listener => listener.Port == port);
        }

        return listeners.Any(listener => listener.Port == port &&
            (listener.Address.Equals(address) || listener.Address.Equals(IPAddress.Any) || listener.Address.Equals(IPAddress.IPv6Any)));
    }

    private static bool TryParseLocalAddress(string localAddress, out IPAddress address)
    {
        if (string.Equals(localAddress, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            address = IPAddress.Loopback;
            return true;
        }

        return IPAddress.TryParse(localAddress.Trim('[', ']'), out address!);
    }
}
