using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using CopyWeb.Models;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using LinkState = CopyWeb.Models.LinkState;

namespace CopyWeb.Services;

public sealed partial class SiteDownloader(SiteSession session)
{
    private readonly SiteSession _session = session;
    private readonly HtmlParser _parser = new();
    private readonly Dictionary<string, string> _assets = new(StringComparer.OrdinalIgnoreCase);
    private string _outputRoot = string.Empty;

    public async Task DownloadAsync(
        Uri root,
        IReadOnlyCollection<DownloadItem> sourceItems,
        string outputDirectory,
        IProgress<DownloadProgress>? progress,
        CancellationToken token,
        int delayMilliseconds = 0)
    {
        _outputRoot = outputDirectory;
        Directory.CreateDirectory(outputDirectory);
        foreach (var folder in new[] { "pages", "Img", "CSS", "JS", "Fonts", "Files" })
            Directory.CreateDirectory(Path.Combine(outputDirectory, folder));

        var selected = sourceItems.Where(x => x.IsSelected).ToList();
        var items = selected.Where(x => x.State != LinkState.Downloaded).ToList();
        var pageMap = selected.ToDictionary(
            x => x.Url,
            x => MakePagePath(root, x.Uri, outputDirectory),
            StringComparer.OrdinalIgnoreCase);

        var completed = selected.Count(x => x.State == LinkState.Downloaded);
        foreach (var item in items)
        {
            token.ThrowIfCancellationRequested();
            item.State = LinkState.Downloading;
            progress?.Report(new DownloadProgress(completed, selected.Count, item.Url, 5, item.Url, Failed: sourceItems.Count(x => x.State == LinkState.Failed)));
            progress?.Report(new DownloadProgress(completed, items.Count, $"در حال دانلود: {item.Url}"));
            try
            {
                using var response = await _session.GetAsync(item.Uri, HttpCompletionOption.ResponseContentRead, token);
                response.EnsureSuccessStatusCode();
                progress?.Report(new DownloadProgress(completed, selected.Count, item.Url, 25, item.Url, response.Content.Headers.ContentLength ?? 0, response.Content.Headers.ContentLength ?? 0, sourceItems.Count(x => x.State == LinkState.Failed)));
                var html = await response.Content.ReadAsStringAsync(token);
                progress?.Report(new DownloadProgress(completed, selected.Count, item.Url, 40, item.Url, response.Content.Headers.ContentLength ?? 0, response.Content.Headers.ContentLength ?? 0, sourceItems.Count(x => x.State == LinkState.Failed)));
                var document = await _parser.ParseDocumentAsync(html, token);
                var pageFile = pageMap[item.Url];

                RewritePageLinks(document, item.Uri, pageFile, pageMap);
                progress?.Report(new DownloadProgress(completed, selected.Count, item.Url, 55, item.Url));
                await RewriteResourcesAsync(document, item.Uri, pageFile, token);
                progress?.Report(new DownloadProgress(completed, selected.Count, item.Url, 85, item.Url));

                var output = document.DocumentElement?.OuterHtml ?? html;
                await File.WriteAllTextAsync(pageFile, "<!DOCTYPE html>\n" + output, new UTF8Encoding(false), token);
                item.State = LinkState.Downloaded;
                item.Error = null;
            }
            catch (OperationCanceledException)
            {
                item.State = LinkState.Pending;
                await SaveCheckpointAsync(root, sourceItems, outputDirectory);
                throw;
            }
            catch (Exception ex)
            {
                item.State = LinkState.Failed;
                item.Error = ex.Message;
            }
            completed++;
            progress?.Report(new DownloadProgress(completed, selected.Count, item.Url, 100, item.Url, Failed: sourceItems.Count(x => x.State == LinkState.Failed)));
            await SaveCheckpointAsync(root, sourceItems, outputDirectory, token);
            progress?.Report(new DownloadProgress(completed, items.Count, $"{completed} از {items.Count} صفحه ذخیره شد"));
            if (delayMilliseconds > 0) await Task.Delay(Math.Clamp(delayMilliseconds, 0, 60_000), token);
        }

        await SaveCheckpointAsync(root, sourceItems, outputDirectory, token);
        var log = sourceItems.Select(x => $"{x.State}\t{x.Url}\t{x.Error}");
        await File.WriteAllLinesAsync(Path.Combine(outputDirectory, "download-log.txt"), log, token);
    }

    private static Task SaveCheckpointAsync(Uri root, IReadOnlyCollection<DownloadItem> items, string outputDirectory, CancellationToken token = default) =>
        ProjectStorage.SaveAsync(Path.Combine(outputDirectory, "links.json"), root, items, token);

    private static string MakePagePath(Uri root, Uri page, string output)
    {
        if (page.AbsoluteUri.Equals(root.AbsoluteUri, StringComparison.OrdinalIgnoreCase) ||
            (page.AbsolutePath is "/" or "" && string.IsNullOrEmpty(page.Query)))
            return Path.Combine(output, "index.html");

        var leaf = page.Segments.LastOrDefault()?.Trim('/') ?? "page";
        leaf = UrlTools.CleanName(Path.GetFileNameWithoutExtension(leaf), "page");
        return Path.Combine(output, "pages", $"{leaf}-{UrlTools.Hash(page.AbsoluteUri)}.html");
    }

    private static void RewritePageLinks(IDocument document, Uri pageUri, string pageFile, Dictionary<string, string> pageMap)
    {
        foreach (var anchor in document.QuerySelectorAll("a[href]"))
        {
            var target = UrlTools.NormalizePageUrl(pageUri, anchor.GetAttribute("href"));
            if (target is not null && pageMap.TryGetValue(target.AbsoluteUri, out var targetFile))
                anchor.SetAttribute("href", Relative(pageFile, targetFile));
        }
    }

    private async Task RewriteResourcesAsync(IDocument document, Uri pageUri, string pageFile, CancellationToken token)
    {
        var targets = new (string Selector, string Attribute)[]
        {
            ("img[src]", "src"), ("script[src]", "src"), ("link[href]", "href"),
            ("source[src]", "src"), ("video[poster]", "poster"), ("audio[src]", "src"),
            ("input[type=image][src]", "src")
        };

        foreach (var (selector, attribute) in targets)
        foreach (var element in document.QuerySelectorAll(selector))
        {
            var raw = element.GetAttribute(attribute);
            if (!TryResourceUri(pageUri, raw, out var resource)) continue;
            var local = await DownloadAssetAsync(resource, token);
            if (local is not null) element.SetAttribute(attribute, Relative(pageFile, local));
        }

        foreach (var image in document.QuerySelectorAll("img[srcset], source[srcset]"))
        {
            var rewritten = new List<string>();
            foreach (var part in (image.GetAttribute("srcset") ?? "").Split(','))
            {
                var pieces = part.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (pieces.Length == 0 || !TryResourceUri(pageUri, pieces[0], out var resource)) continue;
                var local = await DownloadAssetAsync(resource, token);
                if (local is not null) rewritten.Add(Relative(pageFile, local) + (pieces.Length > 1 ? " " + pieces[1] : ""));
            }
            if (rewritten.Count > 0) image.SetAttribute("srcset", string.Join(", ", rewritten));
        }
    }

    private async Task<string?> DownloadAssetAsync(Uri uri, CancellationToken token)
    {
        if (_assets.TryGetValue(uri.AbsoluteUri, out var existing)) return existing;
        try
        {
            using var response = await _session.GetAsync(uri, HttpCompletionOption.ResponseContentRead, token);
            if (!response.IsSuccessStatusCode) return null;
            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            var (folder, extension) = Classify(uri, contentType);
            var stem = UrlTools.CleanName(Path.GetFileNameWithoutExtension(uri.AbsolutePath), "asset");
            var file = Path.Combine(_outputRoot, folder, $"{stem}-{UrlTools.Hash(uri.AbsoluteUri)}{extension}");
            _assets[uri.AbsoluteUri] = file;

            if (folder == "CSS")
            {
                var css = await response.Content.ReadAsStringAsync(token);
                css = await RewriteCssAsync(css, uri, file, token);
                await File.WriteAllTextAsync(file, css, new UTF8Encoding(false), token);
            }
            else
            {
                var bytes = await response.Content.ReadAsByteArrayAsync(token);
                await File.WriteAllBytesAsync(file, bytes, token);
            }
            return file;
        }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
    }

    private async Task<string> RewriteCssAsync(string css, Uri cssUri, string cssFile, CancellationToken token)
    {
        var values = CssUrlRegex().Matches(css).Select(m => m.Groups[2].Value).Distinct().ToList();
        foreach (var value in values)
        {
            if (!TryResourceUri(cssUri, value, out var resource)) continue;
            var local = await DownloadAssetAsync(resource, token);
            if (local is not null) css = css.Replace(value, Relative(cssFile, local), StringComparison.Ordinal);
        }
        return css;
    }

    private static (string Folder, string Extension) Classify(Uri uri, string contentType)
    {
        var ext = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
        if (contentType.Contains("css") || ext == ".css") return ("CSS", ".css");
        if (contentType.Contains("javascript") || ext is ".js" or ".mjs") return ("JS", ext is ".mjs" ? ".mjs" : ".js");
        if (contentType.StartsWith("image/") || ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".svg" or ".ico")
            return ("Img", string.IsNullOrEmpty(ext) ? MimeExtension(contentType) : ext);
        if (contentType.Contains("font") || ext is ".woff" or ".woff2" or ".ttf" or ".otf" or ".eot")
            return ("Fonts", string.IsNullOrEmpty(ext) ? ".bin" : ext);
        return ("Files", string.IsNullOrEmpty(ext) || ext.Length > 8 ? ".bin" : ext);
    }

    private static string MimeExtension(string mime) => mime.ToLowerInvariant() switch
    {
        "image/jpeg" => ".jpg", "image/png" => ".png", "image/gif" => ".gif",
        "image/webp" => ".webp", "image/svg+xml" => ".svg", _ => ".img"
    };

    private static bool TryResourceUri(Uri page, string? value, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("blob:", StringComparison.OrdinalIgnoreCase) || value.StartsWith('#')) return false;
        return Uri.TryCreate(page, WebUtility.HtmlDecode(value.Trim()), out uri!) && uri.Scheme is "http" or "https";
    }

    private static string Relative(string fromFile, string toFile) =>
        Path.GetRelativePath(Path.GetDirectoryName(fromFile)!, toFile).Replace('\\', '/');

    [GeneratedRegex("""url\(\s*(['\"]?)([^'\")]+)\1\s*\)""", RegexOptions.IgnoreCase)]
    private static partial Regex CssUrlRegex();
}
