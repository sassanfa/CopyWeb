using AngleSharp.Html.Parser;
using CopyWeb.Models;
using System.Net;
using LinkState = CopyWeb.Models.LinkState;

namespace CopyWeb.Services;

public sealed class SiteCrawler(SiteSession session)
{
    private readonly HtmlParser _parser = new();
    private readonly SiteSession _session = session;

    public async Task<List<DownloadItem>> CrawlAsync(
        Uri root,
        CrawlOptions options,
        Func<Uri, CancellationToken, Task<IReadOnlyList<BrowserCookie>?>> captchaHandler,
        IProgress<CrawlProgress>? progress,
        CancellationToken token,
        IReadOnlyCollection<DownloadItem>? resumeItems = null,
        Func<IReadOnlyCollection<DownloadItem>, Task>? checkpoint = null)
    {
        var found = new Dictionary<string, DownloadItem>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<(Uri Uri, int Depth)>();
        var robots = options.RespectRobotsTxt ? await ReadRobotsAsync(root, token) : [];
        var rootUri = new UriBuilder(root) { Fragment = string.Empty }.Uri;
        if (resumeItems is { Count: > 0 })
        {
            foreach (var saved in resumeItems)
            {
                if (!Uri.TryCreate(saved.Url, UriKind.Absolute, out var savedUri)) continue;
                found[savedUri.AbsoluteUri] = saved;
                if (saved.State is LinkState.Pending or LinkState.Failed or LinkState.Downloading)
                    queue.Enqueue((savedUri, saved.Depth));
            }
        }
        if (!found.ContainsKey(rootUri.AbsoluteUri))
        {
            found[rootUri.AbsoluteUri] = new DownloadItem { Url = rootUri.AbsoluteUri, Depth = 0 };
            queue.Enqueue((rootUri, 0));
        }

        var processed = 0;
        while (queue.Count > 0 && found.Count <= options.MaxPages)
        {
            token.ThrowIfCancellationRequested();
            var (current, depth) = queue.Dequeue();
            var item = found[current.AbsoluteUri];

            if (IsDisallowed(current, robots))
            {
                item.State = LinkState.Skipped;
                item.Error = "Blocked by robots.txt";
                continue;
            }

            try
            {
                progress?.Report(new CrawlProgress(processed, found.Count, $"در حال بررسی: {current}"));
                using var response = await GetWithCaptchaAsync(current, captchaHandler, token);
                item.ContentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
                if (!response.IsSuccessStatusCode)
                {
                    item.State = LinkState.Failed;
                    item.Error = $"HTTP {(int)response.StatusCode}";
                    continue;
                }

                if (!item.ContentType.Contains("html", StringComparison.OrdinalIgnoreCase))
                {
                    item.State = LinkState.Skipped;
                    continue;
                }

                var html = await response.Content.ReadAsStringAsync(token);
                var document = await _parser.ParseDocumentAsync(html, token);
                item.Title = document.Title?.Trim() ?? string.Empty;
                item.State = LinkState.Crawled;
                processed++;

                if (depth >= options.MaxDepth) continue;
                foreach (var anchor in document.QuerySelectorAll("a[href]"))
                {
                    var next = UrlTools.NormalizePageUrl(current, anchor.GetAttribute("href"));
                    if (next is null || !UrlTools.IsInternal(next, root, options.IncludeSubdomains)) continue;
                    if (found.ContainsKey(next.AbsoluteUri)) continue;
                    if (found.Count >= options.MaxPages) break;

                    found[next.AbsoluteUri] = new DownloadItem
                    {
                        Url = next.AbsoluteUri,
                        Depth = depth + 1,
                        State = LinkState.Pending
                    };
                    queue.Enqueue((next, depth + 1));
                }
            }
            catch (OperationCanceledException)
            {
                item.State = LinkState.Pending;
                if (checkpoint is not null) await checkpoint(found.Values.ToList());
                throw;
            }
            catch (Exception ex)
            {
                item.State = LinkState.Failed;
                item.Error = ex.Message;
            }

            progress?.Report(new CrawlProgress(processed, found.Count, $"{found.Count} لینک مرتبط پیدا شد"));
            if (checkpoint is not null) await checkpoint(found.Values.ToList());
            if (options.DelayMilliseconds > 0)
                await Task.Delay(options.DelayMilliseconds, token);
        }

        return found.Values.OrderBy(x => x.Depth).ThenBy(x => x.Url).ToList();
    }

    private async Task<HttpResponseMessage> GetWithCaptchaAsync(
        Uri uri,
        Func<Uri, CancellationToken, Task<IReadOnlyList<BrowserCookie>?>> captchaHandler,
        CancellationToken token)
    {
        var response = await _session.GetAsync(uri, HttpCompletionOption.ResponseContentRead, token);
        if (!await LooksLikeCaptchaAsync(response, token)) return response;

        response.Dispose();
        var cookies = await captchaHandler(uri, token);
        if (cookies is null) throw new OperationCanceledException("حل کپچا توسط کاربر لغو شد.", token);
        _session.ImportCookies(cookies);
        return await _session.GetAsync(uri, HttpCompletionOption.ResponseContentRead, token);
    }

    private static async Task<bool> LooksLikeCaptchaAsync(HttpResponseMessage response, CancellationToken token)
    {
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
            return true;
        var type = response.Content.Headers.ContentType?.MediaType;
        if (type?.Contains("html", StringComparison.OrdinalIgnoreCase) != true) return false;
        var html = await response.Content.ReadAsStringAsync(token);
        string[] markers = ["captcha", "g-recaptcha", "hcaptcha", "cf-chl-", "challenge-platform", "verify you are human", "تأیید کنید ربات نیستید"];
        return markers.Any(x => html.Contains(x, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<List<string>> ReadRobotsAsync(Uri root, CancellationToken token)
    {
        var rules = new List<string>();
        try
        {
            var uri = new Uri(root.GetLeftPart(UriPartial.Authority) + "/robots.txt");
            using var response = await _session.GetAsync(uri, token);
            if (!response.IsSuccessStatusCode) return rules;
            var lines = (await response.Content.ReadAsStringAsync(token)).Split('\n');
            var applies = false;
            foreach (var raw in lines)
            {
                var line = raw.Split('#')[0].Trim();
                if (line.StartsWith("User-agent:", StringComparison.OrdinalIgnoreCase))
                    applies = line[11..].Trim() == "*";
                else if (applies && line.StartsWith("Disallow:", StringComparison.OrdinalIgnoreCase))
                {
                    var path = line[9..].Trim();
                    if (!string.IsNullOrEmpty(path)) rules.Add(path);
                }
            }
        }
        catch { }
        return rules;
    }

    private static bool IsDisallowed(Uri uri, List<string> rules) =>
        rules.Any(rule => uri.PathAndQuery.StartsWith(rule, StringComparison.Ordinal));
}
