using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
using System.Text.Json;

namespace CopyWeb;

/// <summary>
/// Optional JavaScript/SPA snapshot helper. It is deliberately opt-in because
/// browser rendering is slower and uses more memory than the normal HTTP crawler.
/// </summary>
public sealed class BrowserSnapshotForm : Form
{
    private readonly Uri _uri;
    private readonly WebView2 _browser = new() { Dock = DockStyle.Fill };
    private readonly Label _status = UiTheme.Label("در حال اجرای JavaScript صفحه...", 10, color: UiTheme.Muted);
    private readonly TaskCompletionSource<string?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _completed;
    private string? _screenshotPath;

    public BrowserSnapshotForm(Uri uri)
    {
        _uri = uri;
        Text = "Snapshot صفحات JavaScript";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(980, 680);
        MinimumSize = new Size(700, 480);
        Font = UiTheme.NormalFont;
        BackColor = UiTheme.Background;
        var top = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = UiTheme.Surface };
        _status.Location = new Point(16, 12);
        top.Controls.Add(_status);
        Controls.Add(_browser);
        Controls.Add(top);
        Services.Localization.Apply(this, Services.AppSettingsStore.Load().Language);
        Shown += InitializeAsync;
        FormClosed += (_, _) => Complete(null);
    }

    public Task<string?> CaptureAsync(CancellationToken token)
    {
        if (!IsHandleCreated) CreateControl();
        Show();
        token.Register(() =>
        {
            if (IsDisposed) return;
            BeginInvoke((Action)(() => Complete(null)));
        });
        return _completion.Task;
    }

    public Task<string?> CaptureScreenshotAsync(string outputPath, CancellationToken token)
    {
        _screenshotPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(_screenshotPath)!);
        return CaptureAsync(token);
    }

    private async void InitializeAsync(object? sender, EventArgs e)
    {
        try
        {
            await _browser.EnsureCoreWebView2Async();
            _browser.CoreWebView2.NavigationCompleted += NavigationCompleted;
            _browser.CoreWebView2.Navigate(_uri.AbsoluteUri);
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            Complete(null);
        }
    }

    private async void NavigationCompleted(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess) { Complete(null); return; }
        try
        {
            await Task.Delay(400);
            if (!string.IsNullOrWhiteSpace(_screenshotPath))
            {
                await using var image = File.Create(_screenshotPath);
                await _browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, image);
                Complete(_screenshotPath);
                return;
            }
            var json = await _browser.CoreWebView2.ExecuteScriptAsync("document.documentElement.outerHTML");
            var html = JsonSerializer.Deserialize<string>(json);
            Complete(html);
        }
        catch { Complete(null); }
    }

    private void Complete(string? html)
    {
        if (Interlocked.Exchange(ref _completed, 1) != 0) return;
        _completion.TrySetResult(html);
        if (!IsDisposed) BeginInvoke((Action)Close);
    }
}
