using System.Security.Cryptography;
using System.Text;

namespace CopyWeb.Services;

internal static class UrlTools
{
    private static readonly string[] IgnoredExtensions =
    [
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg", ".ico", ".css", ".js",
        ".pdf", ".zip", ".rar", ".7z", ".mp3", ".mp4", ".avi", ".mov", ".woff", ".woff2", ".ttf"
        , ".otf", ".eot", ".avif", ".bmp", ".m4a", ".wav", ".flac", ".xml", ".json", ".rss", ".webmanifest"
    ];

    public static Uri? NormalizePageUrl(Uri page, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith('#')) return null;
        if (!Uri.TryCreate(page, value.Trim(), out var uri)) return null;
        if (uri.Scheme is not ("http" or "https")) return null;
        if (!IsLikelyPageUrl(uri)) return null;
        var builder = new UriBuilder(uri) { Fragment = string.Empty };
        return builder.Uri;
    }

    public static Uri NormalizeResourceUri(Uri uri)
    {
        var builder = new UriBuilder(uri) { Fragment = string.Empty };
        return builder.Uri;
    }

    public static string ResourceCacheKey(Uri uri)
    {
        var normalized = NormalizeResourceUri(uri);
        var extension = Path.GetExtension(normalized.AbsolutePath).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".svg" or ".ico" or ".avif" or ".bmp" or ".jxl"))
            return normalized.AbsoluteUri;

        // Static image URLs often differ only by a cache-busting query (?v=..., ?ver=...).
        var builder = new UriBuilder(normalized) { Query = string.Empty, Fragment = string.Empty };
        return builder.Uri.AbsoluteUri;
    }

    public static bool IsLikelyPageUrl(Uri uri) =>
        !IgnoredExtensions.Any(x => uri.AbsolutePath.EndsWith(x, StringComparison.OrdinalIgnoreCase));

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
