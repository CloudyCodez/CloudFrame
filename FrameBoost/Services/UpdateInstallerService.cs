using System.Diagnostics;
using System.Text;
using FrameBoost.Core;

namespace FrameBoost.Services;

internal sealed class UpdateInstallerService
{
    private readonly Logger _logger;

    public UpdateInstallerService(Logger logger)
    {
        _logger = logger;
    }

    public async Task<PreparedUpdate?> PrepareUpdateAsync(UpdateCheckResult result, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(result.AssetDownloadUrl))
        {
            _logger.Log($"No downloadable release asset was found for CloudFrame {result.LatestVersion}.");
            return null;
        }

        AppPaths.EnsureDataDirectory();
        Directory.CreateDirectory(AppPaths.UpdateStagingDirectory);

        var versionFolder = Path.Combine(AppPaths.UpdateStagingDirectory, result.LatestVersion);
        Directory.CreateDirectory(versionFolder);

        var zipPath = Path.Combine(versionFolder, string.IsNullOrWhiteSpace(result.AssetName) ? $"CloudFrame-{result.LatestVersion}.zip" : result.AssetName);
        var extractPath = Path.Combine(versionFolder, "package");

        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(3)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CloudFrame-Updater/1.0");

        await using (var responseStream = await client.GetStreamAsync(result.AssetDownloadUrl, cancellationToken))
        await using (var fileStream = File.Create(zipPath))
        {
            await responseStream.CopyToAsync(fileStream, cancellationToken);
        }

        if (Directory.Exists(extractPath))
        {
            Directory.Delete(extractPath, recursive: true);
        }

        System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractPath);

        var packageRoot = ResolvePackageRoot(extractPath);
        if (packageRoot is null)
        {
            _logger.Log($"Downloaded CloudFrame {result.LatestVersion}, but the package layout was not recognized.");
            return null;
        }

        return new PreparedUpdate(result, zipPath, packageRoot);
    }

    public void LaunchInstallerAndExit(PreparedUpdate update)
    {
        var appDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var updaterScriptPath = Path.Combine(Path.GetDirectoryName(update.PackageRoot) ?? AppPaths.UpdateStagingDirectory, "apply-update.ps1");
        var exePath = Path.Combine(appDirectory, "CloudFrame.exe");

        var script = BuildUpdateScript(
            Environment.ProcessId,
            appDirectory,
            update.PackageRoot,
            exePath);

        File.WriteAllText(updaterScriptPath, script, Encoding.UTF8);

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{updaterScriptPath}\"",
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }

    private static string? ResolvePackageRoot(string extractPath)
    {
        var directExe = Path.Combine(extractPath, "CloudFrame.exe");
        if (File.Exists(directExe))
        {
            return extractPath;
        }

        var nestedRoot = Path.Combine(extractPath, "CloudFrame");
        if (File.Exists(Path.Combine(nestedRoot, "CloudFrame.exe")))
        {
            return nestedRoot;
        }

        return Directory.GetDirectories(extractPath)
            .FirstOrDefault(static directory => File.Exists(Path.Combine(directory, "CloudFrame.exe")));
    }

    private static string BuildUpdateScript(int currentProcessId, string appDirectory, string packageRoot, string exePath)
    {
        static string Escape(string value) => value.Replace("'", "''");

        return $@"
$ErrorActionPreference = 'Stop'
$pidToWait = {currentProcessId}
$appDir = '{Escape(appDirectory)}'
$packageRoot = '{Escape(packageRoot)}'
$exePath = '{Escape(exePath)}'

for ($i = 0; $i -lt 60; $i++) {{
    $stillRunning = Get-Process -Id $pidToWait -ErrorAction SilentlyContinue
    if (-not $stillRunning) {{ break }}
    Start-Sleep -Milliseconds 500
}}

Get-ChildItem -LiteralPath $packageRoot -Force | ForEach-Object {{
    $destination = Join-Path $appDir $_.Name
    if ($_.PSIsContainer) {{
        if (Test-Path -LiteralPath $destination) {{
            Remove-Item -LiteralPath $destination -Recurse -Force
        }}
        Copy-Item -LiteralPath $_.FullName -Destination $destination -Recurse -Force
    }} else {{
        Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
    }}
}}

Start-Process -FilePath $exePath
";
    }
}

internal sealed record PreparedUpdate(
    UpdateCheckResult Result,
    string ArchivePath,
    string PackageRoot);
