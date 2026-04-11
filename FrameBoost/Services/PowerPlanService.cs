using System.Diagnostics;
using System.Text.RegularExpressions;
using FrameBoost.Models;

namespace FrameBoost.Services;

internal sealed class PowerPlanService
{
    private static readonly Regex PlanRegex = new(
        @"Power Scheme GUID:\s*([A-Fa-f0-9\-]+)\s*\((.+?)\)\s*(\*)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly Logger _logger;

    public PowerPlanService(Logger logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<PowerPlanInfo>> GetPlansAsync()
    {
        var result = await RunPowerCfgAsync("/list").ConfigureAwait(false);
        var plans = new List<PowerPlanInfo>();

        foreach (var line in result.StandardOutput.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
        {
            var match = PlanRegex.Match(line.Trim());
            if (!match.Success)
            {
                continue;
            }

            plans.Add(new PowerPlanInfo
            {
                Guid = match.Groups[1].Value,
                Name = match.Groups[2].Value.Trim(),
                IsActive = match.Groups[3].Success
            });
        }

        return plans;
    }

    public async Task<PowerPlanInfo?> GetActivePlanAsync()
    {
        var result = await RunPowerCfgAsync("/getactivescheme").ConfigureAwait(false);

        foreach (var line in result.StandardOutput.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
        {
            var match = PlanRegex.Match(line.Trim());
            if (!match.Success)
            {
                continue;
            }

            return new PowerPlanInfo
            {
                Guid = match.Groups[1].Value,
                Name = match.Groups[2].Value.Trim(),
                IsActive = true
            };
        }

        return null;
    }

    public async Task<bool> SetActivePlanAsync(string planGuid)
    {
        if (string.IsNullOrWhiteSpace(planGuid))
        {
            return false;
        }

        var result = await RunPowerCfgAsync($"/setactive {planGuid}").ConfigureAwait(false);

        if (result.ExitCode == 0)
        {
            return true;
        }

        _logger.Log($"Failed to switch power plan: {result.StandardError.Trim()}");
        return false;
    }

    private static async Task<PowerCfgResult> RunPowerCfgAsync(string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "powercfg",
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        process.Start();
        var standardOutput = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        var standardError = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);

        return new PowerCfgResult(process.ExitCode, standardOutput, standardError);
    }

    private sealed record PowerCfgResult(int ExitCode, string StandardOutput, string StandardError);
}
