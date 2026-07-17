using System.Security.Cryptography;
using System.Text;

namespace CopyWeb.Services;

internal static class UrlTools
{
    private static readonly string[] IgnoredExtensions =
    [
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg", ".ico", ".css", ".js",
        ".pdf", ".zip", ".rar", ".7z", ".mp3", ".mp4", ".avi", ".mov", ".woff", ".woff2", ".ttf"
    ];

    public static Uri? NormalizePageUrl(Uri page, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith('#')) return null;
        if (!Uri.TryCreate(page, value.Trim(), out var uri)) return null;
        if (uri.Scheme is not ("http" or "https")) return null;
        if (IgnoredExtensions.Any(x => uri.AbsolutePath.EndsWith(x, StringComparison.OrdinalIgnoreCase))) return null;
        var builder = new UriBuilder(uri) { Fragment = string.Empty };
        return builder.Uri;
    }

    public static bool IsInternal(Uri candidate, Uri root, bool includeSubdomains)
    {
        if (candidate.Host.Equals(root.Host, StringComparison.OrdinalIgnoreCase)) return true;
        return includeSubdomains && candidate.Host.EndsWith('.' + root.Host, StringComparison.OrdinalIgnoreCase);
    }

    public static string Hash(string value, int length = 10)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes)[..length].ToLowerInvariant();
    }

    public static string CleanName(string value, string fallback)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var result = new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim(' ', '.', '_');
        return string.IsNullOrWhiteSpace(result) ? fallback : result;
    }
}
