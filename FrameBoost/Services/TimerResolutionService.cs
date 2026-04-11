using System.Runtime.InteropServices;

namespace FrameBoost.Services;

/// <summary>
/// Requests a 1ms system timer resolution via timeBeginPeriod for the
/// duration of a boost session. Tighter timer = less sleep granularity =
/// smoother frame pacing. All major FPS boosters (Process Lasso, Razer
/// Cortex, NVIDIA App) apply this during gaming.
/// </summary>
internal sealed class TimerResolutionService : IDisposable
{
    private const uint Resolution1Ms = 1;
    private readonly Logger _logger;
    private bool _active;

    public TimerResolutionService(Logger logger) => _logger = logger;

    public bool IsActive => _active;

    /// <summary>
    /// Requests 1ms timer resolution. Safe to call multiple times — only
    /// applies once and tracks the paired timeEndPeriod call automatically.
    /// </summary>
    public void SetHighResolution()
    {
        if (_active) return;

        var result = timeBeginPeriod(Resolution1Ms);
        if (result == 0)
        {
            _active = true;
            _logger.Log("Timer resolution set to 1 ms — tighter frame pacing enabled.");
        }
        else
        {
            _logger.Log($"Could not set timer resolution (winmm error {result}).");
        }
    }

    /// <summary>
    /// Releases the 1ms request and lets Windows revert to its default
    /// (typically 15.6 ms) or whatever other apps requested.
    /// </summary>
    public void RestoreResolution()
    {
        if (!_active) return;
        timeEndPeriod(Resolution1Ms);
        _active = false;
        _logger.Log("Timer resolution restored to system default.");
    }

    public void Dispose() => RestoreResolution();

    [DllImport("winmm.dll")]
    private static extern uint timeBeginPeriod(uint uPeriod);

    [DllImport("winmm.dll")]
    private static extern uint timeEndPeriod(uint uPeriod);
}
