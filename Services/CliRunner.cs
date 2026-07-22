using CopyWeb.Models;

namespace CopyWeb.Services;

public static class CliRunner
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Any(x => x.Equals("--help", StringComparison.OrdinalIgnoreCase) || x.Equals("-h", StringComparison.OrdinalIgnoreCase)))
        {
            PrintHelp();
            return 0;
        }
        if (args.Any(x => x.Equals("--self-test", StringComparison.OrdinalIgnoreCase))) return RunSelfTest();

        var url = Value(args, "--url");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var root) || root.Scheme is not ("http" or "https"))
        {
            Console.Error.WriteLine("A valid --url http(s) address is required.");
            PrintHelp();
            return 2;
        }

        var output = Value(args, "--output") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CopyWeb", root.Host);
        var depth = IntValue(args, "--depth", 3, 0, 20);
        var maxPages = IntValue(args, "--max-pages", 500, 1, 100_000);
        var concurrency = IntValue(args, "--concurrency", 4, 1, 16);
        var speed = IntValue(args, "--speed-kbps", 0, 0, 1_000_000);
        var perDomain = IntValue(args, "--per-domain", 2, 1, 32);
        var timeout = IntValue(args, "--timeout", 45, 5, 600);
        var retries = IntValue(args, "--retry", 2, 0, 10);
        var delay = IntValue(args, "--delay-ms", 150, 0, 60_000);
        var proxyAddress = Value(args, "--proxy");
        var proxyKind = ParseProxyKind(Value(args, "--proxy-kind"));
        var proxyPort = IntValue(args, "--proxy-port", proxyKind == ProxyKind.Socks5 ? 1080 : 8080, 1, 65535);
        var proxyUser = Value(args, "--proxy-user");
        var proxyPassword = Value(args, "--proxy-password");
        Directory.CreateDirectory(output);
        Console.WriteLine($"Scanning {root}");
        using var session = new SiteSession(new ProxyOptions { Enabled = !string.IsNullOrWhiteSpace(proxyAddress), Address = proxyAddress ?? string.Empty, Port = proxyPort, Kind = proxyKind, Username = proxyUser, Password = proxyPassword, RetryCount = retries, TimeoutSeconds = timeout, MaxDownloadSpeedKbps = speed });
        var crawler = new SiteCrawler(session);
        var crawlProgress = new Progress<CrawlProgress>(p => Console.WriteLine($"SCAN {p.Processed}/{p.Discovered} {p.Message}"));
        IReadOnlyCollection<DownloadItem>? resume = null;
        var checkpoint = Path.Combine(output, "links.json");
        if (args.Any(x => x.Equals("--resume", StringComparison.OrdinalIgnoreCase)) && File.Exists(checkpoint))
        {
            try { resume = (await ProjectStorage.LoadAsync(checkpoint)).Links; Console.WriteLine("Resuming from links.json"); } catch { }
        }
        var links = await crawler.CrawlAsync(root, new CrawlOptions { MaxDepth = depth, MaxPages = maxPages, IncludeSubdomains = true, RespectRobotsTxt = true, ReadSitemaps = true, FollowCanonicalLinks = true, DelayMilliseconds = delay },
            (_, _) => Task.FromResult<IReadOnlyList<BrowserCookie>?>(null), crawlProgress, CancellationToken.None, resume);
        await ProjectStorage.SaveAsync(Path.Combine(output, "links.json"), root, links);
        var downloader = new SiteDownloader(session);
        var progress = new Progress<DownloadProgress>(p => Console.WriteLine($"DOWNLOAD {p.Completed}/{p.Total} {p.CurrentPercent}% {p.CurrentUrl ?? p.Message}"));
        await downloader.DownloadAsync(root, links, output, progress, CancellationToken.None, delay, concurrency, 512, speed, perDomain);
        Console.WriteLine($"Completed. Output: {output}");
        return 0;
    }

    private static int RunSelfTest()
    {
        var uri = UrlTools.NormalizeResourceUri(new Uri("https://example.com/a.png#one"));
        if (uri.AbsoluteUri != "https://example.com/a.png") return 1;
        var imageA = UrlTools.ResourceCacheKey(new Uri("https://example.com/assets/a.png?v=1"));
        var imageB = UrlTools.ResourceCacheKey(new Uri("https://example.com/assets/a.png?v=2#hero"));
        if (!imageA.Equals(imageB, StringComparison.OrdinalIgnoreCase)) return 1;
        if (UrlTools.Hash("copyweb").Length != 10) return 1;
        var temp = Path.Combine(Path.GetTempPath(), "copyweb-self-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(temp); File.WriteAllText(Path.Combine(temp, "links.json"), "{}"); File.WriteAllText(Path.Combine(temp, "sample.txt"), "ok");
            File.WriteAllText(Path.Combine(temp, "index.html"), "<a href=\"missing.html\">broken</a>");
            if (OfflineLinkChecker.Scan(temp).Count != 1) return 1;
            var archive = temp + ".copyweb.zip"; ProjectArchiveService.CreateBackupAsync(Path.Combine(temp, "links.json"), archive).GetAwaiter().GetResult();
            var restored = temp + "-restored"; ProjectArchiveService.RestoreBackup(archive, restored);
            if (!File.Exists(Path.Combine(restored, "sample.txt"))) return 1;
        }
        finally { try { if (Directory.Exists(temp)) Directory.Delete(temp, true); if (File.Exists(temp + ".copyweb.zip")) File.Delete(temp + ".copyweb.zip"); if (Directory.Exists(temp + "-restored")) Directory.Delete(temp + "-restored", true); } catch { } }
        Console.WriteLine("CopyWeb self-test passed.");
        return 0;
    }

    private static string? Value(string[] args, string name)
    {
        var index = Array.FindIndex(args, x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static int IntValue(string[] args, string name, int fallback, int minimum, int maximum) =>
        int.TryParse(Value(args, name), out var value) ? Math.Clamp(value, minimum, maximum) : fallback;

    private static ProxyKind ParseProxyKind(string? value) => value?.ToLowerInvariant() switch
    {
        "https" => ProxyKind.Https,
        "socks5" or "socks" => ProxyKind.Socks5,
        _ => ProxyKind.Http
    };

    private static void PrintHelp()
    {
        Console.WriteLine("CopyWeb CLI 1.3.1 - headless website archiver");
        Console.WriteLine();
        Console.WriteLine("Basic:");
        Console.WriteLine("  CopyWeb.exe --cli --url https://example.com --output C:\\Sites\\example");
        Console.WriteLine("  --depth N              maximum crawl depth (default: 3)");
        Console.WriteLine("  --max-pages N          maximum pages (default: 500)");
        Console.WriteLine("  --concurrency N        global page workers, 1-16 (default: 4)");
        Console.WriteLine("  --per-domain N         simultaneous requests per host (default: 2)");
        Console.WriteLine("  --speed-kbps N         aggregate speed limit; 0 = unlimited");
        Console.WriteLine("  --delay-ms N           delay between requests");
        Console.WriteLine("  --resume               reuse output\\links.json checkpoint");
        Console.WriteLine();
        Console.WriteLine("Network:");
        Console.WriteLine("  --proxy HOST           HTTP/HTTPS/SOCKS5 proxy address");
        Console.WriteLine("  --proxy-kind TYPE      http | https | socks5 (default: http)");
        Console.WriteLine("  --proxy-port N         proxy port");
        Console.WriteLine("  --proxy-user USER      optional proxy username");
        Console.WriteLine("  --proxy-password PASS  optional proxy password");
        Console.WriteLine("  --timeout N            request timeout in seconds");
        Console.WriteLine("  --retry N              transient retry count");
        Console.WriteLine();
        Console.WriteLine("Diagnostics:");
        Console.WriteLine("  --self-test            run URL, dedup and backup/restore tests");
        Console.WriteLine("  --help                 show this help");
    }
}
