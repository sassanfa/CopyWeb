using System.Net;
using System.Net.Sockets;
using System.Text;

namespace CopyWeb.Services;

/// <summary>Small, local-only static file server for previewing an archived project.</summary>
public sealed class OfflinePreviewServer : IDisposable
{
    public const string OfflineUsername = "admin";
    public const string OfflinePassword = "admin";

    private readonly string _root;
    private readonly HttpListener _listener = new();
    private readonly bool _requireAuthentication;
    private readonly string _sessionToken = Guid.NewGuid().ToString("N");
    private CancellationTokenSource? _cts;

    public int Port { get; }
    public Uri BaseUri => new($"http://localhost:{Port}/");

    public OfflinePreviewServer(string root, int? port = null, bool requireAuthentication = false)
    {
        _root = FindArchiveRoot(root);
        if (!Directory.Exists(_root)) throw new DirectoryNotFoundException(_root);
        _requireAuthentication = requireAuthentication;
        Port = port.GetValueOrDefault(0) is var selected && selected > 0 ? selected : FindAvailablePort();
    }

    public void Start()
    {
        if (_cts is not null) return;
        if (_listener.Prefixes.Count == 0)
        {
            _listener.Prefixes.Add(BaseUri.AbsoluteUri);
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        }
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
            var absolutePath = context.Request.Url?.AbsolutePath ?? "/";
            if (_requireAuthentication)
            {
                if (absolutePath.Equals("/__copyweb/login", StringComparison.OrdinalIgnoreCase))
                {
                    await HandleLoginAsync(context).ConfigureAwait(false);
                    return;
                }
                if (absolutePath.Equals("/__copyweb/logout", StringComparison.OrdinalIgnoreCase))
                {
                    ExpireSession(context.Response);
                    Redirect(context.Response, "/__copyweb/login");
                    return;
                }
                if (!IsAuthenticated(context.Request))
                {
                    Redirect(context.Response, "/__copyweb/login");
                    return;
                }
            }

            var relative = Uri.UnescapeDataString(absolutePath.TrimStart('/')).Replace('/', Path.DirectorySeparatorChar);
            if (string.IsNullOrWhiteSpace(relative)) relative = "index.html";
            var candidate = Path.GetFullPath(Path.Combine(_root, relative));
            if (!candidate.StartsWith(_root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                !candidate.Equals(_root, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 403;
                return;
            }
            if (Directory.Exists(candidate)) candidate = Path.Combine(candidate, "index.html");
            if (!File.Exists(candidate) && string.IsNullOrEmpty(Path.GetExtension(candidate)))
            {
                var directoryIndex = Path.Combine(candidate, "index.html");
                candidate = File.Exists(directoryIndex) ? directoryIndex : Path.Combine(_root, "index.html");
            }
            if (!File.Exists(candidate)) { context.Response.StatusCode = 404; return; }
            var bytes = await File.ReadAllBytesAsync(candidate).ConfigureAwait(false);
            if (_requireAuthentication && Path.GetExtension(candidate).Equals(".html", StringComparison.OrdinalIgnoreCase))
                bytes = RemoveOfflineCaptcha(bytes);
            context.Response.ContentType = Mime(Path.GetExtension(candidate));
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        }
        catch { context.Response.StatusCode = 500; }
        finally { try { context.Response.Close(); } catch { } }
    }

    private async Task HandleLoginAsync(HttpListenerContext context)
    {
        if (context.Request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding ?? Encoding.UTF8);
            var form = ParseForm(await reader.ReadToEndAsync().ConfigureAwait(false));
            if (form.TryGetValue("username", out var username) &&
                form.TryGetValue("password", out var password) &&
                username.Equals(OfflineUsername, StringComparison.Ordinal) &&
                password.Equals(OfflinePassword, StringComparison.Ordinal))
            {
                var cookie = new Cookie("CopyWebOfflineSession", _sessionToken, "/")
                {
                    HttpOnly = true
                };
                context.Response.SetCookie(cookie);
                Redirect(context.Response, "/");
                return;
            }

            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            await WriteLoginPageAsync(context.Response, "نام کاربری یا رمز عبور اشتباه است.").ConfigureAwait(false);
            return;
        }

        await WriteLoginPageAsync(context.Response, null).ConfigureAwait(false);
    }

    private bool IsAuthenticated(HttpListenerRequest request) =>
        request.Cookies["CopyWebOfflineSession"]?.Value.Equals(_sessionToken, StringComparison.Ordinal) == true;

    private static Dictionary<string, string> ParseForm(string value)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in value.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var key = separator >= 0 ? pair[..separator] : pair;
            var item = separator >= 0 ? pair[(separator + 1)..] : string.Empty;
            result[WebUtility.UrlDecode(key.Replace('+', ' '))] = WebUtility.UrlDecode(item.Replace('+', ' '));
        }
        return result;
    }

    private static async Task WriteLoginPageAsync(HttpListenerResponse response, string? error)
    {
        response.ContentType = "text/html; charset=utf-8";
        response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        var message = string.IsNullOrWhiteSpace(error)
            ? string.Empty
            : $"<div class=\"error\">{WebUtility.HtmlEncode(error)}</div>";
        var html = $$"""
<!doctype html>
<html lang="fa" dir="rtl">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>ورود به نسخه آفلاین — CopyWeb</title>
  <style>
    *{box-sizing:border-box}body{margin:0;min-height:100vh;display:grid;place-items:center;background:#080c25;color:#f1f5f9;font-family:"Segoe UI",Tahoma,sans-serif}
    .card{width:min(420px,calc(100% - 32px));padding:32px;border:1px solid #2a3157;border-radius:18px;background:#141a39;box-shadow:0 22px 70px #0008}
    .brand{font-size:28px;font-weight:800;margin:0 0 6px}.hint{color:#a4aecd;margin:0 0 24px;line-height:1.9}
    label{display:block;margin:14px 0 7px;font-size:14px}input{width:100%;height:44px;padding:0 13px;border:1px solid #353e70;border-radius:10px;background:#1d254d;color:#fff;outline:none;font-size:15px}
    input:focus{border-color:#7457ff;box-shadow:0 0 0 3px #7457ff33}button{width:100%;height:46px;margin-top:20px;border:0;border-radius:12px;background:linear-gradient(90deg,#634fff,#8b3dff);color:#fff;font-weight:700;cursor:pointer}
    .error{margin:10px 0;padding:10px 12px;border-radius:9px;background:#4e2d3b;color:#ffb6bd}.credentials{margin-top:16px;text-align:center;color:#a4aecd;font-size:13px}.credentials code{color:#f1f5f9}
  </style>
</head>
<body>
  <form class="card" method="post" action="/__copyweb/login" autocomplete="off">
    <div class="brand">CopyWeb</div>
    <p class="hint">برای مشاهده نسخه ذخیره‌شده، وارد حساب محلی آرشیو شوید.</p>
    {{message}}
    <label for="username">نام کاربری</label>
    <input id="username" name="username" value="admin" required autofocus>
    <label for="password">رمز عبور</label>
    <input id="password" name="password" type="password" required>
    <button type="submit">ورود به نسخه آفلاین</button>
    <div class="credentials">نام کاربری و رمز پیش‌فرض: <code>admin / admin</code></div>
  </form>
</body>
</html>
""";
        var bytes = Encoding.UTF8.GetBytes(html);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
    }

    private static void Redirect(HttpListenerResponse response, string location)
    {
        response.StatusCode = (int)HttpStatusCode.SeeOther;
        response.RedirectLocation = location;
    }

    private static void ExpireSession(HttpListenerResponse response)
    {
        var cookie = new Cookie("CopyWebOfflineSession", string.Empty, "/")
        {
            Expires = DateTime.UtcNow.AddDays(-1),
            HttpOnly = true
        };
        response.SetCookie(cookie);
    }

    private static byte[] RemoveOfflineCaptcha(byte[] htmlBytes)
    {
        var html = Encoding.UTF8.GetString(htmlBytes);
        const string cleanup = """
<style id="copyweb-offline-captcha-cleanup">
.g-recaptcha,.grecaptcha-badge,.h-captcha,[data-sitekey],
iframe[src*="recaptcha" i],iframe[src*="hcaptcha" i],
[class*="captcha" i],[id*="captcha" i]{display:none!important}
</style>
<script>
(()=>{const q='.g-recaptcha,.grecaptcha-badge,.h-captcha,[data-sitekey],iframe[src*="recaptcha" i],iframe[src*="hcaptcha" i],[class*="captcha" i],[id*="captcha" i]';
const clean=()=>document.querySelectorAll(q).forEach(x=>x.remove());
document.addEventListener('DOMContentLoaded',clean);new MutationObserver(clean).observe(document.documentElement,{childList:true,subtree:true});})();
</script>
""";
        var head = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        html = head >= 0 ? html.Insert(head, cleanup) : cleanup + html;
        return Encoding.UTF8.GetBytes(html);
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { }
        _cts = null;
        try { if (_listener.IsListening) _listener.Stop(); } catch { }
    }

    public void Dispose() { Stop(); _listener.Close(); }

    private static int FindAvailablePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private static string FindArchiveRoot(string root)
    {
        var full = Path.GetFullPath(root);
        if (!Directory.Exists(full) || File.Exists(Path.Combine(full, "index.html"))) return full;
        try
        {
            var candidate = Directory.EnumerateFiles(full, "index.html", SearchOption.AllDirectories)
                .OrderBy(path => Path.GetRelativePath(full, path).Count(ch => ch is '\\' or '/'))
                .ThenBy(path => path.Length)
                .FirstOrDefault();
            return candidate is null ? full : Path.GetDirectoryName(candidate)!;
        }
        catch { return full; }
    }

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
