using System.Diagnostics;
using System.Text;
using ThixTunnel.Core.Models;

namespace ThixTunnel.Core.Infrastructure;

/// <summary>One running "ssh -N -L ..." process of a route, with its error output.</summary>
internal sealed class SshProcess : IDisposable
{
    private const int MaximumErrorOutputLength = 8000;

    private readonly Process _process;
    private readonly StringBuilder _errorOutput = new();

    private SshProcess(Process process)
    {
        _process = process;
    }

    public string ErrorOutput
    {
        get
        {
            lock (_errorOutput)
            {
                return _errorOutput.ToString();
            }
        }
    }

    public bool HasExited => _process.HasExited;

    /// <param name="askPassProgram">Program for password and host key prompts (SSH_ASKPASS); null = no prompts.</param>
    public static SshProcess Start(string sshExecutable, TunnelRoute route, IReadOnlyList<SshProfile> hops, bool interactive, string? askPassProgram)
    {
        var configFileName = SshConfigWriter.Write(route.Id, hops, interactive && askPassProgram is not null);
        return Launch(sshExecutable, SshCommandBuilder.BuildArguments(route, hops.Count, configFileName), interactive ? askPassProgram : null, route.Name);
    }

    /// <summary>
    /// Runs one command on the last of <paramref name="hops"/> (the earlier ones are jump hosts), always with prompts.
    /// <paramref name="configId"/> names its own configuration file, so a running tunnel of the same route is not touched.
    /// </summary>
    public static SshProcess StartCommand(string sshExecutable, Guid configId, IReadOnlyList<SshProfile> hops, string remoteCommand,
        string askPassProgram, string promptTitle)
    {
        var configFileName = SshConfigWriter.Write(configId, hops, interactive: true);
        return Launch(sshExecutable, SshCommandBuilder.BuildCommandArguments(hops.Count, configFileName, remoteCommand), askPassProgram, promptTitle);
    }

    public int ExitCode => _process.ExitCode;

    private static SshProcess Launch(string sshExecutable, IReadOnlyList<string> arguments, string? askPassProgram, string promptTitle)
    {
        var startInfo = new ProcessStartInfo(sshExecutable)
        {
            WorkingDirectory = AppPaths.SshConfigDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (askPassProgram is not null)
        {
            startInfo.Environment["SSH_ASKPASS"] = askPassProgram;
            startInfo.Environment["SSH_ASKPASS_REQUIRE"] = "force";
            startInfo.Environment["DISPLAY"] = ":0";
            startInfo.Environment[AskPassProtocol.EnvironmentVariable] = promptTitle;
        }

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var sshProcess = new SshProcess(process);
        process.ErrorDataReceived += (_, line) => sshProcess.AppendErrorLine(line.Data);
        process.OutputDataReceived += (_, _) => { };

        process.Start();
        ProcessJob.Assign(process);
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();
        return sshProcess;
    }

    public Task WaitForExitAsync(CancellationToken cancellationToken) => _process.WaitForExitAsync(cancellationToken);

    public void Stop()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // already gone
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            Log.Warning($"Could not end ssh process: {exception.Message}");
        }
    }

    public void Dispose()
    {
        Stop();
        _process.Dispose();
    }

    private void AppendErrorLine(string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (_errorOutput)
        {
            _errorOutput.AppendLine(line);
            if (_errorOutput.Length > MaximumErrorOutputLength)
            {
                _errorOutput.Remove(0, _errorOutput.Length - MaximumErrorOutputLength);
            }
        }
    }
}
