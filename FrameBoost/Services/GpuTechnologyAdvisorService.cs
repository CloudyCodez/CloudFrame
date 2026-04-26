using System.Diagnostics;

namespace FrameBoost.Services;

internal sealed class GpuTechnologyAdvisorService
{
    public GpuTechnologyReport GetReport()
    {
        var gpuNames = GetGpuNames();
        var hasNvidia = gpuNames.Any(name => name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase));
        var hasAmd = gpuNames.Any(name => name.Contains("AMD", StringComparison.OrdinalIgnoreCase) || name.Contains("Radeon", StringComparison.OrdinalIgnoreCase));
        var hasIntel = gpuNames.Any(name => name.Contains("Intel", StringComparison.OrdinalIgnoreCase));
        var hasRtx = gpuNames.Any(IsRtxGpu);
        var hasArc = gpuNames.Any(name => name.Contains("Intel", StringComparison.OrdinalIgnoreCase) && name.Contains("Arc", StringComparison.OrdinalIgnoreCase));

        return new GpuTechnologyReport(
            gpuNames,
            BuildDlssStatus(hasNvidia, hasRtx),
            BuildFsrStatus(hasAmd || hasNvidia || hasIntel),
            BuildXessStatus(hasIntel, hasArc),
            BuildExternalFrameGenStatus(hasNvidia, hasAmd, hasIntel));
    }

    private static IReadOnlyList<string> GetGpuNames()
    {
        var names = new List<string>();
        var powershellOutput = RunCommand(
            "powershell",
            "-NoProfile -ExecutionPolicy Bypass -Command \"Get-CimInstance Win32_VideoController | Select-Object -ExpandProperty Name\"");

        if (!string.IsNullOrWhiteSpace(powershellOutput))
        {
            names.AddRange(
                powershellOutput
                    .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct(StringComparer.OrdinalIgnoreCase));
        }

        if (names.Count > 0)
        {
            return names;
        }

        var wmicOutput = RunCommand("wmic", "path win32_VideoController get Name");
        if (!string.IsNullOrWhiteSpace(wmicOutput))
        {
            names.AddRange(
                wmicOutput
                    .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(static line => !string.Equals(line, "Name", StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase));
        }

        return names;
    }

    private static string RunCommand(string fileName, string arguments)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(3000);
            return output.Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsRtxGpu(string name)
        => name.Contains("RTX", StringComparison.OrdinalIgnoreCase);

    private static TechCapability BuildDlssStatus(bool hasNvidia, bool hasRtx)
    {
        if (!hasNvidia)
        {
            return new TechCapability("DLSS", "Not relevant on this rig", "No NVIDIA GPU was detected. DLSS is NVIDIA-only and still requires game-side integration.", false);
        }

        if (hasRtx)
        {
            return new TechCapability("DLSS", "Relevant hardware detected", "An NVIDIA RTX-class GPU is present, so DLSS can be available in supported games. CloudFrame cannot inject or enable DLSS from the outside.", true);
        }

        return new TechCapability("DLSS", "Partially relevant", "An NVIDIA GPU is present, but not every NVIDIA GPU supports modern DLSS paths. In all cases, DLSS still depends on game integration.", false);
    }

    private static TechCapability BuildFsrStatus(bool anyModernGpu)
    {
        if (!anyModernGpu)
        {
            return new TechCapability("FSR", "Unknown", "CloudFrame could not confidently identify a modern GPU. FSR is still a per-game integration, not an external CloudFrame feature.", false);
        }

        return new TechCapability("FSR", "Broadly relevant", "FSR is the most cross-vendor game-side upscaling family. It may be available in supported titles on many AMD, NVIDIA, and Intel GPUs, but CloudFrame cannot turn it on externally.", true);
    }

    private static TechCapability BuildXessStatus(bool hasIntel, bool hasArc)
    {
        if (hasArc)
        {
            return new TechCapability("XeSS", "Strongest relevance", "Intel Arc hardware was detected, so XeSS is especially relevant in supported games. CloudFrame still cannot inject or enable XeSS itself.", true);
        }

        if (hasIntel)
        {
            return new TechCapability("XeSS", "Relevant", "An Intel GPU was detected. XeSS may be available in supported games, but it remains a game-integrated technology.", true);
        }

        return new TechCapability("XeSS", "Possible but game-dependent", "XeSS support depends on the game. CloudFrame cannot add XeSS to a title that does not already support it.", false);
    }

    private static TechCapability BuildExternalFrameGenStatus(bool hasNvidia, bool hasAmd, bool hasIntel)
    {
        if (hasNvidia || hasAmd || hasIntel)
        {
            return new TechCapability("External Frame Gen", "Prototype candidate", "This rig is suitable for CloudFrame's future external frame interpolation experiments. The safe path is capture + interpolate + present, starting with non-anti-cheat borderless games only.", true);
        }

        return new TechCapability("External Frame Gen", "Unknown", "CloudFrame could not confirm a supported GPU path. External interpolation experiments should stay disabled until the capture pipeline is validated.", false);
    }
}

internal sealed record GpuTechnologyReport(
    IReadOnlyList<string> GpuNames,
    TechCapability Dlss,
    TechCapability Fsr,
    TechCapability Xess,
    TechCapability ExternalFrameGen);

internal sealed record TechCapability(string Name, string Status, string Detail, bool Highlight);
