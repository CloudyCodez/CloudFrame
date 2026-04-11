using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FrameBoost.Services;

internal sealed class SystemTelemetryService : IDisposable
{
    private readonly object _gpuSync = new();
    private readonly Dictionary<string, PerformanceCounter> _gpuCounters = new(StringComparer.OrdinalIgnoreCase);
    private bool _cpuInitialized;
    private FileTime _prevIdle;
    private FileTime _prevKernel;
    private FileTime _prevUser;
    private DateTimeOffset _lastGpuDiscoveryAt = DateTimeOffset.MinValue;
    private DateTimeOffset _lastGpuSampleAt = DateTimeOffset.MinValue;
    private double? _lastGpuPercent;

    public double GetCpuPercent()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
        {
            return 0;
        }

        if (!_cpuInitialized)
        {
            _prevIdle = idle;
            _prevKernel = kernel;
            _prevUser = user;
            _cpuInitialized = true;
            return 0;
        }

        var idleDiff = ToUInt64(idle) - ToUInt64(_prevIdle);
        var kernelDiff = ToUInt64(kernel) - ToUInt64(_prevKernel);
        var userDiff = ToUInt64(user) - ToUInt64(_prevUser);
        var systemDiff = kernelDiff + userDiff;

        _prevIdle = idle;
        _prevKernel = kernel;
        _prevUser = user;

        if (systemDiff == 0)
        {
            return 0;
        }

        var busy = systemDiff - idleDiff;
        return Math.Clamp(busy * 100d / systemDiff, 0, 100);
    }

    public double? GetGpuPercent()
    {
        lock (_gpuSync)
        {
            var now = DateTimeOffset.Now;
            if (now - _lastGpuSampleAt < TimeSpan.FromMilliseconds(1200))
            {
                return _lastGpuPercent;
            }

            try
            {
                if (_gpuCounters.Count == 0 || now - _lastGpuDiscoveryAt >= TimeSpan.FromSeconds(10))
                {
                    RefreshGpuCounters();
                    _lastGpuDiscoveryAt = now;
                }

                if (_gpuCounters.Count == 0)
                {
                    _lastGpuPercent = null;
                    _lastGpuSampleAt = now;
                    return null;
                }

                var total = 0d;
                foreach (var counter in _gpuCounters.Values)
                {
                    total += counter.NextValue();
                }

                _lastGpuPercent = Math.Clamp(total, 0, 100);
                _lastGpuSampleAt = now;
                return _lastGpuPercent;
            }
            catch
            {
                return _lastGpuPercent;
            }
        }
    }

    public void Dispose()
    {
        foreach (var counter in _gpuCounters.Values)
        {
            counter.Dispose();
        }

        _gpuCounters.Clear();
    }

    private void RefreshGpuCounters()
    {
        var category = new PerformanceCounterCategory("GPU Engine");
        var instanceNames = category.GetInstanceNames()
            .Where(static name =>
                name.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase)
                || name.Contains("engtype_Compute", StringComparison.OrdinalIgnoreCase)
                || name.Contains("engtype_Copy", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var activeNames = new HashSet<string>(instanceNames, StringComparer.OrdinalIgnoreCase);
        foreach (var staleName in _gpuCounters.Keys.Where(name => !activeNames.Contains(name)).ToList())
        {
            _gpuCounters[staleName].Dispose();
            _gpuCounters.Remove(staleName);
        }

        foreach (var instanceName in instanceNames)
        {
            if (_gpuCounters.ContainsKey(instanceName))
            {
                continue;
            }

            var counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", instanceName);
            counter.NextValue();
            _gpuCounters[instanceName] = counter;
        }
    }

    private static ulong ToUInt64(FileTime time)
    {
        return ((ulong)time.dwHighDateTime << 32) | time.dwLowDateTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct FileTime
    {
        public readonly uint dwLowDateTime;
        public readonly uint dwHighDateTime;
    }
}
