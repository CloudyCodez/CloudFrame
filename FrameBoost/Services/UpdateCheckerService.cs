using System.Net.Http.Headers;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FrameBoost.Services;

internal sealed class UpdateCheckerService
{
    private static readonly HttpClient Client = CreateClient();

    private readonly Logger _logger;

    public UpdateCheckerService(Logger logger)
    {
        _logger = logger;
    }

    public async Task<UpdateCheckResult> CheckForUpdateAsync(string repository, string? skippedVersion, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repository) || !repository.Contains('/'))
        {
            return UpdateCheckResult.Disabled("GitHub Releases are not configured yet.");
        }

        try
        {
            var response = await Client.GetAsync(
                $"https://api.github.com/repos/{repository}/releases/latest",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.Log($"Update check could not reach GitHub Releases for '{repository}' ({(int)response.StatusCode}).");
                return UpdateCheckResult.Failed("CloudFrame could not reach GitHub Releases right now.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<GitHubReleasePayload>(stream, cancellationToken: cancellationToken);
            if (payload is null || string.IsNullOrWhiteSpace(payload.TagName))
            {
                return UpdateCheckResult.Failed("GitHub returned an empty release payload.");
            }

            var latestVersion = NormalizeVersion(payload.TagName);
            var currentVersion = GetCurrentVersion();
            var skippedNormalized = NormalizeVersion(skippedVersion);

            if (string.Equals(latestVersion, skippedNormalized, StringComparison.OrdinalIgnoreCase))
            {
                return UpdateCheckResult.Skipped(payload.TagName, payload.HtmlUrl);
            }

            var hasUpdate = CompareVersions(latestVersion, currentVersion) > 0;
            if (!hasUpdate)
            {
                return UpdateCheckResult.UpToDate(currentVersion);
            }

            var asset = payload.Assets
                .FirstOrDefault(static candidate =>
                    candidate.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                    candidate.Name.Contains("CloudFrame-win-x64", StringComparison.OrdinalIgnoreCase));

            return UpdateCheckResult.Available(
                currentVersion,
                payload.TagName,
                payload.Name,
                payload.Body,
                BuildPatchSummaryLines(payload.Body),
                payload.HtmlUrl,
                payload.PublishedAt,
                asset?.Name ?? string.Empty,
                asset?.BrowserDownloadUrl ?? string.Empty);
        }
        catch (TaskCanceledException)
        {
            return UpdateCheckResult.Failed("CloudFrame timed out while checking for updates.");
        }
        catch (Exception ex)
        {
            _logger.Log($"Update check failed: {ex.Message}");
            return UpdateCheckResult.Failed("CloudFrame could not complete the update check.");
        }
    }

    public static string GetCurrentVersion()
    {
        return Assembly.GetExecutingAssembly()
                   .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
               ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
               ?? "0.0.0";
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8)
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CloudFrame", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    private static string NormalizeVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim().TrimStart('v', 'V');
    }

    private static int CompareVersions(string left, string right)
    {
        if (Version.TryParse(left, out var leftVersion) && Version.TryParse(right, out var rightVersion))
        {
            return leftVersion.CompareTo(rightVersion);
        }

        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> BuildPatchSummaryLines(string? releaseNotes)
    {
        if (string.IsNullOrWhiteSpace(releaseNotes))
        {
            return ["General improvements and fixes."];
        }

        var lines = releaseNotes
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static line => line.Trim())
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        var bulletLines = lines
            .Where(static line => line.StartsWith("- ") || line.StartsWith("* "))
            .Select(static line => line[2..].Trim())
            .Where(static line => line.Length > 3)
            .Take(6)
            .ToList();

        if (bulletLines.Count > 0)
        {
            return bulletLines;
        }

        var sentenceMatches = Regex.Matches(releaseNotes, @"[^.!?]+[.!?]?");
        var fallback = sentenceMatches
            .Select(static match => match.Value.Trim())
            .Where(static line => line.Length > 8)
            .Take(4)
            .ToList();

        return fallback.Count > 0
            ? fallback
            : ["General improvements and fixes."];
    }

    private sealed class GitHubReleasePayload
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        [JsonPropertyName("published_at")]
        public DateTimeOffset? PublishedAt { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubReleaseAsset> Assets { get; set; } = [];
    }

    private sealed class GitHubReleaseAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;
    }
}

internal sealed record UpdateCheckResult(
    bool IsEnabled,
    bool IsUpdateAvailable,
    bool IsSkipped,
    string CurrentVersion,
    string LatestVersion,
    string ReleaseName,
    string ReleaseNotes,
    IReadOnlyList<string> PatchSummaryLines,
    string ReleaseUrl,
    DateTimeOffset? PublishedAt,
    string AssetName,
    string AssetDownloadUrl,
    string Message)
{
    public static UpdateCheckResult Disabled(string message) =>
        new(false, false, false, UpdateCheckerService.GetCurrentVersion(), string.Empty, string.Empty, string.Empty, [], string.Empty, null, string.Empty, string.Empty, message);

    public static UpdateCheckResult Failed(string message) =>
        new(true, false, false, UpdateCheckerService.GetCurrentVersion(), string.Empty, string.Empty, string.Empty, [], string.Empty, null, string.Empty, string.Empty, message);

    public static UpdateCheckResult UpToDate(string currentVersion) =>
        new(true, false, false, currentVersion, currentVersion, string.Empty, string.Empty, [], string.Empty, null, string.Empty, string.Empty, "CloudFrame is already up to date.");

    public static UpdateCheckResult Skipped(string latestVersion, string releaseUrl) =>
        new(true, false, true, UpdateCheckerService.GetCurrentVersion(), latestVersion, string.Empty, string.Empty, [], releaseUrl, null, string.Empty, string.Empty, $"Version {latestVersion} is currently skipped.");

    public static UpdateCheckResult Available(
        string currentVersion,
        string latestVersion,
        string releaseName,
        string releaseNotes,
        IReadOnlyList<string> patchSummaryLines,
        string releaseUrl,
        DateTimeOffset? publishedAt,
        string assetName,
        string assetDownloadUrl) =>
        new(true, true, false, currentVersion, latestVersion, releaseName, releaseNotes, patchSummaryLines, releaseUrl, publishedAt, assetName, assetDownloadUrl, $"CloudFrame {latestVersion} is available.");
}
