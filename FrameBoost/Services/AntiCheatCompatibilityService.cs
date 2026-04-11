using System.Diagnostics;
using FrameBoost.Models;

namespace FrameBoost.Services;

internal sealed class AntiCheatCompatibilityService
{
    private static readonly VendorSignature[] Signatures =
    [
        new(
            "Easy Anti-Cheat",
            ["EasyAntiCheat", "EasyAntiCheat_EOS", "start_protected_game"],
            ["EasyAntiCheat", "EasyAntiCheat_EOS_Setup.exe", "start_protected_game.exe"],
            []),
        new(
            "BattlEye",
            ["BEService", "BEService_x64", "BELauncher"],
            ["BattlEye", "BEService.exe", "BELauncher.exe"],
            []),
        new(
            "Riot Vanguard",
            ["vgc", "vgtray"],
            ["VALORANT", "Riot Client"],
            ["VALORANT", "Riot Games"]),
        new(
            "FACEIT Anti-Cheat",
            ["FACEIT", "FACEITService"],
            [],
            []),
        new(
            "EA Javelin",
            ["EAAntiCheat.GameServiceLauncher", "EAAntiCheatService"],
            ["EAAntiCheat.GameServiceLauncher.exe", "EAAntiCheat.Installer.exe"],
            [])
    ];

    public AntiCheatStatus Evaluate(string? executablePath)
    {
        foreach (var signature in Signatures)
        {
            if (IsVendorProcessRunning(signature.ProcessNames) && MatchesExecutableHints(executablePath, signature.ExecutableHints))
            {
                return new AntiCheatStatus
                {
                    IsDetected = true,
                    UseCompatibilityMode = true,
                    DisplayName = signature.DisplayName,
                    Reason = $"{signature.DisplayName} helper processes were detected on the system."
                };
            }

            if (HasVendorFilesNearby(executablePath, signature.FileIndicators))
            {
                return new AntiCheatStatus
                {
                    IsDetected = true,
                    UseCompatibilityMode = true,
                    DisplayName = signature.DisplayName,
                    Reason = $"{signature.DisplayName} files were detected next to the game executable."
                };
            }
        }

        return AntiCheatStatus.None;
    }

    private static bool IsVendorProcessRunning(IEnumerable<string> processNames)
    {
        foreach (var processName in processNames)
        {
            try
            {
                if (Process.GetProcessesByName(processName).Length > 0)
                {
                    return true;
                }
            }
            catch
            {
            }
        }

        return false;
    }

    private static bool HasVendorFilesNearby(string? executablePath, IEnumerable<string> indicators)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return false;
        }

        var root = Path.GetDirectoryName(executablePath);
        if (string.IsNullOrWhiteSpace(root))
        {
            return false;
        }

        foreach (var indicator in indicators)
        {
            var candidate = Path.Combine(root, indicator);
            if (Directory.Exists(candidate) || File.Exists(candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesExecutableHints(string? executablePath, IEnumerable<string> executableHints)
    {
        var hints = executableHints.ToArray();
        if (hints.Length == 0)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        return hints.Any(hint => executablePath.Contains(hint, StringComparison.OrdinalIgnoreCase));
    }

    private sealed record VendorSignature(
        string DisplayName,
        IReadOnlyList<string> ProcessNames,
        IReadOnlyList<string> FileIndicators,
        IReadOnlyList<string> ExecutableHints);
}
