namespace CopyWeb.Models;

public sealed class AppSettings
{
    public string ThemePreset { get; set; } = "شب بنفش";
    public int PrimaryColorArgb { get; set; } = Color.FromArgb(111, 82, 255).ToArgb();
    public int BackgroundColorArgb { get; set; } = Color.FromArgb(8, 12, 37).ToArgb();
    public int SurfaceColorArgb { get; set; } = Color.FromArgb(20, 26, 57).ToArgb();
    public bool SaveDetailedLogs { get; set; } = true;
    public string Language { get; set; } = "fa";
    public ProxyKind ProxyKind { get; set; } = ProxyKind.Http;
    public bool ProxyEnabled { get; set; }
    public string ProxyAddress { get; set; } = string.Empty;
    public int ProxyPort { get; set; } = 8080;
    public string? EncryptedProxyUsername { get; set; }
    public string? EncryptedProxyPassword { get; set; }
    public int RequestTimeoutSeconds { get; set; } = 45;
    public int RetryCount { get; set; } = 2;
    public int DelayMilliseconds { get; set; } = 150;
    /// <summary>Maximum number of page downloads running at the same time.</summary>
    public int MaxConcurrentDownloads { get; set; } = 4;
    /// <summary>Minimum free space required before starting a download (MB).</summary>
    public long MinimumFreeDiskSpaceMb { get; set; } = 512;
    /// <summary>Maximum aggregate download speed in KB/s; zero means unlimited.</summary>
    public int MaxDownloadSpeedKbps { get; set; }
    /// <summary>Maximum simultaneous page requests per host.</summary>
    public int MaxConnectionsPerDomain { get; set; } = 2;
    public bool CompactMode { get; set; }
    public bool EnableLocalApi { get; set; }
    public int LocalApiPort { get; set; } = 17842;
    public bool ReadSitemaps { get; set; } = true;
    public bool FollowCanonicalLinks { get; set; } = true;
    public bool RenderJavaScript { get; set; }
    public bool EnableCompletionNotification { get; set; } = true;
    public string CompletionWebhook { get; set; } = string.Empty;
    public string CompletionEmail { get; set; } = string.Empty;
    public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 CopyWeb/1.0";
    public Dictionary<string, string> CustomHeaders { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string CustomCookies { get; set; } = string.Empty;
    public List<ProxyProfile> ProxyProfiles { get; set; } = [];
}

public sealed class ProxyProfile
{
    public string Name { get; set; } = string.Empty;
    public ProxyKind Kind { get; set; } = ProxyKind.Http;
    public bool Enabled { get; set; }
    public string Address { get; set; } = string.Empty;
    public int Port { get; set; } = 8080;
    public string? EncryptedUsername { get; set; }
    public string? EncryptedPassword { get; set; }
}
