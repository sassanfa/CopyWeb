using CopyWeb.Models;
using System.Net;

namespace CopyWeb.Services;

public sealed class SiteSession : IDisposable
{
    private readonly CookieContainer _cookies = new();
    private readonly HttpClient _client;

    public SiteSession(ProxyOptions? proxy = null)
    {
        var handler = new HttpClientHandler
        {
            CookieContainer = _cookies,
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10
        };
        if (proxy?.Enabled == true && Uri.TryCreate(proxy.Address, UriKind.Absolute, out var proxyUri))
        {
            if (proxy.Port > 0 && proxyUri.IsDefaultPort)
                proxyUri = new UriBuilder(proxyUri) { Port = proxy.Port }.Uri;
            handler.Proxy = new WebProxy(proxyUri) { BypassProxyOnLocal = true };
            handler.UseProxy = true;
            if (!string.IsNullOrWhiteSpace(proxy.Username))
                handler.Proxy.Credentials = new NetworkCredential(proxy.Username, proxy.Password);
        }
        _client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(45) };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 CopyWeb/1.0");
        _client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("fa-IR,fa;q=0.9,en;q=0.7");
    }

    public Task<HttpResponseMessage> GetAsync(Uri uri, HttpCompletionOption option, CancellationToken token) =>
        _client.GetAsync(uri, option, token);

    public Task<HttpResponseMessage> GetAsync(Uri uri, CancellationToken token) => _client.GetAsync(uri, token);

    public void ImportCookies(IEnumerable<BrowserCookie> cookies)
    {
        foreach (var item in cookies)
        {
            try
            {
                var cookie = new Cookie(item.Name, item.Value, string.IsNullOrWhiteSpace(item.Path) ? "/" : item.Path, item.Domain)
                {
                    Secure = false
                };
                if (item.Expires is not null)
                    cookie.Expires = item.Expires.Value;
                _cookies.Add(cookie);
            }
            catch (CookieException) { }
        }
    }

    public void Dispose() => _client.Dispose();
}
