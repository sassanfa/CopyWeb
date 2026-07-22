using System.Net;
using System.Text;

namespace CopyWeb.Services;

/// <summary>Small, local-only static file server for previewing an archived project.</summary>
public sealed class OfflinePreviewServer : IDisposable
{
    private readonly string _root;
    private readonly HttpListener _listener = new();
    private CancellationTokenSource? _cts;

    public int Port { get; }
    public Uri BaseUri => new($"http://127.0.0.1:{Port}/");

    public OfflinePreviewServer(string root, int? port = null)
    {
        _root = Path.GetFullPath(root);
        if (!Directory.Exists(_root)) throw new DirectoryNotFoundException(_root);
        Port = port.GetValueOrDefault(0) is var selected && selected > 0 ? selected : Random.Shared.Next(43000, 47000);
    }

    public void Start()
    {
        if (_cts is not null) return;
        _listener.Prefixes.Add(BaseUri.AbsoluteUri);
        _listener.Start();
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => ListenAsync(_cts.Token));
    }

    private async Task ListenAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener.IsListening)
        {
            HttpListenerContext context;
            try { context = await _listener.GetContextAsync().WaitAsync(token).ConfigureAwait(false); }
            catch { break; }
            _ = Task.Run(() => ServeAsync(context), token);
        }
    }

    private async Task ServeAsync(HttpListenerContext context)
    {
        try
        {
            var relative = Uri.UnescapeDataString(context.Request.Url?.AbsolutePath.TrimStart('/') ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
            if (string.IsNullOrWhiteSpace(relative)) relative = "index.html";
            var candidate = Path.GetFullPath(Path.Combine(_root, relative));
            if (!candidate.StartsWith(_root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                !candidate.Equals(_root, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 403;
                return;
            }
            if (Directory.Exists(candidate)) candidate = Path.Combine(candidate, "index.html");
            if (!File.Exists(candidate)) { context.Response.StatusCode = 404; return; }
            var bytes = await File.ReadAllBytesAsync(candidate).ConfigureAwait(false);
            context.Response.ContentType = Mime(Path.GetExtension(candidate));
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        }
        catch { context.Response.StatusCode = 500; }
        finally { try { context.Response.Close(); } catch { } }
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { }
        _cts = null;
        try { if (_listener.IsListening) _listener.Stop(); } catch { }
    }

    public void Dispose() { Stop(); _listener.Close(); }

    private static string Mime(string extension) => extension.ToLowerInvariant() switch
    {
        ".html" or ".htm" => "text/html; charset=utf-8", ".css" => "text/css; charset=utf-8", ".js" or ".mjs" => "text/javascript; charset=utf-8",
        ".json" => "application/json", ".svg" => "image/svg+xml", ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif", ".webp" => "image/webp", ".woff" => "font/woff", ".woff2" => "font/woff2", ".ttf" => "font/ttf",
        ".mp4" => "video/mp4", ".webm" => "video/webm", _ => "application/octet-stream"
    };
}

public sealed record BrokenLink(string SourceFile, string Reference, string? ExpectedPath);

public static class OfflineLinkChecker
{
    public static IReadOnlyList<BrokenLink> Scan(string root)
    {
        var result = new List<BrokenLink>();
        if (!Directory.Exists(root)) return result;
        foreach (var file in Directory.EnumerateFiles(root, "*.html", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file, Encoding.UTF8);
            foreach (var reference in ExtractReferences(text))
            {
                if (reference.StartsWith("#") || reference.StartsWith("data:", StringComparison.OrdinalIgnoreCase) || reference.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) || Uri.TryCreate(reference, UriKind.Absolute, out _)) continue;
                var clean = reference.Split('?', '#')[0].Replace('/', Path.DirectorySeparatorChar);
                var expected = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, clean));
                if (!expected.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(expected))
                    result.Add(new BrokenLink(Path.GetRelativePath(root, file), reference, Path.GetRelativePath(root, expected)));
            }
        }
        return result;
    }

    private static IEnumerable<string> ExtractReferences(string html)
    {
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(html, "(?:href|src|poster)=\\\"([^\\\"]+)\\\"", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) yield return WebUtility.HtmlDecode(match.Groups[1].Value);
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(html, "(?:href|src|poster)=\\'([^\\']+)\\'", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) yield return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
}
