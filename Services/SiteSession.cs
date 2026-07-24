using CopyWeb.Models;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;

namespace CopyWeb.Services;

public sealed class SiteSession : IDisposable
{
    private readonly CookieContainer _cookies = new();
    private readonly HttpClient[] _clients;
    private readonly ProxyOptions _options;
    private int _nextClient;

    public SiteSession(ProxyOptions? proxy = null, IEnumerable<ProxyOptions>? rotatingProxies = null)
    {
        _options = proxy ?? new ProxyOptions();
        var options = (rotatingProxies ?? []).Where(x => x.Enabled && !string.IsNullOrWhiteSpace(x.Address)).ToList();
        if (_options.Enabled && !string.IsNullOrWhiteSpace(_options.Address)) options.Insert(0, _options);
        if (options.Count == 0) options.Add(_options);
        _clients = options.Select(CreateClient).ToArray();
    }

    private HttpClient CreateClient(ProxyOptions options)
    {
        var handler = new SocketsHttpHandler
        {
            CookieContainer = _cookies,
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        };

        if (options.Enabled)
        {
            var proxyUri = NormalizeProxyAddress(options);
            if (options.Kind == ProxyKind.Socks5)
            {
                handler.ConnectCallback = (context, token) => ConnectThroughSocks5Async(proxyUri.Host, proxyUri.Port, context.DnsEndPoint.Host, context.DnsEndPoint.Port, options.Username, options.Password, token);
            }
            else
            {
                handler.Proxy = new WebProxy(proxyUri) { BypassProxyOnLocal = true };
                handler.UseProxy = true;
                if (!string.IsNullOrWhiteSpace(options.Username))
                    handler.Proxy.Credentials = new NetworkCredential(options.Username, options.Password);
            }
        }

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 5, 600))
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(string.IsNullOrWhiteSpace(options.UserAgent)
            ? "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 CopyWeb/1.0"
            : options.UserAgent);
        // Advertise modern image formats so CDNs return the actual WebP/AVIF
        // candidate referenced by the page instead of a browser-specific fallback.
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("fa-IR,fa;q=0.9,en;q=0.7");
        foreach (var header in options.Headers)
        {
            try { client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value); } catch { }
        }
        if (!string.IsNullOrWhiteSpace(options.CookieHeader))
            client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", options.CookieHeader);
        return client;
    }

    public Task<HttpResponseMessage> GetAsync(Uri uri, HttpCompletionOption option, CancellationToken token) =>
        SendWithRetryAsync(uri, option, token, null, null);

    public Task<HttpResponseMessage> GetAsync(Uri uri, CancellationToken token) =>
        SendWithRetryAsync(uri, HttpCompletionOption.ResponseContentRead, token, null, null);

    public Task<HttpResponseMessage> GetResourceAsync(
        Uri uri,
        Uri? referer,
        HttpCompletionOption option,
        CancellationToken token) =>
        SendWithRetryAsync(
            uri,
            option,
            token,
            referer,
            "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");

    public void ImportCookies(IEnumerable<BrowserCookie> cookies)
    {
        foreach (var item in cookies)
        {
            try
            {
                var domain = item.Domain.StartsWith('.') ? item.Domain[1..] : item.Domain;
                var cookie = new Cookie(item.Name, item.Value, string.IsNullOrWhiteSpace(item.Path) ? "/" : item.Path, domain)
                {
                    Secure = false
                };
                if (item.Expires is not null) cookie.Expires = item.Expires.Value;
                _cookies.Add(cookie);
            }
            catch (CookieException) { }
        }
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Uri uri,
        HttpCompletionOption option,
        CancellationToken token,
        Uri? referer,
        string? accept)
    {
        var retries = Math.Clamp(_options.RetryCount, 0, 10);
        for (var attempt = 0; ; attempt++)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var client = _clients[Math.Abs(Interlocked.Increment(ref _nextClient)) % _clients.Length];
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                if (referer is not null && referer.IsAbsoluteUri && referer.Scheme is "http" or "https")
                    request.Headers.Referrer = referer;
                if (!string.IsNullOrWhiteSpace(accept))
                {
                    request.Headers.Remove("Accept");
                    request.Headers.TryAddWithoutValidation("Accept", accept);
                }
                var response = await client.SendAsync(request, option, token).ConfigureAwait(false);
                if (attempt < retries && IsTransient(response.StatusCode))
                {
                    response.Dispose();
                    await Task.Delay(Math.Clamp(_options.RetryDelayMilliseconds, 100, 30_000) * (attempt + 1), token).ConfigureAwait(false);
                    continue;
                }
                return response;
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested && attempt < retries)
            {
                await Task.Delay(Math.Clamp(_options.RetryDelayMilliseconds, 100, 30_000) * (attempt + 1), token).ConfigureAwait(false);
            }
            catch (HttpRequestException) when (attempt < retries)
            {
                await Task.Delay(Math.Clamp(_options.RetryDelayMilliseconds, 100, 30_000) * (attempt + 1), token).ConfigureAwait(false);
            }
        }
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or HttpStatusCode.BadGateway or
        HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout || (int)status >= 500;

    private static Uri NormalizeProxyAddress(ProxyOptions options)
    {
        var raw = options.Address.Trim();
        if (!raw.Contains("://", StringComparison.Ordinal)) raw = options.Kind == ProxyKind.Socks5 ? "socks5://" + raw : "http://" + raw;
        var uri = new Uri(raw);
        if (options.Port > 0 && uri.IsDefaultPort) uri = new UriBuilder(uri) { Port = options.Port }.Uri;
        if (options.Kind == ProxyKind.Https && uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
            uri = new UriBuilder(uri) { Scheme = "https" }.Uri;
        return uri;
    }

    private static async ValueTask<Stream> ConnectThroughSocks5Async(string proxyHost, int proxyPort, string targetHost, int targetPort, string? username, string? password, CancellationToken token)
    {
        var client = new TcpClient();
        try
        {
            await client.ConnectAsync(proxyHost, proxyPort, token).ConfigureAwait(false);
            var stream = client.GetStream();
            var authenticationMethods = string.IsNullOrWhiteSpace(username) ? new byte[] { 0x00 } : new byte[] { 0x00, 0x02 };
            await stream.WriteAsync(new byte[] { 0x05, (byte)authenticationMethods.Length }.Concat(authenticationMethods).ToArray(), token).ConfigureAwait(false);
            var selected = await ReadExactAsync(stream, 2, token).ConfigureAwait(false);
            if (selected[0] != 0x05 || selected[1] == 0xFF) throw new IOException("پروکسی SOCKS5 روش احراز هویت را قبول نکرد.");
            if (selected[1] == 0x02)
            {
                var user = Encoding.UTF8.GetBytes(username ?? string.Empty);
                var pass = Encoding.UTF8.GetBytes(password ?? string.Empty);
                await stream.WriteAsync(new byte[] { 0x01, (byte)Math.Min(user.Length, 255) }.Concat(user.Take(255)).Concat(new byte[] { (byte)Math.Min(pass.Length, 255) }).Concat(pass.Take(255)).ToArray(), token).ConfigureAwait(false);
                var auth = await ReadExactAsync(stream, 2, token).ConfigureAwait(false);
                if (auth[1] != 0x00) throw new IOException("احراز هویت پروکسی SOCKS5 ناموفق بود.");
            }

            var target = Encoding.UTF8.GetBytes(targetHost);
            var request = new byte[] { 0x05, 0x01, 0x00, 0x03, (byte)Math.Min(target.Length, 255) }
                .Concat(target.Take(255)).Concat(new byte[] { (byte)(targetPort >> 8), (byte)(targetPort & 0xff) }).ToArray();
            await stream.WriteAsync(request, token).ConfigureAwait(false);
            var response = await ReadExactAsync(stream, 4, token).ConfigureAwait(false);
            if (response[1] != 0x00) throw new IOException($"پروکسی SOCKS5 اتصال را رد کرد: {response[1]}");
            var addressLength = response[3] switch { 0x01 => 4, 0x04 => 16, 0x03 => (await ReadExactAsync(stream, 1, token).ConfigureAwait(false))[0], _ => throw new IOException("پاسخ SOCKS5 نامعتبر است.") };
            await ReadExactAsync(stream, addressLength + 2, token).ConfigureAwait(false);
            return stream;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private static async Task<byte[]> ReadExactAsync(Stream stream, int count, CancellationToken token)
    {
        var buffer = new byte[count];
        var offset = 0;
        while (offset < count)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), token).ConfigureAwait(false);
            if (read == 0) throw new EndOfStreamException("پاسخ پروکسی SOCKS5 ناقص است.");
            offset += read;
        }
        return buffer;
    }

    public void Dispose() { foreach (var client in _clients) client.Dispose(); }
}
