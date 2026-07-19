using AngleSharp.Html.Parser;
using CopyWeb.Models;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
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
        Func<IReadOnlyCollection<DownloadItem>, Task>? checkpoint = null,
        Func<Uri, CancellationToken, Task<string?>>? renderHandler = null)
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

        if (options.ReadSitemaps)
        {
            foreach (var sitemapPage in await ReadSitemapPagesAsync(root, token))
            {
                if (found.Count >= options.MaxPages || !UrlTools.IsInternal(sitemapPage, root, options.IncludeSubdomains) || found.ContainsKey(sitemapPage.AbsoluteUri)) continue;
                found[sitemapPage.AbsoluteUri] = new DownloadItem { Url = sitemapPage.AbsoluteUri, Depth = 1, State = LinkState.Pending };
                queue.Enqueue((sitemapPage, 1));
            }
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
                if (options.RenderJavaScript && renderHandler is not null)
                {
                    var rendered = await renderHandler(current, token);
                    if (!string.IsNullOrWhiteSpace(rendered)) html = rendered;
                }
                var document = await _parser.ParseDocumentAsync(html, token);
                item.Title = document.Title?.Trim() ?? string.Empty;
                var previousSelections = item.Resources.ToDictionary(x => x.Url, x => x.IsSelected, StringComparer.OrdinalIgnoreCase);
                item.Resources = DiscoverResources(document, current)
                    .Select(resource =>
                    {
                        if (previousSelections.TryGetValue(resource.Url, out var selected)) resource.IsSelected = selected;
                        return resource;
                    })
                    .ToList();
                item.State = LinkState.Crawled;
                processed++;

                if (options.FollowCanonicalLinks)
                {
                    var canonical = UrlTools.NormalizePageUrl(current, document.QuerySelector("link[rel~='canonical']")?.GetAttribute("href"));
                    if (canonical is not null && UrlTools.IsInternal(canonical, root, options.IncludeSubdomains) && !found.ContainsKey(canonical.AbsoluteUri) && found.Count < options.MaxPages)
                    {
                        found[canonical.AbsoluteUri] = new DownloadItem { Url = canonical.AbsoluteUri, Depth = depth, State = LinkState.Pending };
                        queue.Enqueue((canonical, depth));
                    }
                }

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
        var type = response.Content.Headers.ContentType?.MediaType;
        if (type?.Contains("html", StringComparison.OrdinalIgnoreCase) != true) return false;
        var html = await response.Content.ReadAsStringAsync(token);
        // Do not trigger the browser merely because a site includes a generic
        // captcha script in its bundle. Only explicit challenge markers count.
        var visibleHtml = Regex.Replace(html, "<(script|style|noscript)\\b[^>]*>.*?</\\1>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        string[] markers = ["g-recaptcha", "hcaptcha", "cf-chl-", "challenge-platform", "challenge-form", "data-sitekey", "turnstile", "verify you are human", "checking your browser", "just a moment", "تأیید کنید ربات نیستید"];
        var explicitChallenge = markers.Any(x => visibleHtml.Contains(x, StringComparison.OrdinalIgnoreCase));
        var blockedStatus = response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable;
        return explicitChallenge || (blockedStatus && visibleHtml.Contains("challenge", StringComparison.OrdinalIgnoreCase));
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

    private async Task<List<Uri>> ReadSitemapPagesAsync(Uri root, CancellationToken token)
    {
        var result = new List<Uri>();
        var sitemapUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            new Uri(root.GetLeftPart(UriPartial.Authority) + "/sitemap.xml").AbsoluteUri
        };
        var pendingSitemaps = new Queue<string>(sitemapUrls);
        while (pendingSitemaps.Count > 0)
        {
            var sitemapUrl = pendingSitemaps.Dequeue();
            try
            {
                using var response = await _session.GetAsync(new Uri(sitemapUrl), token);
                if (!response.IsSuccessStatusCode) continue;
                var xml = await response.Content.ReadAsStringAsync(token);
                var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
                foreach (var loc in document.Descendants().Where(x => x.Name.LocalName.Equals("loc", StringComparison.OrdinalIgnoreCase)))
                {
                    if (Uri.TryCreate(loc.Value.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
                    {
                        if (!UrlTools.IsLikelyPageUrl(uri)) continue;
                        if (document.Root?.Name.LocalName.Equals("sitemapindex", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            if (sitemapUrls.Add(uri.AbsoluteUri)) pendingSitemaps.Enqueue(uri.AbsoluteUri);
                        }
                        else if (!result.Any(x => x.AbsoluteUri.Equals(uri.AbsoluteUri, StringComparison.OrdinalIgnoreCase)))
                            result.Add(uri);
                    }
                }
            }
            catch
            {
                // A missing or malformed sitemap should not prevent normal crawling.
            }
        }
        return result;
    }

    private static bool IsDisallowed(Uri uri, List<string> rules) =>
        rules.Any(rule => uri.PathAndQuery.StartsWith(rule, StringComparison.Ordinal));

    private static List<ResourceItem> DiscoverResources(AngleSharp.Dom.IDocument document, Uri page)
    {
        var result = new Dictionary<string, ResourceItem>(StringComparer.OrdinalIgnoreCase);

        void Add(string? raw, ResourceKind kind)
        {
            if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
                raw.StartsWith("blob:", StringComparison.OrdinalIgnoreCase) || raw.StartsWith('#')) return;
            if (!Uri.TryCreate(page, WebUtility.HtmlDecode(raw.Trim()), out var uri) || uri.Scheme is not ("http" or "https")) return;
            var normalized = UrlTools.NormalizeResourceUri(uri);
            var key = UrlTools.ResourceCacheKey(normalized);
            if (!result.ContainsKey(key)) result[key] = new ResourceItem { Url = normalized.AbsoluteUri, Kind = kind };
        }

        var selectors = new (string Selector, string Attribute, ResourceKind Kind)[]
        {
            ("img[src]", "src", ResourceKind.Image), ("img[data-src]", "data-src", ResourceKind.Image),
            ("img[data-lazy-src]", "data-lazy-src", ResourceKind.Image), ("script[src]", "src", ResourceKind.Script),
            ("link[rel~='stylesheet'][href]", "href", ResourceKind.Stylesheet), ("link[rel~='icon'][href]", "href", ResourceKind.Image),
            ("source[src]", "src", ResourceKind.Media), ("source[data-src]", "data-src", ResourceKind.Media),
            ("video[poster]", "poster", ResourceKind.Media), ("audio[src]", "src", ResourceKind.Media),
            ("input[type=image][src]", "src", ResourceKind.Image)
        };
        foreach (var (selector, attribute, kind) in selectors)
        foreach (var element in document.QuerySelectorAll(selector)) Add(element.GetAttribute(attribute), kind);

        foreach (var element in document.QuerySelectorAll("img[srcset], source[srcset], img[data-lazy-srcset], source[data-lazy-srcset]"))
        {
            var attribute = element.HasAttribute("srcset") ? "srcset" : "data-lazy-srcset";
            foreach (var part in (element.GetAttribute(attribute) ?? string.Empty).Split(','))
                Add(part.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(), ResourceKind.Image);
        }
        return result.Values.ToList();
    }
}
