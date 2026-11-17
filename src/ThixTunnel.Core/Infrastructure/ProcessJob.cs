using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ThixTunnel.Core.Infrastructure;

/// <summary>
/// No managed tunnel may outlive ThixTunnel.
///   Windows: a job object with "kill on close". Every ssh process (and the jump-host ssh processes it starts) belongs
///            to it; when ThixTunnel ends - normally or by a crash - Windows closes the handle and ends all of them.
///   macOS/Linux: there is no such object. ThixTunnel ends its ssh processes when it exits; after a crash the next start
///            ends the ssh processes still running with the configuration file of one of its routes (EndLeftovers).
/// </summary>
internal static class ProcessJob
{
    private const int JobObjectExtendedLimitInformation = 9;
    private const uint JobObjectLimitKillOnJobClose = 0x2000;

    private static readonly Lazy<IntPtr> JobHandle = new(() => OperatingSystem.IsWindows() ? CreateJob() : IntPtr.Zero);

    public static void Assign(Process process)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        if (!AssignProcessToJobObject(JobHandle.Value, process.Handle))
        {
            // Not fatal: the tunnel works, only the clean-up after a crash is missing.
            Trace.TraceWarning($"AssignProcessToJobObject failed: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
        }
    }

    /// <summary>
    /// macOS/Linux: ends ssh processes that a crashed ThixTunnel left behind. They are recognised by the configuration
    /// file name on their command line ("-F route-&lt;id&gt;.conf"); the ids are those of this data folder only.
    /// </summary>
    public static void EndLeftovers(IEnumerable<string> configFileNames)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        foreach (var fileName in configFileNames)
        {
            try
            {
                var startInfo = new ProcessStartInfo("pkill") { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
                startInfo.ArgumentList.Add("-f");
                startInfo.ArgumentList.Add($"ssh .*-F {fileName}");
                using var pkill = Process.Start(startInfo)!;
                pkill.WaitForExit(5000);
                if (pkill.ExitCode == 0)
                {
                    Log.Info($"Ended a leftover ssh process ({fileName})");
                }
            }
            catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
            {
                Log.Warning($"Could not look for leftover ssh processes: {exception.Message}");
                return;
            }
        }
    }

    [SupportedOSPlatform("windows")]
    private static IntPtr CreateJob()
    {
        var handle = CreateJobObject(IntPtr.Zero, null);
        if (handle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        var information = new JobObjectExtendedLimitInformationData
        {
            BasicLimitInformation = new JobObjectBasicLimitInformation { LimitFlags = JobObjectLimitKillOnJobClose },
        };

        var length = Marshal.SizeOf<JobObjectExtendedLimitInformationData>();
        var pointer = Marshal.AllocHGlobal(length);
        try
        {
            Marshal.StructureToPtr(information, pointer, false);
            if (!SetInformationJobObject(handle, JobObjectExtendedLimitInformation, pointer, (uint)length))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }

        return handle;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr jobAttributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(IntPtr job, int infoClass, IntPtr info, uint length);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectExtendedLimitInformationData
    {
        public JobObjectBasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }
}
