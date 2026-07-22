using System.Security.Cryptography;
using System.Text.Json;

namespace CopyWeb.Services;

public sealed record SnapshotInfo(string Id, string Directory, DateTimeOffset CreatedAt, int Files);
public sealed record SnapshotDiffEntry(string Path, string Status, string? BeforeHash, string? AfterHash);

public static class SnapshotVersionService
{
    private sealed class Manifest { public DateTimeOffset CreatedAt { get; set; } public Dictionary<string, string> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase); }

    public static async Task<SnapshotInfo> CreateAsync(string projectDirectory, CancellationToken token = default)
    {
        var root = Path.GetFullPath(projectDirectory); var id = DateTime.Now.ToString("yyyyMMdd-HHmmss"); var destination = Path.Combine(root, "snapshots", id); Directory.CreateDirectory(destination);
        var manifest = new Manifest { CreatedAt = DateTimeOffset.Now };
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Where(x => !x.StartsWith(Path.Combine(root, "snapshots") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
        {
            token.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(root, file); var target = Path.Combine(destination, relative); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target, true);
            await using var stream = File.OpenRead(file); manifest.Files[relative] = Convert.ToHexString(await SHA256.HashDataAsync(stream, token).ConfigureAwait(false));
        }
        await File.WriteAllTextAsync(Path.Combine(destination, "manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), token).ConfigureAwait(false);
        return new(id, destination, manifest.CreatedAt, manifest.Files.Count);
    }

    public static IReadOnlyList<SnapshotInfo> List(string projectDirectory)
    {
        var folder = Path.Combine(projectDirectory, "snapshots"); if (!Directory.Exists(folder)) return [];
        return Directory.EnumerateDirectories(folder).Select(path => LoadInfo(path)).Where(x => x is not null).Cast<SnapshotInfo>().OrderByDescending(x => x.CreatedAt).ToList();
    }

    public static async Task<IReadOnlyList<SnapshotDiffEntry>> CompareAsync(string beforeDirectory, string afterDirectory, CancellationToken token = default)
    {
        var before = await ReadManifestAsync(beforeDirectory, token).ConfigureAwait(false); var after = await ReadManifestAsync(afterDirectory, token).ConfigureAwait(false); var result = new List<SnapshotDiffEntry>();
        foreach (var path in before.Keys.Union(after.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x))
        {
            before.TryGetValue(path, out var oldHash); after.TryGetValue(path, out var newHash);
            if (oldHash is null) result.Add(new(path, "Added", null, newHash)); else if (newHash is null) result.Add(new(path, "Removed", oldHash, null)); else if (!oldHash.Equals(newHash, StringComparison.OrdinalIgnoreCase)) result.Add(new(path, "Changed", oldHash, newHash));
        }
        return result;
    }

    public static string ResolveFile(string snapshotDirectory, string relativePath) => Path.Combine(snapshotDirectory, relativePath);

    private static SnapshotInfo? LoadInfo(string directory)
    {
        try { var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(Path.Combine(directory, "manifest.json"))); return manifest is null ? null : new(Path.GetFileName(directory), directory, manifest.CreatedAt, manifest.Files.Count); } catch { return null; }
    }

    private static async Task<Dictionary<string, string>> ReadManifestAsync(string directory, CancellationToken token)
    {
        await using var stream = File.OpenRead(Path.Combine(directory, "manifest.json")); var manifest = await JsonSerializer.DeserializeAsync<Manifest>(stream, cancellationToken: token).ConfigureAwait(false); return manifest?.Files ?? new(StringComparer.OrdinalIgnoreCase);
    }
}
