using ThixTunnel.Core.Models;

namespace ThixTunnel.Core.Infrastructure;

/// <summary>Builds the arguments of: ssh -F route.conf -N -L local:port:target:port -J hop1,hop2 hopN</summary>
internal static class SshCommandBuilder
{
    public static IReadOnlyList<string> BuildArguments(TunnelRoute route, int hopCount, string configFileName)
    {
        var arguments = new List<string>
        {
            "-F", configFileName,
            "-N",
            "-L", ForwardingSpecification(route),
        };

        if (hopCount > 1)
        {
            arguments.Add("-J");
            arguments.Add(string.Join(',', Enumerable.Range(0, hopCount - 1).Select(SshConfigWriter.HostAlias)));
        }

        arguments.Add(SshConfigWriter.HostAlias(hopCount - 1));
        return arguments;
    }

    /// <summary>The command a user could type, for the details view (with the real addresses instead of aliases).</summary>
    public static string DescribeCommand(TunnelRoute route, IReadOnlyList<SshProfile> hops)
    {
        var parts = new List<string> { "ssh", "-N" };
        if (hops.Count > 1)
        {
            parts.Add("-J");
            parts.Add(string.Join(',', hops.Take(hops.Count - 1).Select(hop => hop.DisplayAddress)));
        }

        parts.Add("-L");
        parts.Add(ForwardingSpecification(route));
        if (hops.Count > 0)
        {
            parts.Add(hops[^1].DisplayAddress);
        }

        return string.Join(' ', parts);
    }

    private static string ForwardingSpecification(TunnelRoute route) =>
        $"{FormatHost(route.LocalAddress)}:{route.LocalPort}:{FormatHost(route.TargetHost)}:{route.TargetPort}";

    /// <summary>IPv6 addresses need brackets in a forwarding specification.</summary>
    private static string FormatHost(string host) => host.Contains(':') && !host.StartsWith('[') ? $"[{host}]" : host;
}
