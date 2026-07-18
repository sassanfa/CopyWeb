namespace CopyWeb.Models;

public sealed class AppSettings
{
    public string ThemePreset { get; set; } = "آبی";
    public int PrimaryColorArgb { get; set; } = Color.FromArgb(39, 91, 219).ToArgb();
    public int BackgroundColorArgb { get; set; } = Color.FromArgb(244, 247, 251).ToArgb();
    public int SurfaceColorArgb { get; set; } = Color.White.ToArgb();
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
    public bool ReadSitemaps { get; set; } = true;
    public bool FollowCanonicalLinks { get; set; } = true;
    public bool RenderJavaScript { get; set; }
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
