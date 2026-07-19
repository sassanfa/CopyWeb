using System.Net;
using System.Text;
using System.Text.Json;

namespace CopyWeb.Services;

public sealed class LocalApiServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly Func<object> _status;
    private readonly Action _stop;
    private CancellationTokenSource? _cts;

    public LocalApiServer(int port, Func<object> status, Action stop)
    {
        if (port is < 1024 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        _status = status; _stop = stop; _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
    }

    public void Start()
    {
        if (_listener.IsListening) return;
        _cts = new CancellationTokenSource(); _listener.Start(); _ = Task.Run(() => ListenAsync(_cts.Token));
    }

    private async Task ListenAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            HttpListenerContext? context = null;
            try { context = await _listener.GetContextAsync().WaitAsync(token).ConfigureAwait(false); await HandleAsync(context).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            catch { context?.Response.Close(); }
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        var response = context.Response; response.ContentType = "application/json; charset=utf-8"; response.Headers["Access-Control-Allow-Origin"] = "*";
        object payload;
        if (context.Request.HttpMethod == "GET" && context.Request.Url?.AbsolutePath.Equals("/api/status", StringComparison.OrdinalIgnoreCase) == true) payload = _status();
        else if (context.Request.HttpMethod == "GET" && context.Request.Url?.AbsolutePath.Equals("/api/projects", StringComparison.OrdinalIgnoreCase) == true) payload = new { projects = ProjectStorage.GetKnownProjectFiles().Where(File.Exists).ToArray() };
        else if (context.Request.HttpMethod == "POST" && context.Request.Url?.AbsolutePath.Equals("/api/stop", StringComparison.OrdinalIgnoreCase) == true) { _stop(); payload = new { ok = true, message = "stop requested" }; }
        else { response.StatusCode = 404; payload = new { ok = false, error = "not found" }; }
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)); response.ContentLength64 = bytes.Length; await response.OutputStream.WriteAsync(bytes); response.Close();
    }

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { }
        try { _listener.Stop(); _listener.Close(); } catch { }
        _cts?.Dispose();
    }
}
