using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FrameBoost.Models;

namespace FrameBoost.Services;

internal sealed class ProcessService
{
    private static readonly HashSet<string> ProtectedProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "System",
        "Idle",
        "Registry",
        "csrss",
        "wininit",
        "winlogon",
        "services",
        "lsass",
        "smss",
        "svchost",
        "dwm",
        "audiodg",
        "explorer",
        "SearchIndexer",
        "MsMpEng",
        "SecurityHealthService",
        "vmcompute",
        "vmms",
        "vmwp",
        "vmmem",
        "vmmemWSL",
        "VBoxSVC",
        "VBoxHeadless",
        "VirtualBoxVM",
        "vmware-authd",
        "vmware-hostd",
        "vmware-vmx",
        "vmnat",
        "vmnetdhcp",
        "prl_cc",
        "prl_tools",
        "qemu-system-x86_64"
    };

    public bool TryGetExecutablePath(Process process, out string? path)
    {
        try
        {
            path = process.MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(path))
            {
                return true;
            }
        }
        catch (Win32Exception)
        {
        }
        catch (InvalidOperationException)
        {
        }

        return TryQueryImageName(process, out path);
    }

    public bool IsProtectedProcess(Process process)
    {
        return ProtectedProcesses.Contains(process.ProcessName);
    }

    public ResolvedGameProcess? ResolveGameProcess(GameProfile profile, int anchorProcessId, int? currentGameProcessId = null)
    {
        return ResolveGameProcessCore(profile, anchorProcessId, currentGameProcessId);
    }

    public string? GetEngineHint(GameProfile profile, string? executablePath, string? processName = null)
    {
        return TryInferEngineHint(profile, executablePath, processName);
    }

    public ResolvedGameProcess? WaitForResolvedGameProcess(
        GameProfile profile,
        int anchorProcessId,
        int? currentGameProcessId,
        TimeSpan timeout,
        TimeSpan pollInterval)
    {
        var deadline = DateTime.UtcNow + timeout;
        ResolvedGameProcess? latestCandidate = null;

        do
        {
            latestCandidate = ResolveGameProcessCore(profile, anchorProcessId, currentGameProcessId);
            if (latestCandidate is not null && latestCandidate.ProcessId != anchorProcessId)
            {
                return latestCandidate;
            }

            if (DateTime.UtcNow >= deadline)
            {
                return latestCandidate;
            }

            Thread.Sleep(pollInterval);
        }
        while (true);
    }

    public bool TryTrimWorkingSet(Process process, out string? error)
    {
        error = null;

        try
        {
            if (process.HasExited)
            {
                error = "Process already exited.";
                return false;
            }

            var handle = OpenProcess(ProcessAccessRights.QueryLimitedInformation | ProcessAccessRights.SetQuota, false, process.Id);
            if (handle == IntPtr.Zero)
            {
                error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                return false;
            }

            try
            {
                if (!EmptyWorkingSet(handle))
                {
                    error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                    return false;
                }

                return true;
            }
            finally
            {
                CloseHandle(handle);
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TryCloseGracefully(Process process, TimeSpan timeout, out string? error)
    {
        error = null;

        try
        {
            if (process.HasExited)
            {
                error = "Process already exited.";
                return false;
            }

            if (process.MainWindowHandle == IntPtr.Zero)
            {
                error = "No main window is available for a graceful close.";
                return false;
            }

            if (!process.CloseMainWindow())
            {
                error = "Windows did not accept the close request.";
                return false;
            }

            process.WaitForExit((int)Math.Max(500, timeout.TotalMilliseconds));
            if (!process.HasExited)
            {
                error = "The app stayed open after a graceful close request.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TryGetMemoryPriority(Process process, out uint priority, out string? error)
    {
        var success = TryReadProcessInformation(
            process,
            ProcessInformationClass.ProcessMemoryPriority,
            ProcessAccessRights.QueryLimitedInformation | ProcessAccessRights.QueryInformation,
            out MemoryPriorityInformation info,
            out error);

        priority = success ? info.MemoryPriority : 0;
        return success;
    }

    public bool TrySetMemoryPriority(Process process, uint priority, out string? error)
    {
        return TryWriteProcessInformation(
            process,
            ProcessInformationClass.ProcessMemoryPriority,
            new MemoryPriorityInformation { MemoryPriority = priority },
            ProcessAccessRights.SetInformation | ProcessAccessRights.QueryLimitedInformation,
            out error);
    }

    public bool TryGetPowerThrottling(Process process, out ProcessPowerThrottlingState state, out string? error)
    {
        return TryReadProcessInformation(
            process,
            ProcessInformationClass.ProcessPowerThrottling,
            ProcessAccessRights.QueryLimitedInformation | ProcessAccessRights.QueryInformation,
            out state,
            out error);
    }

    public bool TrySetExecutionSpeedThrottling(Process process, bool enabled, out string? error)
    {
        return TryWriteProcessInformation(
            process,
            ProcessInformationClass.ProcessPowerThrottling,
            new ProcessPowerThrottlingState
            {
                Version = ProcessPowerThrottlingCurrentVersion,
                ControlMask = ProcessPowerThrottlingExecutionSpeed,
                StateMask = enabled ? ProcessPowerThrottlingExecutionSpeed : 0
            },
            ProcessAccessRights.SetInformation | ProcessAccessRights.QueryLimitedInformation,
            out error);
    }

    public bool TryGetWorkingSetBytes(Process process, out long workingSetBytes, out string? error)
    {
        workingSetBytes = 0;
        error = null;

        try
        {
            if (process.HasExited)
            {
                error = "Process already exited.";
                return false;
            }

            workingSetBytes = process.WorkingSet64;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool IsProcessAlive(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private ResolvedGameProcess? ResolveGameProcessCore(GameProfile profile, int anchorProcessId, int? currentGameProcessId)
    {
        var candidateProcessIds = new HashSet<int>();
        if (anchorProcessId > 0)
        {
            candidateProcessIds.Add(anchorProcessId);
        }

        if (currentGameProcessId is > 0)
        {
            candidateProcessIds.Add(currentGameProcessId.Value);
        }

        foreach (var descendant in EnumerateDescendants(anchorProcessId))
        {
            candidateProcessIds.Add(descendant.ProcessId);
        }

        var candidates = new List<ResolvedGameProcess>();
        foreach (var processId in candidateProcessIds)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                if (process.HasExited || IsProtectedProcess(process))
                {
                    continue;
                }

                TryGetExecutablePath(process, out var executablePath);
                candidates.Add(new ResolvedGameProcess
                {
                    ProcessId = process.Id,
                    ProcessName = process.ProcessName,
                    ExecutablePath = executablePath,
                    EngineHint = TryInferEngineHint(profile, executablePath, process.ProcessName),
                    IsDescendant = process.Id != anchorProcessId,
                    HasVisibleWindow = process.MainWindowHandle != IntPtr.Zero || !string.IsNullOrWhiteSpace(process.MainWindowTitle),
                    Score = ScoreGameProcessCandidate(profile, process, executablePath, anchorProcessId, currentGameProcessId)
                });
            }
            catch
            {
            }
        }

        return candidates
            .OrderByDescending(static candidate => candidate.Score)
            .ThenByDescending(static candidate => candidate.HasVisibleWindow)
            .ThenByDescending(static candidate => candidate.IsDescendant)
            .FirstOrDefault(static candidate => candidate.Score > 0);
    }

    private static int ScoreGameProcessCandidate(
        GameProfile profile,
        Process process,
        string? executablePath,
        int anchorProcessId,
        int? currentGameProcessId)
    {
        var score = 0;
        var executableName = profile.ExecutableName;
        var pathMatches = !string.IsNullOrWhiteSpace(executablePath)
            && !string.IsNullOrWhiteSpace(profile.ExecutablePath)
            && string.Equals(executablePath, profile.ExecutablePath, StringComparison.OrdinalIgnoreCase);
        var fileNameMatches = !string.IsNullOrWhiteSpace(executablePath)
            && !string.IsNullOrWhiteSpace(executableName)
            && string.Equals(Path.GetFileNameWithoutExtension(executablePath), executableName, StringComparison.OrdinalIgnoreCase);
        var processNameMatches = !string.IsNullOrWhiteSpace(executableName)
            && string.Equals(process.ProcessName, executableName, StringComparison.OrdinalIgnoreCase);

        if (pathMatches)
        {
            score += 1000;
        }

        if (fileNameMatches)
        {
            score += 450;
        }

        if (processNameMatches)
        {
            score += 350;
        }

        if (process.Id == currentGameProcessId)
        {
            score += 140;
        }

        if (process.Id != anchorProcessId)
        {
            score += 85;
        }

        if (process.MainWindowHandle != IntPtr.Zero || !string.IsNullOrWhiteSpace(process.MainWindowTitle))
        {
            score += 130;
        }

        var engineHint = TryInferEngineHint(profile, executablePath, process.ProcessName);
        if (string.Equals(engineHint, "Unreal Engine", StringComparison.Ordinal))
        {
            if (process.ProcessName.Contains("Shipping", StringComparison.OrdinalIgnoreCase))
            {
                score += 260;
            }

            if (!string.IsNullOrWhiteSpace(executablePath) &&
                executablePath.Contains("\\Binaries\\Win64\\", StringComparison.OrdinalIgnoreCase))
            {
                score += 180;
            }
        }

        if (string.Equals(engineHint, "Unity", StringComparison.Ordinal))
        {
            score += 120;
        }

        return score;
    }

    private static string? TryInferEngineHint(GameProfile profile, string? executablePath, string? processName)
    {
        var effectivePath = string.IsNullOrWhiteSpace(executablePath) ? profile.ExecutablePath : executablePath;
        var effectiveName = string.IsNullOrWhiteSpace(processName)
            ? Path.GetFileNameWithoutExtension(effectivePath)
            : processName;

        if (!string.IsNullOrWhiteSpace(effectiveName) &&
            effectiveName.Contains("Shipping", StringComparison.OrdinalIgnoreCase))
        {
            return "Unreal Engine";
        }

        if (!string.IsNullOrWhiteSpace(effectivePath))
        {
            var directory = Path.GetDirectoryName(effectivePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                if (effectivePath.Contains("\\Binaries\\Win64\\", StringComparison.OrdinalIgnoreCase))
                {
                    return "Unreal Engine";
                }

                var baseName = Path.GetFileNameWithoutExtension(effectivePath);
                if (!string.IsNullOrWhiteSpace(baseName))
                {
                    var dataFolder = Path.Combine(directory, $"{baseName}_Data");
                    if (Directory.Exists(dataFolder))
                    {
                        return "Unity";
                    }
                }
            }
        }

        return null;
    }

    private static IEnumerable<ProcessTreeEntry> EnumerateDescendants(int rootProcessId)
    {
        if (rootProcessId <= 0)
        {
            yield break;
        }

        var snapshot = CreateToolhelp32Snapshot(Th32csSnapprocess, 0);
        if (snapshot == InvalidHandleValue)
        {
            yield break;
        }

        try
        {
            var entry = new ProcessEntry32
            {
                DwSize = (uint)Marshal.SizeOf<ProcessEntry32>()
            };

            var entries = new List<ProcessTreeEntry>();
            if (Process32First(snapshot, ref entry))
            {
                do
                {
                    entries.Add(new ProcessTreeEntry((int)entry.Th32ProcessId, (int)entry.Th32ParentProcessID));
                    entry.DwSize = (uint)Marshal.SizeOf<ProcessEntry32>();
                }
                while (Process32Next(snapshot, ref entry));
            }

            var childrenByParent = entries
                .GroupBy(static candidate => candidate.ParentProcessId)
                .ToDictionary(static group => group.Key, static group => group.ToList());

            var queue = new Queue<int>();
            var visited = new HashSet<int>();
            queue.Enqueue(rootProcessId);

            while (queue.Count > 0)
            {
                var parentProcessId = queue.Dequeue();
                if (!visited.Add(parentProcessId))
                {
                    continue;
                }

                if (!childrenByParent.TryGetValue(parentProcessId, out var children))
                {
                    continue;
                }

                foreach (var child in children)
                {
                    yield return child;
                    queue.Enqueue(child.ProcessId);
                }
            }
        }
        finally
        {
            CloseHandle(snapshot);
        }
    }

    private readonly record struct ProcessTreeEntry(int ProcessId, int ParentProcessId);

    internal sealed class ResolvedGameProcess
    {
        public required int ProcessId { get; init; }

        public required string ProcessName { get; init; }

        public string? ExecutablePath { get; init; }

        public string? EngineHint { get; init; }

        public bool IsDescendant { get; init; }

        public bool HasVisibleWindow { get; init; }

        public int Score { get; init; }
    }

    private static bool TryQueryImageName(Process process, out string? path)
    {
        path = null;

        try
        {
            var handle = OpenProcess(ProcessAccessRights.QueryLimitedInformation, false, process.Id);
            if (handle == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                var buffer = new StringBuilder(1024);
                var size = buffer.Capacity;

                if (!QueryFullProcessImageName(handle, 0, buffer, ref size))
                {
                    return false;
                }

                path = buffer.ToString();
                return !string.IsNullOrWhiteSpace(path);
            }
            finally
            {
                CloseHandle(handle);
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadProcessInformation<T>(
        Process process,
        ProcessInformationClass informationClass,
        ProcessAccessRights access,
        out T value,
        out string? error) where T : struct
    {
        value = default;
        error = null;

        try
        {
            if (process.HasExited)
            {
                error = "Process already exited.";
                return false;
            }

            var handle = OpenProcess(access, false, process.Id);
            if (handle == IntPtr.Zero)
            {
                error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                return false;
            }

            try
            {
                var size = Marshal.SizeOf<T>();
                var buffer = Marshal.AllocHGlobal(size);
                try
                {
                    if (!GetProcessInformation(handle, informationClass, buffer, (uint)size))
                    {
                        error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                        return false;
                    }

                    value = Marshal.PtrToStructure<T>(buffer);
                    return true;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            finally
            {
                CloseHandle(handle);
            }
        }
        catch (EntryPointNotFoundException)
        {
            error = "This Windows build does not expose the requested process tuning API.";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryWriteProcessInformation<T>(
        Process process,
        ProcessInformationClass informationClass,
        T value,
        ProcessAccessRights access,
        out string? error) where T : struct
    {
        error = null;

        try
        {
            if (process.HasExited)
            {
                error = "Process already exited.";
                return false;
            }

            var handle = OpenProcess(access, false, process.Id);
            if (handle == IntPtr.Zero)
            {
                error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                return false;
            }

            try
            {
                var size = Marshal.SizeOf<T>();
                var buffer = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.StructureToPtr(value, buffer, false);
                    if (!SetProcessInformation(handle, informationClass, buffer, (uint)size))
                    {
                        error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                        return false;
                    }

                    return true;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            finally
            {
                CloseHandle(handle);
            }
        }
        catch (EntryPointNotFoundException)
        {
            error = "This Windows build does not expose the requested process tuning API.";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(ProcessAccessRights access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessInformation(
        IntPtr hProcess,
        ProcessInformationClass processInformationClass,
        IntPtr processInformation,
        uint processInformationSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessInformation(
        IntPtr hProcess,
        ProcessInformationClass processInformationClass,
        IntPtr processInformation,
        uint processInformationSize);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(
        IntPtr hProcess,
        int dwFlags,
        StringBuilder lpExeName,
        ref int lpdwSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32First(IntPtr hSnapshot, ref ProcessEntry32 lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32Next(IntPtr hSnapshot, ref ProcessEntry32 lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    private static readonly IntPtr InvalidHandleValue = new(-1);
    private const uint Th32csSnapprocess = 0x00000002;

    [Flags]
    private enum ProcessAccessRights : uint
    {
        SetInformation = 0x0200,
        QueryInformation = 0x0400,
        QueryLimitedInformation = 0x1000,
        SetQuota = 0x0100
    }

    private enum ProcessInformationClass
    {
        ProcessMemoryPriority = 0,
        ProcessPowerThrottling = 4
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MemoryPriorityInformation
    {
        public uint MemoryPriority;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ProcessPowerThrottlingState
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct ProcessEntry32
    {
        public uint DwSize;
        public uint CntUsage;
        public uint Th32ProcessId;
        public IntPtr Th32DefaultHeapId;
        public uint Th32ModuleId;
        public uint CntThreads;
        public uint Th32ParentProcessID;
        public int PcPriClassBase;
        public uint DwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string SzExeFile;
    }

    private const uint ProcessPowerThrottlingCurrentVersion = 1;
    private const uint ProcessPowerThrottlingExecutionSpeed = 0x1;

    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);
}
