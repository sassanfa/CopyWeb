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
        var project = new SavedLinkProject { RootUrl = root.AbsoluteUri, Links = links.ToList() };
        await using var stream = File.Create(fileName);
        await JsonSerializer.SerializeAsync(stream, project, Options, token).ConfigureAwait(false);
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

    private static void Register(string fileName)
    {
        try
        {
            var files = GetKnownProjectFiles().Where(File.Exists).ToHashSet(StringComparer.OrdinalIgnoreCase);
            files.Add(Path.GetFullPath(fileName));
            Directory.CreateDirectory(Path.GetDirectoryName(IndexFile)!);
            File.WriteAllText(IndexFile, JsonSerializer.Serialize(files.OrderBy(x => x).ToList(), Options));
        }
        catch
        {
            // A project must remain usable even when the optional index cannot be written.
        }
    }
}
