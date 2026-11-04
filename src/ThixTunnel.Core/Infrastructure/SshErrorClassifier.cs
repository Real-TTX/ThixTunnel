using ThixTunnel.Core.Models;

namespace ThixTunnel.Core.Infrastructure;

/// <summary>Turns the error output of ssh into a problem the user understands, and decides whether a retry makes sense.</summary>
internal static class SshErrorClassifier
{
    private static readonly (string Pattern, TunnelProblem Problem)[] Rules =
    [
        ("REMOTE HOST IDENTIFICATION HAS CHANGED", TunnelProblem.HostKeyChanged),
        ("POSSIBLE DNS SPOOFING", TunnelProblem.HostKeyChanged),
        ("Host key verification failed", TunnelProblem.HostKeyUnknown),
        ("host key is known for", TunnelProblem.HostKeyUnknown),
        ("Permission denied", TunnelProblem.AuthenticationRequired),
        ("Too many authentication failures", TunnelProblem.AuthenticationRequired),
        ("no such identity", TunnelProblem.AuthenticationRequired),
        ("Address already in use", TunnelProblem.LocalPortInUse),
        ("cannot listen to port", TunnelProblem.LocalPortInUse),
        ("Could not request local forwarding", TunnelProblem.LocalPortInUse),
        ("administratively prohibited", TunnelProblem.ForwardingRefused),
        ("Could not resolve hostname", TunnelProblem.HostUnreachable),
        ("Connection timed out", TunnelProblem.HostUnreachable),
        ("Connection refused", TunnelProblem.HostUnreachable),
        ("Network is unreachable", TunnelProblem.HostUnreachable),
        ("No route to host", TunnelProblem.HostUnreachable),
        ("kex_exchange_identification", TunnelProblem.HostUnreachable),
        ("Connection closed by", TunnelProblem.ConnectionLost),
        ("Connection reset", TunnelProblem.ConnectionLost),
        ("not responding", TunnelProblem.ConnectionLost),
        ("Broken pipe", TunnelProblem.ConnectionLost),
        ("client_loop", TunnelProblem.ConnectionLost),
    ];

    public static TunnelProblem Classify(string errorOutput)
    {
        foreach (var (pattern, problem) in Rules)
        {
            if (errorOutput.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return problem;
            }
        }

        return TunnelProblem.Unknown;
    }

    /// <summary>Problems a later attempt can solve on its own. Authentication and host keys need the user.</summary>
    public static bool IsRetryable(TunnelProblem problem) =>
        problem is TunnelProblem.HostUnreachable or TunnelProblem.ConnectionLost or TunnelProblem.Unknown;

    public static string Describe(TunnelProblem problem) => problem switch
    {
        TunnelProblem.AuthenticationRequired => "Anmeldung fehlgeschlagen – Schlüssel, Agent oder Passwort prüfen",
        TunnelProblem.HostKeyUnknown => "Unbekannter Hostschlüssel – einmal manuell verbinden und bestätigen",
        TunnelProblem.HostKeyChanged => "Hostschlüssel geändert – Verbindung aus Sicherheitsgründen abgelehnt",
        TunnelProblem.HostUnreachable => "Server nicht erreichbar",
        TunnelProblem.LocalPortInUse => "Lokaler Port ist bereits belegt",
        TunnelProblem.ForwardingRefused => "Server erlaubt keine Portweiterleitung",
        TunnelProblem.SshNotFound => "ssh.exe nicht gefunden – OpenSSH-Client installieren",
        TunnelProblem.ConnectionLost => "Verbindung unterbrochen",
        TunnelProblem.Unknown => "Verbindung beendet",
        _ => "",
    };

    /// <summary>The last meaningful line of the output, for the details view.</summary>
    public static string LastLine(string errorOutput) =>
        errorOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault(line => !line.StartsWith("debug", StringComparison.Ordinal) && !line.StartsWith('@')) ?? "";
}
