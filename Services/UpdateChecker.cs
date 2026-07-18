using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;

namespace CopyWeb.Services;

public sealed record UpdateCheckResult(bool IsNewer, string CurrentVersion, string LatestVersion, string ReleaseUrl, string? Error = null);

public static class UpdateChecker
{
    public const string CurrentVersion = "1.1.2";
    public const string RepositoryUrl = "https://github.com/SassanFa/CopyWeb";
    private const string LatestReleaseApi = "https://api.github.com/repos/SassanFa/CopyWeb/releases/latest";

    public static async Task<UpdateCheckResult> CheckAsync(CancellationToken token = default)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CopyWeb", CurrentVersion));
            using var response = await client.GetAsync(LatestReleaseApi, token).ConfigureAwait(false);
            // A repository can be valid before its first GitHub release exists.
            // Treat that case as "no newer release" and still offer the repository link.
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return new(false, CurrentVersion, CurrentVersion, RepositoryUrl);
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token).ConfigureAwait(false));
            var tag = json.RootElement.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() : null;
            var url = json.RootElement.TryGetProperty("html_url", out var urlElement) ? urlElement.GetString() : null;
            var latest = NormalizeVersion(tag);
            if (latest is null) return new(false, CurrentVersion, CurrentVersion, url ?? RepositoryUrl, "نسخه منتشرشده نامعتبر است.");
            return new(Version.TryParse(latest, out var latestVersion) && latestVersion > new Version(CurrentVersion), CurrentVersion, latest, url ?? RepositoryUrl);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new(false, CurrentVersion, CurrentVersion, RepositoryUrl, ex.Message);
        }
    }

    public static void OpenRepository(string? url = null)
    {
        try { Process.Start(new ProcessStartInfo(url ?? RepositoryUrl) { UseShellExecute = true }); }
        catch { }
    }

    private static string? NormalizeVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim().TrimStart('v', 'V');
        return Version.TryParse(value, out _) ? value : null;
    }
}
