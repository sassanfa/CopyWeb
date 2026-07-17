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
}

public sealed record CrawlProgress(int Processed, int Discovered, string Message);
public sealed record DownloadProgress(
    int Completed,
    int Total,
    string Message,
    int CurrentPercent = 0,
    string? CurrentUrl = null);

public sealed class ProxyOptions
{
    public bool Enabled { get; init; }
    public string Address { get; init; } = string.Empty;
    public int Port { get; init; } = 8080;
    public string? Username { get; init; }
    public string? Password { get; init; }
}

public sealed class SavedLinkProject
{
    public string RootUrl { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public List<DownloadItem> Links { get; set; } = [];
}

public sealed record BrowserCookie(string Name, string Value, string Domain, string Path, DateTime? Expires);
