using System.Text;
using FrameBoost.Core;
using FrameBoost.Models;

namespace FrameBoost.Services;

internal sealed class IssueReportService
{
    public string ExportIssueReport(
        AppSettings settings,
        ActiveBoostSession? activeSession,
        TelemetrySnapshot telemetry,
        SessionReport? lastSessionReport,
        ProfileTuningRecommendation recommendation,
        IReadOnlyList<DetectedGame> detectedGames)
    {
        AppPaths.EnsureDataDirectory();

        var timestamp = DateTimeOffset.Now;
        var fileName = $"CloudFrame-IssueReport-{timestamp:yyyyMMdd-HHmmss}.txt";
        var outputPath = Path.Combine(AppPaths.DataDirectory, fileName);
        var builder = new StringBuilder();

        builder.AppendLine("CloudFrame Issue Report");
        builder.AppendLine("======================");
        builder.AppendLine($"Generated: {timestamp:yyyy-MM-dd HH:mm:ss zzz}");
        builder.AppendLine($"App version: {UpdateCheckerService.GetCurrentVersion()}");
        builder.AppendLine($"Repository: {settings.GitHubRepository}");
        builder.AppendLine();

        builder.AppendLine("Session");
        builder.AppendLine("-------");
        if (activeSession is null)
        {
            builder.AppendLine("No active boost session.");
        }
        else
        {
            builder.AppendLine($"Profile: {activeSession.Profile.Name}");
            builder.AppendLine($"Preset: {activeSession.Profile.BoostPreset}");
            builder.AppendLine($"Anchor PID: {activeSession.RecoveryState.AnchorProcessId}");
            builder.AppendLine($"Game PID: {activeSession.RecoveryState.GameProcessId}");
            builder.AppendLine($"Game process: {activeSession.RecoveryState.GameProcessName}");
            builder.AppendLine($"Engine hint: {activeSession.RecoveryState.EngineHint ?? "Unknown"}");
            builder.AppendLine($"Compatibility mode: {activeSession.AntiCheatStatus.UseCompatibilityMode}");
            builder.AppendLine($"Anti-cheat label: {activeSession.AntiCheatStatus.DisplayName}");
        }

        builder.AppendLine();
        builder.AppendLine("Telemetry");
        builder.AppendLine("---------");
        builder.AppendLine($"CPU: {telemetry.CpuPercent:0.#}%");
        builder.AppendLine($"GPU: {(telemetry.GpuPercent is double gpu ? $"{gpu:0.#}%" : "Unavailable")}");
        builder.AppendLine($"FPS: {(telemetry.FramesPerSecond is double fps ? $"{fps:0.#}" : "--")}");
        builder.AppendLine($"Frame time: {(telemetry.FrameTimeMs is double frameTime ? $"{frameTime:0.00} ms" : "Unavailable")}");
        builder.AppendLine($"FPS status: {telemetry.FpsStatus}");
        builder.AppendLine($"Tracking PID: {(telemetry.TargetProcessId?.ToString() ?? "None")}");
        builder.AppendLine($"Tracking name: {telemetry.TargetProcessName ?? "None"}");
        builder.AppendLine($"Monitoring mode: {settings.MonitoringMode}");
        builder.AppendLine($"Maintenance backoff: {settings.EnableMaintenanceBackoff}");

        builder.AppendLine();
        builder.AppendLine("Recommendation");
        builder.AppendLine("--------------");
        builder.AppendLine(recommendation.Title);
        builder.AppendLine(recommendation.Detail);
        builder.AppendLine($"Auto-apply candidate: {recommendation.CanAutoApply}");

        if (lastSessionReport is not null)
        {
            builder.AppendLine();
            builder.AppendLine("Last Session Result");
            builder.AppendLine("-------------------");
            builder.AppendLine(lastSessionReport.ProfileName);
            builder.AppendLine(lastSessionReport.Summary);
            builder.AppendLine(lastSessionReport.Detail);
            if (lastSessionReport.BaselineAverageFps is not null || lastSessionReport.LiveAverageFps is not null)
            {
                builder.AppendLine($"Average FPS: {(lastSessionReport.BaselineAverageFps is double baselineAvg ? baselineAvg.ToString("0.0") : "--")} -> {(lastSessionReport.LiveAverageFps is double liveAvg ? liveAvg.ToString("0.0") : "--")}");
            }

            if (lastSessionReport.BaselineOnePercentLowFps is not null || lastSessionReport.LiveOnePercentLowFps is not null)
            {
                builder.AppendLine($"1% low FPS: {(lastSessionReport.BaselineOnePercentLowFps is double baselineLow ? baselineLow.ToString("0.0") : "--")} -> {(lastSessionReport.LiveOnePercentLowFps is double liveLow ? liveLow.ToString("0.0") : "--")}");
            }

            if (lastSessionReport.BaselinePacingScore is not null || lastSessionReport.LivePacingScore is not null)
            {
                builder.AppendLine($"Pacing score: {(lastSessionReport.BaselinePacingScore is double baselinePacing ? baselinePacing.ToString("0") : "--")} -> {(lastSessionReport.LivePacingScore is double livePacing ? livePacing.ToString("0") : "--")}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Detected Matches");
        builder.AppendLine("----------------");
        foreach (var game in detectedGames)
        {
            builder.AppendLine($"{game.Profile.Name} | PID {game.ProcessId} | {game.ProcessName} | Engine: {game.EngineHint ?? "Unknown"} | Launcher handoff: {game.HasLauncherHandoff}");
        }

        builder.AppendLine();
        builder.AppendLine("Recent Log");
        builder.AppendLine("----------");
        foreach (var line in ReadRecentLogLines(60))
        {
            builder.AppendLine(line);
        }

        File.WriteAllText(outputPath, builder.ToString());
        return outputPath;
    }

    private static IReadOnlyList<string> ReadRecentLogLines(int lineCount)
    {
        if (!File.Exists(AppPaths.LogPath))
        {
            return [];
        }

        try
        {
            return File.ReadLines(AppPaths.LogPath)
                .TakeLast(lineCount)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }
}
