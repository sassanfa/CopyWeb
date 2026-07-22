using System.Text.Json;

namespace CopyWeb.Services;

public sealed record ArchiveIssue(string Kind, string Path, string Message);
public sealed record ArchiveValidationResult(int FilesChecked, int ValidFiles, IReadOnlyList<ArchiveIssue> Issues)
{
    public bool IsValid => Issues.Count == 0;
}

public static class ArchiveValidatorService
{
    public static async Task<ArchiveValidationResult> ValidateAsync(string projectFile, CancellationToken token = default)
    {
        var issues = new List<ArchiveIssue>();
        var root = Path.GetFullPath(Path.GetDirectoryName(projectFile) ?? string.Empty);
        var checkedFiles = 0;
        if (!File.Exists(projectFile)) return new(0, 0, [new("Missing", projectFile, "فایل links.json پیدا نشد.")]);
        try { await ProjectStorage.LoadAsync(projectFile, token).ConfigureAwait(false); }
        catch (Exception ex) { issues.Add(new("Corrupt", "links.json", ex.Message)); }
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Where(x => !x.Contains(Path.DirectorySeparatorChar + "snapshots" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
        {
            token.ThrowIfCancellationRequested();
            checkedFiles++;
            try
            {
                var info = new FileInfo(file);
                if (info.Length == 0 && !Path.GetFileName(file).Equals("activity.log", StringComparison.OrdinalIgnoreCase)) issues.Add(new("Corrupt", Path.GetRelativePath(root, file), "فایل خالی است."));
                if (Path.GetExtension(file).Equals(".html", StringComparison.OrdinalIgnoreCase))
                {
                    var html = await File.ReadAllTextAsync(file, token).ConfigureAwait(false);
                    if (!html.Contains("<html", StringComparison.OrdinalIgnoreCase) && !html.Contains("<!doctype", StringComparison.OrdinalIgnoreCase)) issues.Add(new("Corrupt", Path.GetRelativePath(root, file), "ساختار HTML معتبر نیست."));
                }
            }
            catch (Exception ex) { issues.Add(new("Corrupt", Path.GetRelativePath(root, file), ex.Message)); }
        }
        foreach (var broken in OfflineLinkChecker.Scan(root)) issues.Add(new("BrokenLink", broken.SourceFile, $"{broken.Reference} → {broken.ExpectedPath}"));
        var manifest = Path.Combine(root, "assets-manifest.json");
        if (File.Exists(manifest))
        {
            try
            {
                using var document = JsonDocument.Parse(await File.ReadAllTextAsync(manifest, token).ConfigureAwait(false));
                foreach (var property in document.RootElement.EnumerateObject().SelectMany(x => x.Value.EnumerateObject()))
                    if (property.Value.ValueKind == JsonValueKind.String && !File.Exists(property.Value.GetString())) issues.Add(new("Missing", property.Name, "مسیر منبع در assets-manifest وجود ندارد."));
            }
            catch (Exception ex) { issues.Add(new("Corrupt", "assets-manifest.json", ex.Message)); }
        }
        return new(checkedFiles, Math.Max(0, checkedFiles - issues.Count(x => x.Kind == "Corrupt")), issues);
    }
}
