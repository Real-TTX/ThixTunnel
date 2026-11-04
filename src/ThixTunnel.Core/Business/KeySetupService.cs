using System.Diagnostics;
using System.Text.RegularExpressions;
using ThixTunnel.Core.Infrastructure;
using ThixTunnel.Core.Models;

namespace ThixTunnel.Core.Business;

/// <summary>Outcome of setting up the keys of a route: the hops to connect with, or why it failed.</summary>
internal sealed record KeySetupResult(IReadOnlyList<SshProfile> Hops, TunnelProblem Problem = TunnelProblem.None, string Message = "")
{
    public bool Succeeded => Problem == TunnelProblem.None;
}

/// <summary>
/// "Beim ersten Anmelden Schlüssel anlegen": a password profile gets a key of its own at the next manual connect.
///  1. ssh-keygen creates an ed25519 key without passphrase in %AppData%\ThixTunnel\keys (one per profile).
///  2. One ssh session to that hop - through the hops before it, like the tunnel - appends the public key to
///     ~/.ssh/authorized_keys. That is the one time the password is asked for.
///  3. The profile switches to the key file; from then on it connects without a password, also automatically.
/// Hops are handled in order, so a jump host that was just set up already carries the next one without a password.
/// The password is never stored. Only Linux/Unix servers (POSIX shell) are supported as targets of the setup.
/// </summary>
public static partial class KeySetupService
{
    private static readonly TimeSpan SetupTimeout = TimeSpan.FromMinutes(5);

    public static string KeyDirectory => Path.Combine(AppPaths.DataDirectory, "keys");

    public static string KeyFileFor(Guid profileId) => Path.Combine(KeyDirectory, $"profile-{profileId:N}_ed25519");

    /// <summary>True when the profile waits for its key to be set up at the next manual connect.</summary>
    public static bool NeedsSetup(SshProfile profile) =>
        profile is { AuthenticationMethod: SshAuthenticationMethod.Password, SetUpKeyOnFirstLogin: true } && !AccessPolicy.IsConfigurationLocked;

    /// <summary>The command that adds <paramref name="publicKey"/> once to ~/.ssh/authorized_keys (POSIX shell).</summary>
    internal static string AuthorizeCommand(string publicKey)
    {
        if (!PublicKeyPattern().IsMatch(publicKey))
        {
            throw new BusinessException("Der erzeugte öffentliche Schlüssel hat ein unerwartetes Format.");
        }

        const string file = "~/.ssh/authorized_keys";
        return "umask 077 && mkdir -p ~/.ssh && touch " + file +
               $" && {{ [ ! -s {file} ] || [ -z \"$(tail -c 1 {file})\" ] || echo >> {file}; }}" +
               $" && {{ grep -qxF '{publicKey}' {file} || echo '{publicKey}' >> {file}; }}";
    }

    /// <summary>Sets up every hop of the route that asks for it; <paramref name="track"/> receives the running ssh process (for "Trennen").</summary>
    internal static async Task<KeySetupResult> SetUpAsync(string sshExecutable, string askPassProgram, TunnelRoute route, IReadOnlyList<SshProfile> hops,
        Action<string> report, Action<SshProcess?> track, CancellationToken token)
    {
        var current = hops.ToList();
        for (var index = 0; index < current.Count; index++)
        {
            var profile = current[index];
            if (!NeedsSetup(profile))
            {
                continue;
            }

            report($"Schlüssel wird auf „{profile.Name}“ hinterlegt – bitte einmal das Passwort eingeben …");
            Log.Info($"Route '{route.Name}': setting up a key for profile '{profile.Name}'");

            string publicKey;
            try
            {
                publicKey = CreateKey(sshExecutable, profile);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or BusinessException)
            {
                Log.Error($"Key for profile '{profile.Name}' could not be created", exception);
                return new KeySetupResult(current, TunnelProblem.Unknown, $"Schlüssel für „{profile.Name}“ konnte nicht erzeugt werden: {exception.Message}");
            }

            var failure = await AuthorizeAsync(sshExecutable, askPassProgram, route, current.Take(index + 1).ToList(), publicKey, track, token);
            if (failure is not null || token.IsCancellationRequested)
            {
                return new KeySetupResult(current, failure?.Problem ?? TunnelProblem.None, failure?.Message ?? "Getrennt");
            }

            var updated = profile.Clone();
            updated.AuthenticationMethod = SshAuthenticationMethod.KeyFile;
            updated.KeyFilePath = KeyFileFor(profile.Id);
            updated.SetUpKeyOnFirstLogin = false;
            current[index] = ProfileService.Save(updated);
            Log.Info($"Profile '{profile.Name}' now uses its key {updated.KeyFilePath}");
        }

        return new KeySetupResult(current);
    }

    /// <summary>Creates the key pair of the profile (or reuses it after an earlier, interrupted setup) and returns the public key.</summary>
    private static string CreateKey(string sshExecutable, SshProfile profile)
    {
        var privateKey = KeyFileFor(profile.Id);
        var publicKeyFile = privateKey + ".pub";
        if (!File.Exists(privateKey) || !File.Exists(publicKeyFile))
        {
            Directory.CreateDirectory(KeyDirectory);
            File.Delete(privateKey);
            File.Delete(publicKeyFile);

            var keygen = Path.Combine(Path.GetDirectoryName(sshExecutable) ?? "", "ssh-keygen.exe");
            if (!File.Exists(keygen))
            {
                throw new BusinessException("ssh-keygen.exe wurde neben ssh.exe nicht gefunden.");
            }

            var comment = SafeCommentPattern().Replace($"thixtunnel-{Environment.MachineName}-{profile.Name}", "-");
            var startInfo = new ProcessStartInfo(keygen) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
            foreach (var argument in new[] { "-q", "-t", "ed25519", "-N", "", "-C", comment, "-f", privateKey })
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo)!;
            var errors = process.StandardError.ReadToEnd();
            process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0 || !File.Exists(publicKeyFile))
            {
                throw new BusinessException($"ssh-keygen ist fehlgeschlagen: {errors.Trim()}");
            }
        }

        return File.ReadAllText(publicKeyFile).Trim();
    }

    /// <summary>One ssh session to the last of <paramref name="hops"/>; null = the key is on the server.</summary>
    private static async Task<KeySetupResult?> AuthorizeAsync(string sshExecutable, string askPassProgram, TunnelRoute route, IReadOnlyList<SshProfile> hops,
        string publicKey, Action<SshProcess?> track, CancellationToken token)
    {
        var target = hops[^1];
        var configId = Guid.NewGuid();
        using var process = SshProcess.StartCommand(sshExecutable, configId, hops, AuthorizeCommand(publicKey), askPassProgram, $"{route.Name} – {target.Name}");
        track(process);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(SetupTimeout);
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Stop();
            return token.IsCancellationRequested
                ? null
                : new KeySetupResult(hops, TunnelProblem.Unknown, $"Schlüssel für „{target.Name}“: keine Anmeldung innerhalb von 5 Minuten.");
        }
        finally
        {
            track(null);
            SshConfigWriter.Delete(configId);
        }

        if (process.ExitCode == 0)
        {
            return null;
        }

        var errorOutput = process.ErrorOutput;
        var detail = SshErrorClassifier.LastLine(errorOutput);
        Log.Warning($"Key setup on '{target.Name}' failed with exit code {process.ExitCode}: {detail}");

        // 255 = ssh itself failed (login, host key, network); anything else = the command on the server failed.
        if (process.ExitCode == 255)
        {
            var problem = SshErrorClassifier.Classify(errorOutput);
            return new KeySetupResult(hops, problem, $"Schlüssel für „{target.Name}“: {SshErrorClassifier.Describe(problem)}");
        }

        return new KeySetupResult(hops, TunnelProblem.Unknown,
            $"Der Schlüssel konnte auf „{target.Name}“ nicht eingetragen werden (nur Linux-/Unix-Server werden unterstützt){(detail.Length > 0 ? ": " + detail : "")}");
    }

    [GeneratedRegex(@"^ssh-ed25519 [A-Za-z0-9+/=]+ [A-Za-z0-9._@-]+$")]
    private static partial Regex PublicKeyPattern();

    [GeneratedRegex(@"[^A-Za-z0-9._-]+")]
    private static partial Regex SafeCommentPattern();
}
