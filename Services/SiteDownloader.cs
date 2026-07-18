using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using CopyWeb.Models;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using LinkState = CopyWeb.Models.LinkState;

namespace CopyWeb.Services;

/// <summary>Downloads selected pages and their local resources with bounded parallelism and resumable checkpoints.</summary>
public sealed partial class SiteDownloader(SiteSession session)
{
    private readonly SiteSession _session = session;
    private readonly ConcurrentDictionary<string, string> _assets = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _assetGates = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _checkpointGate = new(1, 1);
    private string _outputRoot = string.Empty;
    private IProgress<DownloadProgress>? _progress;
    private int _completed;
    private int _total;
    private int _failed;
    private int _queued;
    private int _activeDownloads;
    private long _totalBytesDownloaded;
    private long _totalBytesExpected;
    private long _lastProgressTimestamp;

    public async Task DownloadAsync(
        Uri root,
        IReadOnlyCollection<DownloadItem> sourceItems,
        string outputDirectory,
        IProgress<DownloadProgress>? progress,
        CancellationToken token,
        int delayMilliseconds = 0,
        int maxConcurrentDownloads = 4,
        long minimumFreeDiskSpaceMb = 512)
    {
        _outputRoot = outputDirectory;
        _progress = progress;
        Directory.CreateDirectory(outputDirectory);
        foreach (var folder in new[] { "pages", "Img", "CSS", "JS", "Fonts", "Files" })
            Directory.CreateDirectory(Path.Combine(outputDirectory, folder));

        var selected = sourceItems
            .Where(x => x.IsSelected && x.State != LinkState.Skipped && UrlTools.IsLikelyPageUrl(x.Uri) &&
                        (string.IsNullOrWhiteSpace(x.ContentType) || x.ContentType.Contains("html", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        var items = selected.Where(x => x.State != LinkState.Downloaded).ToList();
        var pageMap = selected.ToDictionary(x => x.Url, x => MakePagePath(root, x.Uri, outputDirectory), StringComparer.OrdinalIgnoreCase);

        _completed = selected.Count(x => x.State == LinkState.Downloaded);
        _total = selected.Count;
        _failed = 0;
        _queued = items.Count;
        _activeDownloads = 0;
        _totalBytesDownloaded = 0;
        _totalBytesExpected = 0;
        _lastProgressTimestamp = 0;

        var minimumFreeBytes = Math.Max(0, minimumFreeDiskSpaceMb) * 1024L * 1024L;
        var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(outputDirectory))!);
        if (drive.AvailableFreeSpace < minimumFreeBytes)
            throw new IOException($"فضای آزاد دیسک کافی نیست. حداقل {minimumFreeDiskSpaceMb:N0} مگابایت لازم است.");

        if (items.Count == 0)
        {
            await SaveCheckpointAsync(root, sourceItems, outputDirectory, token).ConfigureAwait(false);
            Report(100, null, $"عملیات دانلود تمام شد: {_completed} از {_total} صفحه");
            return;
        }

        using var workers = new SemaphoreSlim(Math.Clamp(maxConcurrentDownloads, 1, 16));
        var tasks = items.Select(item => DownloadWorkerAsync(root, sourceItems, outputDirectory, item, pageMap, workers, delayMilliseconds, token)).ToList();
        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            foreach (var item in items.Where(x => x.State == LinkState.Downloading)) item.State = LinkState.Pending;
            await SaveCheckpointAsync(root, sourceItems, outputDirectory).ConfigureAwait(false);
            throw;
        }

        await SaveCheckpointAsync(root, sourceItems, outputDirectory, token).ConfigureAwait(false);
        var log = sourceItems.Select(x => $"{x.State}\t{x.Url}\t{x.Error}");
        await File.WriteAllLinesAsync(Path.Combine(outputDirectory, "download-log.txt"), log, token).ConfigureAwait(false);
        Report(100, null, $"عملیات دانلود تمام شد: {_completed} از {_total} صفحه");
    }

    private async Task DownloadWorkerAsync(Uri root, IReadOnlyCollection<DownloadItem> sourceItems, string outputDirectory,
        DownloadItem item, Dictionary<string, string> pageMap, SemaphoreSlim workers, int delayMilliseconds, CancellationToken token)
    {
        await workers.WaitAsync(token).ConfigureAwait(false);
        Interlocked.Decrement(ref _queued);
        Interlocked.Increment(ref _activeDownloads);
        try
        {
            item.State = LinkState.Downloading;
            Report(0, item.Url, $"در حال اتصال به صفحه {Volatile.Read(ref _completed) + 1} از {_total}");
            var pageFile = pageMap[item.Url];
            var parser = new HtmlParser();
            using var response = await _session.GetAsync(item.Uri, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var bytes = await ReadContentWithProgressAsync(response.Content, item.Uri, $"در حال دانلود صفحه {Volatile.Read(ref _completed) + 1} از {_total}", token).ConfigureAwait(false);
            var html = DecodeText(bytes, response.Content.Headers.ContentType?.CharSet);
            var document = await parser.ParseDocumentAsync(html, token).ConfigureAwait(false);
            RewritePageLinks(document, item.Uri, pageFile, pageMap);
            Report(55, item.Url, "در حال پردازش صفحه", bytes.LongLength, bytes.LongLength);
            await RewriteResourcesAsync(document, item.Uri, pageFile, item, token).ConfigureAwait(false);
            Report(85, item.Url, "در حال ذخیره صفحه");
            var output = document.DocumentElement?.OuterHtml ?? html;
            await File.WriteAllTextAsync(pageFile, "<!DOCTYPE html>\n" + output, new UTF8Encoding(false), token).ConfigureAwait(false);
            item.State = LinkState.Downloaded;
            item.Error = null;
            if (delayMilliseconds > 0) await Task.Delay(Math.Clamp(delayMilliseconds, 0, 60_000), token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            item.State = LinkState.Pending;
            throw;
        }
        catch (Exception ex)
        {
            item.State = LinkState.Failed;
            item.Error = ex.Message;
            Interlocked.Increment(ref _failed);
        }
        finally
        {
            Interlocked.Increment(ref _completed);
            Interlocked.Decrement(ref _activeDownloads);
            Report(100, item.Url, item.State == LinkState.Downloaded ? $"صفحه {_completed} از {_total} ذخیره شد" : $"دانلود ناموفق صفحه {_completed} از {_total}");
            try { await SaveCheckpointAsync(root, sourceItems, outputDirectory, token).ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            workers.Release();
        }
    }

    private void Report(int percent, string? url, string message, long bytes = 0, long totalBytes = 0)
    {
        if (_progress is null) return;
        var now = Stopwatch.GetTimestamp();
        var isFinal = percent >= 100 || message.Contains("ذخیره شد", StringComparison.OrdinalIgnoreCase) || message.Contains("تمام شد", StringComparison.OrdinalIgnoreCase);
        var last = Interlocked.Read(ref _lastProgressTimestamp);
        if (!isFinal && last != 0 && now - last < Stopwatch.Frequency / 10) return;
        Interlocked.Exchange(ref _lastProgressTimestamp, now);
        _progress.Report(new DownloadProgress(
            Volatile.Read(ref _completed), _total, message, Math.Clamp(percent, 0, 100), url, bytes, totalBytes,
            Volatile.Read(ref _failed), Interlocked.Read(ref _totalBytesDownloaded), Volatile.Read(ref _queued),
            Volatile.Read(ref _activeDownloads), Interlocked.Read(ref _totalBytesExpected), GetFreeDiskBytes()));
    }

    private async Task<byte[]> ReadContentWithProgressAsync(HttpContent content, Uri uri, string message, CancellationToken token)
    {
        var totalBytes = content.Headers.ContentLength ?? 0;
        if (totalBytes > 0) Interlocked.Add(ref _totalBytesExpected, totalBytes);
        await using var stream = await content.ReadAsStreamAsync(token).ConfigureAwait(false);
        await using var buffer = new MemoryStream(totalBytes is > 0 and <= int.MaxValue ? (int)totalBytes : 0);
        var chunk = new byte[32 * 1024];
        long bytes = 0;
        while (true)
        {
            var read = await stream.ReadAsync(chunk.AsMemory(), token).ConfigureAwait(false);
            if (read <= 0) break;
            await buffer.WriteAsync(chunk.AsMemory(0, read), token).ConfigureAwait(false);
            bytes += read;
            Interlocked.Add(ref _totalBytesDownloaded, read);
            var percent = totalBytes > 0 ? (int)Math.Clamp(bytes * 100L / totalBytes, 0, 100) : 0;
            Report(percent, uri.AbsoluteUri, message, bytes, totalBytes);
        }
        return buffer.ToArray();
    }

    private static string DecodeText(byte[] bytes, string? charset)
    {
        if (!string.IsNullOrWhiteSpace(charset))
        {
            try { return Encoding.GetEncoding(charset.Trim('"', '\''), EncoderFallback.ExceptionFallback, DecoderFallback.ReplacementFallback).GetString(bytes); }
            catch { }
        }
        return Encoding.UTF8.GetString(bytes);
    }

    private async Task SaveCheckpointAsync(Uri root, IReadOnlyCollection<DownloadItem> items, string outputDirectory, CancellationToken token = default)
    {
        await _checkpointGate.WaitAsync(token).ConfigureAwait(false);
        try { await ProjectStorage.SaveAsync(Path.Combine(outputDirectory, "links.json"), root, items, token).ConfigureAwait(false); }
        finally { _checkpointGate.Release(); }
    }

    private long GetFreeDiskBytes()
    {
        try { return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(_outputRoot))!).AvailableFreeSpace; }
        catch { return 0; }
    }

    private static string MakePagePath(Uri root, Uri page, string output)
    {
        if (page.AbsoluteUri.Equals(root.AbsoluteUri, StringComparison.OrdinalIgnoreCase) || (page.AbsolutePath is "/" or "" && string.IsNullOrEmpty(page.Query)))
            return Path.Combine(output, "index.html");
        var leaf = page.Segments.LastOrDefault()?.Trim('/') ?? "page";
        leaf = Uri.UnescapeDataString(leaf);
        leaf = UrlTools.CleanName(Path.GetFileNameWithoutExtension(leaf), "page");
        return Path.Combine(output, "pages", $"{leaf}-{UrlTools.Hash(page.AbsoluteUri)}.html");
    }

    private static void RewritePageLinks(IDocument document, Uri pageUri, string pageFile, Dictionary<string, string> pageMap)
    {
        foreach (var anchor in document.QuerySelectorAll("a[href]"))
        {
            var target = UrlTools.NormalizePageUrl(pageUri, anchor.GetAttribute("href"));
            if (target is not null && pageMap.TryGetValue(target.AbsoluteUri, out var targetFile)) anchor.SetAttribute("href", Relative(pageFile, targetFile));
        }
    }

    private async Task RewriteResourcesAsync(IDocument document, Uri pageUri, string pageFile, DownloadItem pageItem, CancellationToken token)
    {
        var targets = new (string Selector, string Attribute)[]
        {
            ("img[src]", "src"), ("img[data-src]", "data-src"), ("img[data-lazy-src]", "data-lazy-src"),
            ("script[src]", "src"), ("link[rel~='stylesheet'][href]", "href"), ("source[src]", "src"),
            ("source[data-src]", "data-src"), ("video[poster]", "poster"), ("audio[src]", "src"), ("input[type=image][src]", "src")
        };
        foreach (var (selector, attribute) in targets)
        foreach (var element in document.QuerySelectorAll(selector))
        {
            var raw = element.GetAttribute(attribute);
            if (!TryResourceUri(pageUri, raw, out var resource)) continue;
            var selection = FindResource(pageItem, resource);
            if (selection is { IsSelected: false }) continue;
            var local = await DownloadAssetAsync(resource, token).ConfigureAwait(false);
            if (local is not null)
            {
                var rewritten = Relative(pageFile, local);
                element.SetAttribute(attribute, rewritten);
                if (attribute is "data-src" or "data-lazy-src") element.SetAttribute("src", rewritten);
                if (selection is not null) { selection.State = LinkState.Downloaded; selection.Error = null; }
            }
            else if (selection is not null) { selection.State = LinkState.Failed; selection.Error = "منبع قابل دریافت نیست"; }
        }
        foreach (var image in document.QuerySelectorAll("img[srcset], source[srcset], img[data-lazy-srcset], source[data-lazy-srcset]"))
        {
            var srcsetAttribute = image.HasAttribute("srcset") ? "srcset" : "data-lazy-srcset";
            var rewritten = new List<string>();
            foreach (var part in (image.GetAttribute(srcsetAttribute) ?? "").Split(','))
            {
                var pieces = part.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (pieces.Length == 0 || !TryResourceUri(pageUri, pieces[0], out var resource)) continue;
                var selection = FindResource(pageItem, resource);
                if (selection is { IsSelected: false }) continue;
                var local = await DownloadAssetAsync(resource, token).ConfigureAwait(false);
                if (local is not null)
                {
                    rewritten.Add(Relative(pageFile, local) + (pieces.Length > 1 ? " " + pieces[1] : ""));
                    if (selection is not null) { selection.State = LinkState.Downloaded; selection.Error = null; }
                }
            }
            if (rewritten.Count > 0) { image.SetAttribute(srcsetAttribute, string.Join(", ", rewritten)); if (srcsetAttribute == "data-lazy-srcset") image.SetAttribute("srcset", string.Join(", ", rewritten)); }
        }
    }

    private async Task<string?> DownloadAssetAsync(Uri uri, CancellationToken token)
    {
        var gate = _assetGates.GetOrAdd(uri.AbsoluteUri, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (_assets.TryGetValue(uri.AbsoluteUri, out var existing) && File.Exists(existing)) return existing;
            using var response = await _session.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;
            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            var (folder, extension) = Classify(uri, contentType);
            var stem = UrlTools.CleanName(Path.GetFileNameWithoutExtension(uri.AbsolutePath), "asset");
            var file = Path.Combine(_outputRoot, folder, $"{stem}-{UrlTools.Hash(uri.AbsoluteUri)}{extension}");
            var bytes = await ReadContentWithProgressAsync(response.Content, uri, "در حال دانلود منبع صفحه", token).ConfigureAwait(false);
            if (folder == "CSS")
            {
                var css = DecodeText(bytes, response.Content.Headers.ContentType?.CharSet);
                css = await RewriteCssAsync(css, uri, file, token).ConfigureAwait(false);
                await File.WriteAllTextAsync(file, css, new UTF8Encoding(false), token).ConfigureAwait(false);
            }
            else await File.WriteAllBytesAsync(file, bytes, token).ConfigureAwait(false);
            _assets[uri.AbsoluteUri] = file;
            return file;
        }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
        finally { gate.Release(); }
    }

    private static ResourceItem? FindResource(DownloadItem page, Uri resource) => page.Resources.FirstOrDefault(item => item.Url.Equals(resource.AbsoluteUri, StringComparison.OrdinalIgnoreCase));

    private async Task<string> RewriteCssAsync(string css, Uri cssUri, string cssFile, CancellationToken token)
    {
        foreach (var value in CssUrlRegex().Matches(css).Select(m => m.Groups[2].Value).Distinct().ToList())
        {
            if (!TryResourceUri(cssUri, value, out var resource)) continue;
            var local = await DownloadAssetAsync(resource, token).ConfigureAwait(false);
            if (local is not null) css = css.Replace(value, Relative(cssFile, local), StringComparison.Ordinal);
        }
        return css;
    }

    private static (string Folder, string Extension) Classify(Uri uri, string contentType)
    {
        var ext = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
        if (contentType.Contains("css") || ext == ".css") return ("CSS", ".css");
        if (contentType.Contains("javascript") || ext is ".js" or ".mjs") return ("JS", ext is ".mjs" ? ".mjs" : ".js");
        if (contentType.StartsWith("image/") || ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".svg" or ".ico") return ("Img", string.IsNullOrEmpty(ext) ? MimeExtension(contentType) : ext);
        if (contentType.Contains("font") || ext is ".woff" or ".woff2" or ".ttf" or ".otf" or ".eot") return ("Fonts", string.IsNullOrEmpty(ext) ? ".bin" : ext);
        return ("Files", string.IsNullOrEmpty(ext) || ext.Length > 8 ? ".bin" : ext);
    }

    private static string MimeExtension(string mime) => mime.ToLowerInvariant() switch { "image/jpeg" => ".jpg", "image/png" => ".png", "image/gif" => ".gif", "image/webp" => ".webp", "image/svg+xml" => ".svg", _ => ".img" };

    private static bool TryResourceUri(Uri page, string? value, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith("data:", StringComparison.OrdinalIgnoreCase) || value.StartsWith("blob:", StringComparison.OrdinalIgnoreCase) || value.StartsWith('#')) return false;
        return Uri.TryCreate(page, WebUtility.HtmlDecode(value.Trim()), out uri!) && uri.Scheme is "http" or "https";
    }

    private static string Relative(string fromFile, string toFile) => Path.GetRelativePath(Path.GetDirectoryName(fromFile)!, toFile).Replace('\\', '/');

    [GeneratedRegex("""url\\(\\s*(['\\\"]?)([^'\\\")]+)\\1\\s*\\)""", RegexOptions.IgnoreCase)]
    private static partial Regex CssUrlRegex();
}
