namespace ThixTunnel.Core.Infrastructure;

/// <summary>
/// ssh asks for passwords and host key confirmations through SSH_ASKPASS. ThixTunnel.exe is that program:
/// started with this environment variable (value = route name) and the prompt as argument, it shows a dialog
/// and writes the answer to standard output - straight to ssh, never through the main process or a log.
/// </summary>
public static class AskPassProtocol
{
    public const string EnvironmentVariable = "THIXTUNNEL_ASKPASS_ROUTE";

    public static bool IsAskPassInvocation(out string routeName)
    {
        routeName = Environment.GetEnvironmentVariable(EnvironmentVariable) ?? "";
        return routeName.Length > 0;
    }

    /// <summary>ssh asks "Are you sure you want to continue connecting (yes/no/[fingerprint])?" for an unknown host key.</summary>
    public static bool IsConfirmation(string prompt) =>
        prompt.Contains("(yes/no", StringComparison.OrdinalIgnoreCase);
}
