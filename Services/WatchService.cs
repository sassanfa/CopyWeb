using System.Security.Cryptography;
using System.Text.Json;
using CopyWeb.Models;

namespace CopyWeb.Services;

public sealed record WatchResult(int Checked, IReadOnlyList<string> Changed, DateTimeOffset Timestamp);

public static class WatchService
{
    public static async Task<WatchResult> CheckAsync(string projectFile, CancellationToken token = default)
    {
        var project = await ProjectStorage.LoadAsync(projectFile, token).ConfigureAwait(false);
        var manifestPath = Path.Combine(Path.GetDirectoryName(projectFile)!, "watch-manifest.json");
        Dictionary<string, string> previous = [];
        try { if (File.Exists(manifestPath)) previous = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(manifestPath, token).ConfigureAwait(false)) ?? []; } catch { }
        var current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var changed = new List<string>();
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
        foreach (var link in project.Links.Where(x => x.IsSelected && UrlTools.IsLikelyPageUrl(x.Uri)))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var bytes = await client.GetByteArrayAsync(link.Uri, token).ConfigureAwait(false);
                var hash = Convert.ToHexString(SHA256.HashData(bytes));
                current[link.Url] = hash;
                if (previous.TryGetValue(link.Url, out var old) && !old.Equals(hash, StringComparison.OrdinalIgnoreCase)) changed.Add(link.Url);
            }
            catch { }
        }
        var temp = manifestPath + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(current, new JsonSerializerOptions { WriteIndented = true }), token).ConfigureAwait(false);
        File.Move(temp, manifestPath, true);
        if (changed.Count > 0 && Uri.TryCreate(project.RootUrl, UriKind.Absolute, out var root))
        {
            foreach (var item in project.Links.Where(x => changed.Contains(x.Url, StringComparer.OrdinalIgnoreCase)))
            {
                item.State = CopyWeb.Models.LinkState.Pending;
                item.Error = null;
            }
            await ProjectStorage.SaveAsync(projectFile, root, project.Links, project.Proxy, token).ConfigureAwait(false);
        }
        return new WatchResult(current.Count, changed, DateTimeOffset.Now);
    }
}
