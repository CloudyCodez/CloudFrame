using Microsoft.Win32;

namespace FrameBoost.Services;

/// <summary>
/// Applies and reverts Windows registry tweaks used by every major FPS booster
/// on the market. All originals are saved before modification and restored
/// exactly on session end — no permanent changes.
///
/// MMCSS tweaks (HKLM — need elevation):
///   NetworkThrottlingIndex → 0xFFFFFFFF  (disables network-burst throttling)
///   SystemResponsiveness   → 0           (gives game full MMCSS CPU quota; default 20%)
///
/// Game DVR tweaks (HKCU — no elevation needed):
///   GameDVR_Enabled        → 0           (removes hidden DX capture hook overhead)
///   AppCaptureEnabled      → 0
/// </summary>
internal sealed class WindowsBoostRegistryService
{
    private const string MmcssKey  = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
    private const string GameDvrKey = @"System\GameConfigStore";
    private const string CaptureKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR";

    private readonly Logger _logger;
    private bool _applied;

    // Saved originals — nullable means "key did not exist before we touched it"
    private int? _prevNetworkThrottlingIndex;
    private int? _prevSystemResponsiveness;
    private int? _prevGameDvrEnabled;
    private int? _prevAppCaptureEnabled;

    public WindowsBoostRegistryService(Logger logger) => _logger = logger;

    public bool IsApplied => _applied;

    /// <summary>
    /// Saves and overwrites the relevant registry values.
    /// Silently skips any key it cannot open (not elevated / key missing).
    /// </summary>
    public void ApplyTweaks(bool applyMmcss, bool disableGameDvr)
    {
        if (_applied) return;

        if (applyMmcss) ApplyMmcss();
        if (disableGameDvr) ApplyGameDvr();

        _applied = true;
    }

    /// <summary>
    /// Restores every value that was saved by <see cref="ApplyTweaks"/>.
    /// Safe to call even if ApplyTweaks was never called or was partially skipped.
    /// </summary>
    public void RestoreTweaks()
    {
        if (!_applied) return;

        RestoreMmcss();
        RestoreGameDvr();
        _applied = false;
    }

    // ── MMCSS ──────────────────────────────────────────────────────────────────

    private void ApplyMmcss()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(MmcssKey, writable: true);
            if (key is null)
            {
                _logger.Log("MMCSS key not accessible — skipping MMCSS tweaks (run as admin to enable).");
                return;
            }

            // NetworkThrottlingIndex — default 10; 0xFFFFFFFF disables throttle
            _prevNetworkThrottlingIndex = key.GetValue("NetworkThrottlingIndex") is int nti ? nti : 10;
            key.SetValue("NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF), RegistryValueKind.DWord);

            // SystemResponsiveness — default 20 (20% reserved for non-multimedia);
            // 0 gives the game its full MMCSS CPU allocation
            _prevSystemResponsiveness = key.GetValue("SystemResponsiveness") is int sr ? sr : 20;
            key.SetValue("SystemResponsiveness", 0, RegistryValueKind.DWord);

            _logger.Log("MMCSS tweaks applied — NetworkThrottlingIndex disabled, SystemResponsiveness → 0.");
        }
        catch (Exception ex)
        {
            _logger.Log($"MMCSS tweaks skipped: {ex.Message}");
        }
    }

    private void RestoreMmcss()
    {
        if (_prevNetworkThrottlingIndex is null && _prevSystemResponsiveness is null) return;

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(MmcssKey, writable: true);
            if (key is null) return;

            if (_prevNetworkThrottlingIndex.HasValue)
                key.SetValue("NetworkThrottlingIndex", _prevNetworkThrottlingIndex.Value, RegistryValueKind.DWord);
            if (_prevSystemResponsiveness.HasValue)
                key.SetValue("SystemResponsiveness", _prevSystemResponsiveness.Value, RegistryValueKind.DWord);

            _logger.Log("MMCSS registry values restored.");
        }
        catch (Exception ex)
        {
            _logger.Log($"Could not restore MMCSS values: {ex.Message}");
        }
        finally
        {
            _prevNetworkThrottlingIndex = null;
            _prevSystemResponsiveness   = null;
        }
    }

    // ── Game DVR ───────────────────────────────────────────────────────────────

    private void ApplyGameDvr()
    {
        try
        {
            using var dvrKey = Registry.CurrentUser.OpenSubKey(GameDvrKey, writable: true);
            if (dvrKey is not null)
            {
                _prevGameDvrEnabled = dvrKey.GetValue("GameDVR_Enabled") is int v ? v : 1;
                dvrKey.SetValue("GameDVR_Enabled", 0, RegistryValueKind.DWord);
            }

            using var capKey = Registry.CurrentUser.OpenSubKey(CaptureKey, writable: true);
            if (capKey is not null)
            {
                _prevAppCaptureEnabled = capKey.GetValue("AppCaptureEnabled") is int v2 ? v2 : 1;
                capKey.SetValue("AppCaptureEnabled", 0, RegistryValueKind.DWord);
            }

            _logger.Log("Game DVR / Xbox capture hook disabled for this session.");
        }
        catch (Exception ex)
        {
            _logger.Log($"Could not disable Game DVR: {ex.Message}");
        }
    }

    private void RestoreGameDvr()
    {
        try
        {
            using var dvrKey = Registry.CurrentUser.OpenSubKey(GameDvrKey, writable: true);
            if (dvrKey is not null && _prevGameDvrEnabled.HasValue)
                dvrKey.SetValue("GameDVR_Enabled", _prevGameDvrEnabled.Value, RegistryValueKind.DWord);

            using var capKey = Registry.CurrentUser.OpenSubKey(CaptureKey, writable: true);
            if (capKey is not null && _prevAppCaptureEnabled.HasValue)
                capKey.SetValue("AppCaptureEnabled", _prevAppCaptureEnabled.Value, RegistryValueKind.DWord);

            _logger.Log("Game DVR settings restored.");
        }
        catch { /* best effort */ }
        finally
        {
            _prevGameDvrEnabled    = null;
            _prevAppCaptureEnabled = null;
        }
    }
}
