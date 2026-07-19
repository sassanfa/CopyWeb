using System.IO.Compression;

namespace CopyWeb.Services;

public static class ProjectArchiveService
{
    public static async Task CreateBackupAsync(string projectFile, string archiveFile, CancellationToken token = default)
    {
        var projectPath = Path.GetFullPath(projectFile);
        var projectDirectory = Path.GetDirectoryName(projectPath) ?? throw new DirectoryNotFoundException(projectPath);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(archiveFile))!);
        var temporary = archiveFile + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var archive = ZipFile.Open(temporary, ZipArchiveMode.Create))
            {
                foreach (var file in Directory.EnumerateFiles(projectDirectory, "*", SearchOption.AllDirectories))
                {
                    token.ThrowIfCancellationRequested();
                    if (file.Equals(Path.GetFullPath(archiveFile), StringComparison.OrdinalIgnoreCase) ||
                        file.Equals(Path.GetFullPath(temporary), StringComparison.OrdinalIgnoreCase)) continue;
                    var relative = Path.GetRelativePath(projectDirectory, file);
                    archive.CreateEntryFromFile(file, relative, CompressionLevel.Fastest);
                }
            }
            File.Move(temporary, archiveFile, true);
            await Task.CompletedTask.ConfigureAwait(false);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
        }
    }

    public static void RestoreBackup(string archiveFile, string destinationDirectory)
    {
        var destination = Path.GetFullPath(destinationDirectory);
        Directory.CreateDirectory(destination);
        using var archive = ZipFile.OpenRead(archiveFile);
        foreach (var entry in archive.Entries)
        {
            var target = Path.GetFullPath(Path.Combine(destination, entry.FullName));
            if (!target.StartsWith(destination.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                !target.Equals(destination, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("فایل پشتیبان مسیر نامعتبر دارد.");
            if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(target); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, true);
        }
    }
}
