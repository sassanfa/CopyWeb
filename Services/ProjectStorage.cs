using CopyWeb.Models;
using System.Text.Json;

namespace CopyWeb.Services;

public static class ProjectStorage
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static string IndexFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CopyWeb", "projects.json");

    public static async Task SaveAsync(string fileName, Uri root, IEnumerable<DownloadItem> links, CancellationToken token = default)
    {
        await SaveAsync(fileName, root, links, null, token).ConfigureAwait(false);
    }

    public static async Task SaveAsync(
        string fileName,
        Uri root,
        IEnumerable<DownloadItem> links,
        ProxySnapshot? proxy,
        CancellationToken token = default)
    {
        var project = new SavedLinkProject { RootUrl = root.AbsoluteUri, Links = links.ToList(), Proxy = proxy };
        var directory = Path.GetDirectoryName(Path.GetFullPath(fileName))!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".{Path.GetFileName(fileName)}.{Guid.NewGuid():N}.tmp");
        await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.WriteThrough | FileOptions.Asynchronous))
        {
            await JsonSerializer.SerializeAsync(stream, project, Options, token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
            stream.Flush(true);
        }
        ReplaceAtomically(temporary, fileName);
        Register(fileName);
    }

    public static async Task<SavedLinkProject> LoadAsync(string fileName, CancellationToken token = default)
    {
        await using var stream = File.OpenRead(fileName);
        return await JsonSerializer.DeserializeAsync<SavedLinkProject>(stream, Options, token).ConfigureAwait(false)
               ?? throw new InvalidDataException("فایل لیست معتبر نیست.");
    }

    public static IReadOnlyList<string> GetKnownProjectFiles()
    {
        try
        {
            if (!File.Exists(IndexFile)) return [];
            return JsonSerializer.Deserialize<List<string>>(File.ReadAllText(IndexFile)) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void Forget(string fileName)
    {
        try
        {
            var fullPath = Path.GetFullPath(fileName);
            var files = GetKnownProjectFiles()
                .Where(File.Exists)
                .Where(x => !Path.GetFullPath(x).Equals(fullPath, StringComparison.OrdinalIgnoreCase))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            Directory.CreateDirectory(Path.GetDirectoryName(IndexFile)!);
            var temp = Path.Combine(Path.GetDirectoryName(IndexFile)!, $".projects.{Guid.NewGuid():N}.tmp");
            File.WriteAllText(temp, JsonSerializer.Serialize(files.OrderBy(x => x).ToList(), Options));
            ReplaceAtomically(temp, IndexFile);
        }
        catch
        {
            // Removing a project must still succeed when the optional index is unavailable.
        }
    }

    private static void Register(string fileName)
    {
        try
        {
            var files = GetKnownProjectFiles().Where(File.Exists).ToHashSet(StringComparer.OrdinalIgnoreCase);
            files.Add(Path.GetFullPath(fileName));
            Directory.CreateDirectory(Path.GetDirectoryName(IndexFile)!);
            var temp = Path.Combine(Path.GetDirectoryName(IndexFile)!, $".projects.{Guid.NewGuid():N}.tmp");
            File.WriteAllText(temp, JsonSerializer.Serialize(files.OrderBy(x => x).ToList(), Options));
            ReplaceAtomically(temp, IndexFile);
        }
        catch
        {
            // A project must remain usable even when the optional index cannot be written.
        }
    }

    private static void ReplaceAtomically(string temporary, string destination)
    {
        try
        {
            if (File.Exists(destination))
            {
                try { File.Replace(temporary, destination, null, true); return; }
                catch (PlatformNotSupportedException) { }
                catch (IOException) { }
            }
            File.Move(temporary, destination, true);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
        }
    }
}
