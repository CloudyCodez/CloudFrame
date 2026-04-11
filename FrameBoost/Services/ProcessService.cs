using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

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
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

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

    private const uint ProcessPowerThrottlingCurrentVersion = 1;
    private const uint ProcessPowerThrottlingExecutionSpeed = 0x1;

    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);
}
