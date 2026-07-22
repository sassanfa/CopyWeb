using System.IO.Compression;
using System.Net;
using System.Text;
using System.Diagnostics;

namespace CopyWeb.Services;

public static class PublishService
{
    public static Task CreateZipAsync(string projectDirectory, string zipPath, CancellationToken token = default) => Task.Run(() =>
    {
        if (File.Exists(zipPath)) File.Delete(zipPath);
        ZipFile.CreateFromDirectory(projectDirectory, zipPath, CompressionLevel.Fastest, false);
    }, token);

    public static async Task PrepareIisAsync(string projectDirectory, string destination, CancellationToken token = default)
    {
        await Task.Run(() => CopyDirectory(projectDirectory, destination), token).ConfigureAwait(false);
        var webConfig = Path.Combine(destination, "web.config");
        if (!File.Exists(webConfig)) await File.WriteAllTextAsync(webConfig, "<?xml version=\"1.0\" encoding=\"utf-8\"?><configuration><system.webServer><staticContent><mimeMap fileExtension=\".webp\" mimeType=\"image/webp\" /></staticContent><directoryBrowse enabled=\"false\" /></system.webServer></configuration>", Encoding.UTF8, token).ConfigureAwait(false);
    }

    public static async Task UploadFtpAsync(string sourceDirectory, Uri ftpRoot, NetworkCredential credentials, IProgress<string>? progress = null, CancellationToken token = default)
    {
        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            token.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(sourceDirectory, file).Replace('\\', '/');
            var target = new Uri(ftpRoot, relative);
            var request = (FtpWebRequest)WebRequest.Create(target);
            request.Method = WebRequestMethods.Ftp.UploadFile;
            request.Credentials = credentials;
            request.UseBinary = true;
            await using var input = File.OpenRead(file);
            await using var output = await request.GetRequestStreamAsync().WaitAsync(token).ConfigureAwait(false);
            await input.CopyToAsync(output, token).ConfigureAwait(false);
            progress?.Report(relative);
        }
    }

    /// <summary>Uploads with the Windows OpenSSH sftp client using the user's configured SSH key.</summary>
    public static async Task UploadSftpAsync(string sourceDirectory, string host, string user, string remoteDirectory, IProgress<string>? progress = null, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(host) || host.Any(char.IsWhiteSpace) || host.Contains('"')) throw new ArgumentException("میزبان SFTP معتبر نیست.", nameof(host));
        var sftp = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator).Select(x => Path.Combine(x, "sftp.exe")).FirstOrDefault(File.Exists) ?? "sftp.exe";
        var batch = Path.Combine(Path.GetTempPath(), "copyweb-sftp-" + Guid.NewGuid().ToString("N") + ".txt");
        var lines = new List<string>();
        var directories = Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories).Select(x => Path.GetRelativePath(sourceDirectory, x).Replace('\\', '/')).OrderBy(x => x.Length);
        foreach (var directory in directories) lines.Add($"-mkdir {remoteDirectory.TrimEnd('/')}/{directory}");
        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, file).Replace('\\', '/');
            lines.Add($"put \"{file.Replace("\"", "") }\" \"{remoteDirectory.TrimEnd('/')}/{relative}\"");
            progress?.Report(relative);
        }
        await File.WriteAllLinesAsync(batch, lines, Encoding.UTF8, token).ConfigureAwait(false);
        try
        {
            using var process = new Process { StartInfo = new ProcessStartInfo { FileName = sftp, Arguments = $"-b \"{batch}\" {user}@{host}", UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true } };
            process.Start();
            await process.WaitForExitAsync(token).ConfigureAwait(false);
            if (process.ExitCode != 0) throw new InvalidOperationException((await process.StandardError.ReadToEndAsync(token).ConfigureAwait(false)).Trim());
        }
        finally { try { File.Delete(batch); } catch { } }
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
        foreach (var dir in Directory.EnumerateDirectories(source)) CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
    }
}
