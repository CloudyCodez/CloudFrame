using System.Net.Http.Headers;
using System.Reflection;
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

            return UpdateCheckResult.Available(
                currentVersion,
                payload.TagName,
                payload.Name,
                payload.Body,
                payload.HtmlUrl,
                payload.PublishedAt);
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
    string ReleaseUrl,
    DateTimeOffset? PublishedAt,
    string Message)
{
    public static UpdateCheckResult Disabled(string message) =>
        new(false, false, false, UpdateCheckerService.GetCurrentVersion(), string.Empty, string.Empty, string.Empty, string.Empty, null, message);

    public static UpdateCheckResult Failed(string message) =>
        new(true, false, false, UpdateCheckerService.GetCurrentVersion(), string.Empty, string.Empty, string.Empty, string.Empty, null, message);

    public static UpdateCheckResult UpToDate(string currentVersion) =>
        new(true, false, false, currentVersion, currentVersion, string.Empty, string.Empty, string.Empty, null, "CloudFrame is already up to date.");

    public static UpdateCheckResult Skipped(string latestVersion, string releaseUrl) =>
        new(true, false, true, UpdateCheckerService.GetCurrentVersion(), latestVersion, string.Empty, string.Empty, releaseUrl, null, $"Version {latestVersion} is currently skipped.");

    public static UpdateCheckResult Available(
        string currentVersion,
        string latestVersion,
        string releaseName,
        string releaseNotes,
        string releaseUrl,
        DateTimeOffset? publishedAt) =>
        new(true, true, false, currentVersion, latestVersion, releaseName, releaseNotes, releaseUrl, publishedAt, $"CloudFrame {latestVersion} is available.");
}
