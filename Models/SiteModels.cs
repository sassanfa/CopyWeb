using System.Text.Json.Serialization;

namespace CopyWeb.Models;

public enum LinkState
{
    Pending,
    Crawled,
    Selected,
    Downloading,
    Downloaded,
    Failed,
    Skipped
}

public sealed class DownloadItem
{
    public required string Url { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Depth { get; set; }
    public string ContentType { get; set; } = "text/html";
    public LinkState State { get; set; } = LinkState.Pending;
    public bool IsSelected { get; set; } = true;
    public string? Error { get; set; }

    [JsonIgnore]
    public Uri Uri => new(Url);
}

public sealed class CrawlOptions
{
    public int MaxDepth { get; init; } = 3;
    public int MaxPages { get; init; } = 500;
    public int DelayMilliseconds { get; init; } = 150;
    public bool IncludeSubdomains { get; init; } = true;
    public bool RespectRobotsTxt { get; init; } = true;
    public bool ReadSitemaps { get; init; } = true;
    public bool FollowCanonicalLinks { get; init; } = true;
    public bool RenderJavaScript { get; init; }
}

public sealed record CrawlProgress(int Processed, int Discovered, string Message);
public sealed record DownloadProgress(
    int Completed,
    int Total,
    string Message,
    int CurrentPercent = 0,
    string? CurrentUrl = null,
    long BytesDownloaded = 0,
    long TotalBytes = 0,
    int Failed = 0,
    long TotalBytesDownloaded = 0);

public enum ProxyKind
{
    Http,
    Https,
    Socks5
}

public sealed class ProxyOptions
{
    public bool Enabled { get; init; }
    public string Address { get; init; } = string.Empty;
    public int Port { get; init; } = 8080;
    public string? Username { get; init; }
    public string? Password { get; init; }
    public ProxyKind Kind { get; init; } = ProxyKind.Http;
    public int TimeoutSeconds { get; init; } = 45;
    public int RetryCount { get; init; } = 2;
    public int RetryDelayMilliseconds { get; init; } = 750;
    public string UserAgent { get; init; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 CopyWeb/1.0";
    public Dictionary<string, string> Headers { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string CookieHeader { get; init; } = string.Empty;
}

public sealed class SavedLinkProject
{
    public string RootUrl { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public List<DownloadItem> Links { get; set; } = [];
    public ProxySnapshot? Proxy { get; set; }
}

public sealed class ProxySnapshot
{
    public bool Enabled { get; set; }
    public ProxyKind Kind { get; set; } = ProxyKind.Http;
    public string Address { get; set; } = string.Empty;
    public int Port { get; set; } = 8080;
    public string? EncryptedUsername { get; set; }
    public string? EncryptedPassword { get; set; }
}

public enum ActivitySeverity
{
    Success,
    Info,
    Warning,
    Error
}

public sealed class ActivityLogEntry
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public ActivitySeverity Severity { get; set; } = ActivitySeverity.Info;
    public string Url { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
}

public sealed record BrowserCookie(string Name, string Value, string Domain, string Path, DateTime? Expires);
