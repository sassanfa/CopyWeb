using System.Collections.Concurrent;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using CopyWeb.Services;

namespace CopyWeb;

/// <summary>
/// A browser-assisted capture mode. The page is rendered normally in WebView2;
/// every successful network response is copied to the selected project folder
/// and the matching DOM element is highlighted in pale green.
/// </summary>
public sealed class LiveArchiveForm : Form
{
    private readonly Uri _root;
    private readonly string _output;
    private readonly bool _manualNavigation;
    private readonly WebView2 _browser = new() { Dock = DockStyle.Fill };
    private TextBox? _address;
    private readonly Label _status = UiTheme.Label("در حال آماده‌سازی ذخیره زنده...", 10, color: UiTheme.Muted);
    private readonly ListView _resources = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = true, GridLines = true, HideSelection = false };
    private readonly ConcurrentDictionary<string, string> _urlToFile = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _visitedPages = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _htmlPages = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _fileUrls = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _hashToFile = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _seenUrls = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _saveGate = new(4, 4);
    private readonly HttpClient _fallbackClient = new(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All }) { Timeout = TimeSpan.FromSeconds(45) };
    private readonly object _manifestGate = new();
    private readonly SemaphoreSlim _rewriteGate = new(1, 1);
    private int _rewriteScheduled;
    private int _saved;
    private int _failed;
    private int _pending;
    private bool _closing;

    public LiveArchiveForm(Uri root, string outputDirectory, bool manualNavigation = false)
    {
        _root = root;
        _output = Path.GetFullPath(outputDirectory);
        _manualNavigation = manualNavigation;
        Text = manualNavigation ? "CopyWeb — کپی وبی" : "CopyWeb — ذخیره زنده سایت";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1180, 760);
        MinimumSize = new Size(860, 560);
        Font = UiTheme.NormalFont;
        BackColor = UiTheme.Background;
        RightToLeft = RightToLeft.Yes;
        BuildUi();
        Shown += InitializeAsync;
        FormClosed += (_, _) =>
        {
            _closing = true;
            try { RewriteSavedFilesAsync().GetAwaiter().GetResult(); } catch { }
            SaveManifest();
            _saveGate.Dispose();
            _rewriteGate.Dispose();
            _fallbackClient.Dispose();
        };
    }

    private void BuildUi()
    {
        var top = new Panel { Dock = DockStyle.Top, Height = _manualNavigation ? 122 : 78, BackColor = UiTheme.Surface, Padding = new Padding(18, 12, 18, 8) };
        var title = UiTheme.Label(_manualNavigation ? "حالت کپی وبی" : "حالت ذخیره زنده", 16, FontStyle.Bold); title.Dock = DockStyle.Top; title.Height = 28;
        _status.Dock = DockStyle.Top; _status.Height = 24; _status.TextAlign = ContentAlignment.MiddleRight;
        top.Controls.Add(_status); top.Controls.Add(title);
        if (_manualNavigation)
        {
            var navigation = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 38,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 3, 0, 0),
                BackColor = UiTheme.Surface
            };
            var go = UiTheme.Button("رفتن", UiTheme.Primary); go.Width = 76; go.Height = 30; go.Click += (_, _) => NavigateAddress();
            var forward = UiTheme.Button("جلو", Color.FromArgb(226, 231, 239)); forward.Tag = "secondary-button"; forward.ForeColor = UiTheme.Text; forward.Width = 70; forward.Height = 30; forward.Click += (_, _) => { if (_browser.CanGoForward) _browser.GoForward(); };
            var back = UiTheme.Button("عقب", Color.FromArgb(226, 231, 239)); back.Tag = "secondary-button"; back.ForeColor = UiTheme.Text; back.Width = 70; back.Height = 30; back.Click += (_, _) => { if (_browser.CanGoBack) _browser.GoBack(); };
            _address = new TextBox { Width = 650, Height = 28, Margin = new Padding(8, 0, 8, 0), RightToLeft = RightToLeft.No, TextAlign = HorizontalAlignment.Left, BorderStyle = BorderStyle.FixedSingle };
            _address.Text = _root.AbsoluteUri;
            _address.KeyDown += (_, args) => { if (args.KeyCode == Keys.Enter) { args.SuppressKeyPress = true; NavigateAddress(); } };
            navigation.Controls.Add(go); navigation.Controls.Add(forward); navigation.Controls.Add(back); navigation.Controls.Add(_address);
            top.Controls.Add(navigation);
        }

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 820, BackColor = UiTheme.Border };
        split.Panel1.Controls.Add(_browser);
        var side = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), BackColor = UiTheme.Background };
        var legend = UiTheme.Label("سبز: ذخیره شد | زرد: در انتظار | قرمز: خطا", 9, color: UiTheme.Muted); legend.Dock = DockStyle.Top; legend.Height = 34;
        _resources.Columns.Add("وضعیت", 116); _resources.Columns.Add("نوع", 76); _resources.Columns.Add("آدرس / فایل", 300);
        _resources.BackColor = Color.White; _resources.RightToLeft = RightToLeft.No; _resources.RightToLeftLayout = false;
        side.Controls.Add(_resources); side.Controls.Add(legend); split.Panel2.Controls.Add(side);

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 54, Padding = new Padding(14, 8, 14, 8), FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = UiTheme.Surface };
        var close = UiTheme.Button("بستن", UiTheme.Primary); close.Width = 110; close.Height = 36; close.Click += (_, _) => Close();
        var open = UiTheme.Button("باز کردن پوشه", Color.FromArgb(226, 231, 239)); open.Tag = "secondary-button"; open.ForeColor = UiTheme.Text; open.Width = 135; open.Height = 36; open.Click += (_, _) => OpenFolder();
        var retry = UiTheme.Button("\u062A\u0644\u0627\u0634 \u062F\u0648\u0628\u0627\u0631\u0647", Color.FromArgb(92, 143, 116)); retry.Width = 125; retry.Height = 36; retry.Click += async (_, _) => await RetrySelectedAsync();
        var remove = UiTheme.Button("\u062D\u0630\u0641 \u0645\u0648\u0631\u062F", Color.FromArgb(173, 99, 99)); remove.Width = 110; remove.Height = 36; remove.Click += (_, _) => DeleteSelected();
        var backButton = UiTheme.Button("\u0628\u0627\u0632\u06af\u0634\u062A", Color.FromArgb(226, 231, 239)); backButton.Tag = "secondary-button"; backButton.ForeColor = UiTheme.Text; backButton.Width = 105; backButton.Height = 36; backButton.Click += (_, _) => GoBackOrClose();
        bottom.Controls.Add(close); bottom.Controls.Add(open); bottom.Controls.Add(retry); bottom.Controls.Add(remove); bottom.Controls.Add(backButton);
        Controls.Add(split); Controls.Add(bottom); Controls.Add(top);
    }

    private async void InitializeAsync(object? sender, EventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_output);
            var options = new CoreWebView2EnvironmentOptions("--enable-features=msWebView2EnableDownloadContentInWebResourceResponseReceived");
            var environment = await CoreWebView2Environment.CreateAsync(null, null, options);
            await _browser.EnsureCoreWebView2Async(environment);
            await _browser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(OverlayScript);
            _browser.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            _browser.CoreWebView2.WebResourceResponseReceived += ResponseReceived;
            _browser.CoreWebView2.NewWindowRequested += (_, args) =>
            {
                // Keep target=_blank/window.open inside the capture browser so
                // the new page and its resources are archived as well.
                args.NewWindow = _browser.CoreWebView2;
                args.Handled = true;
            };
            _browser.CoreWebView2.NavigationStarting += (_, args) =>
            {
                if (_address is not null && Uri.TryCreate(args.Uri, UriKind.Absolute, out var navigationUri)) _address.Text = navigationUri.AbsoluteUri;
                if (Uri.TryCreate(args.Uri, UriKind.Absolute, out var pageUri) && pageUri.Scheme is ("http" or "https")) _visitedPages[pageUri.AbsoluteUri] = "در حال بارگذاری";
                MarkUrl(args.Uri, "pending");
            };
            _browser.CoreWebView2.NavigationCompleted += (_, args) =>
            {
                var current = _browser.Source?.AbsoluteUri ?? _root.AbsoluteUri;
                if (_address is not null) _address.Text = current;
                if (Uri.TryCreate(current, UriKind.Absolute, out var pageUri) && pageUri.Scheme is ("http" or "https")) _visitedPages[pageUri.AbsoluteUri] = args.IsSuccess ? "ذخیره شد" : "خطا";
                if (!args.IsSuccess) MarkUrl(current, "failed", args.WebErrorStatus.ToString());
                else MarkUrl(current, "saved");
            };
            _browser.CoreWebView2.Navigate(_root.AbsoluteUri);
            _status.Text = $"در حال نمایش {_root.AbsoluteUri} — منابع ذخیره‌شده: ۰";
        }
        catch (Exception ex)
        {
            _status.Text = "WebView2 در دسترس نیست: " + ex.Message;
            MessageBox.Show(this, _status.Text, "خطای ذخیره زنده", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void NavigateAddress()
    {
        if (_address is null) return;
        var text = _address.Text.Trim();
        if (!text.Contains("://", StringComparison.Ordinal)) text = "https://" + text;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            MessageBox.Show(this, "آدرس وب معتبر وارد کنید.", "کپی وبی", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        _address.Text = uri.AbsoluteUri;
        _browser.CoreWebView2?.Navigate(uri.AbsoluteUri);
    }

    private async void ResponseReceived(object? sender, CoreWebView2WebResourceResponseReceivedEventArgs e)
    {
        if (_closing || !Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) return;
        var key = UrlTools.ResourceCacheKey(uri);
        if (!_seenUrls.TryAdd(key, 0)) return;
        var row = AddRow(uri.AbsoluteUri, "در انتظار ۰٪", "در حال دریافت");
        Interlocked.Increment(ref _pending);
        try
        {
            var statusCode = e.Response.StatusCode;
            if (statusCode < 200 || statusCode >= 400) throw new InvalidOperationException($"HTTP {statusCode}");
            // Keep the COM call that asks WebView2 for the response body on the
            // UI apartment; the returned stream itself is safe to read in the
            // background.
            await _saveGate.WaitAsync();
            try
            {
                var contentType = e.Response.Headers.GetHeader("Content-Type") ?? string.Empty;
                var responseData = await ReadResponseBytesAsync(uri, e, contentType, percent => UpdateRowProgress(row, percent)).ConfigureAwait(false);
                if (responseData is null || responseData.Value.Bytes.Length == 0) throw new InvalidOperationException("محتوای پاسخ از WebView2 و مسیر پشتیبان قابل دریافت نبود");
                var bytes = responseData.Value.Bytes;
                if (string.IsNullOrWhiteSpace(contentType)) contentType = responseData.Value.ContentType;
                var kind = KindFor(uri, contentType);
                var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
                var path = await SaveBytesAsync(uri, kind, contentType, hash, bytes).ConfigureAwait(false);
                _urlToFile[key] = path;
                _fileUrls[path] = uri.AbsoluteUri;
                if (kind == "HTML") _htmlPages[path] = uri.AbsoluteUri;
                ScheduleRewrite();
                UpdateRow(row, "موفق ۱۰۰٪", kind, path);
                Interlocked.Increment(ref _saved);
                MarkUrl(uri.AbsoluteUri, "saved");
            }
            finally { _saveGate.Release(); }
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failed);
            UpdateRow(row, "خطا", "-", ex.Message);
            MarkUrl(uri.AbsoluteUri, "failed", ex.Message);
        }
        finally
        {
            Interlocked.Decrement(ref _pending);
            UpdateStatus();
        }
    }

    private async Task<(byte[] Bytes, string ContentType)?> ReadResponseBytesAsync(Uri uri, CoreWebView2WebResourceResponseReceivedEventArgs args, string contentType, Action<int> progress)
    {
        try
        {
            await using var content = await args.Response.GetContentAsync().ConfigureAwait(false);
            if (content is not null)
            {
                var totalLength = long.TryParse(args.Response.Headers.GetHeader("Content-Length"), out var length) ? length : 0;
                var bytes = await ReadStreamWithProgressAsync(content, totalLength, progress).ConfigureAwait(false);
                if (bytes.Length > 0) return (bytes, contentType);
            }
        }
        catch (COMException) { }
        catch (InvalidOperationException) { }

        // Some WebView2 runtimes do not expose response bodies for resources.
        // Re-request the same URL with the browser's current cookies instead.
        try
        {
            var cookieHeader = await ReadCookieHeaderAsync(uri).ConfigureAwait(false);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36");
            request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");
            try
            {
                var currentPage = _browser.Source;
                if (currentPage is not null && currentPage.Scheme is "http" or "https") request.Headers.Referrer = currentPage;
            }
            catch { }
            if (!string.IsNullOrWhiteSpace(cookieHeader)) request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
            using var response = await _fallbackClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;
            var totalLength = response.Content.Headers.ContentLength ?? 0;
            await using var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            var bytes = await ReadStreamWithProgressAsync(responseStream, totalLength, progress).ConfigureAwait(false);
            var fallbackType = response.Content.Headers.ContentType?.ToString() ?? contentType;
            return bytes.Length == 0 ? null : (bytes, fallbackType);
        }
        catch { return null; }
    }

    private async Task<string> ReadCookieHeaderAsync(Uri uri)
    {
        async Task<string> ReadCoreAsync()
        {
            if (_browser.CoreWebView2 is null) return string.Empty;
            var cookies = await _browser.CoreWebView2.CookieManager.GetCookiesAsync(uri.AbsoluteUri);
            return string.Join("; ", cookies.Select(cookie => $"{cookie.Name}={cookie.Value}"));
        }

        try
        {
            if (InvokeRequired) return await (Task<string>)Invoke(new Func<Task<string>>(() => ReadCoreAsync()));
            return await ReadCoreAsync();
        }
        catch { return string.Empty; }
    }

    private async Task RetrySelectedAsync()
    {
        var rows = _resources.SelectedItems.Cast<ListViewItem>().ToList();
        if (rows.Count == 0)
        {
            MessageBox.Show(this, "ابتدا حداقل یک منبع را انتخاب کنید.", "تلاش دوباره", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        foreach (var row in rows) await RetryRowAsync(row);
    }

    private async Task RetryRowAsync(ListViewItem row)
    {
        if (row.Tag is not string rawUrl || !Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) return;
        var key = UrlTools.ResourceCacheKey(uri);
        _seenUrls.TryRemove(key, out _);
        Interlocked.Increment(ref _pending);
        UpdateRowProgress(row, 0);
        try
        {
            var cookieHeader = await ReadCookieHeaderAsync(uri).ConfigureAwait(false);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36");
            request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");
            try
            {
                var currentPage = _browser.Source;
                if (currentPage is not null && currentPage.Scheme is "http" or "https") request.Headers.Referrer = currentPage;
            }
            catch { }
            if (!string.IsNullOrWhiteSpace(cookieHeader)) request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
            using var response = await _fallbackClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"HTTP {(int)response.StatusCode}");
            var contentType = response.Content.Headers.ContentType?.ToString() ?? string.Empty;
            var totalLength = response.Content.Headers.ContentLength ?? 0;
            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            var bytes = await ReadStreamWithProgressAsync(stream, totalLength, percent => UpdateRowProgress(row, percent)).ConfigureAwait(false);
            if (bytes.Length == 0) throw new InvalidOperationException("محتوای منبع خالی است");
            var kind = KindFor(uri, contentType);
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
            var path = await SaveBytesAsync(uri, kind, contentType, hash, bytes).ConfigureAwait(false);
            _urlToFile[key] = path;
            _fileUrls[path] = uri.AbsoluteUri;
            if (kind == "HTML") _htmlPages[path] = uri.AbsoluteUri;
            _seenUrls[key] = 0;
            ScheduleRewrite();
            UpdateRow(row, "موفق ۱۰۰٪", kind, path);
            Interlocked.Increment(ref _saved);
            MarkUrl(uri.AbsoluteUri, "saved");
        }
        catch (Exception ex)
        {
            UpdateRow(row, "خطا", "-", ex.Message);
            MarkUrl(uri.AbsoluteUri, "failed", ex.Message);
        }
        finally
        {
            Interlocked.Decrement(ref _pending);
            UpdateStatus();
        }
    }

    private void DeleteSelected()
    {
        var rows = _resources.SelectedItems.Cast<ListViewItem>().ToList();
        if (rows.Count == 0)
        {
            MessageBox.Show(this, "ابتدا حداقل یک منبع را انتخاب کنید.", "حذف مورد", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (MessageBox.Show(this, $"آیا {rows.Count:N0} مورد انتخابی حذف شود؟", "تأیید حذف", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        foreach (var row in rows)
        {
            if (row.Tag is string rawUrl && Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri))
                _seenUrls.TryRemove(UrlTools.ResourceCacheKey(uri), out _);
            var path = row.SubItems.Count > 2 ? row.SubItems[2].Text : string.Empty;
            try { if (Path.IsPathFullyQualified(path) && File.Exists(path)) File.Delete(path); } catch { }
            if (Path.IsPathFullyQualified(path))
            {
                _fileUrls.TryRemove(path, out _);
                _htmlPages.TryRemove(path, out _);
            }
            _resources.Items.Remove(row);
        }
        SaveManifest();
        UpdateStatus();
    }

    private static async Task<byte[]> ReadStreamWithProgressAsync(Stream stream, long totalLength, Action<int> progress)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[64 * 1024];
        long readTotal = 0;
        var lastPercent = -1;
        if (totalLength > 0) progress(0);
        int read;
        while ((read = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length)).ConfigureAwait(false)) > 0)
        {
            await buffer.WriteAsync(chunk.AsMemory(0, read)).ConfigureAwait(false);
            readTotal += read;
            if (totalLength > 0)
            {
                var percent = (int)Math.Clamp(readTotal * 100L / totalLength, 0L, 100L);
                if (percent != lastPercent) { lastPercent = percent; progress(percent); }
            }
        }
        if (totalLength > 0 && lastPercent < 100) progress(100);
        return buffer.ToArray();
    }

    private async Task<string> SaveBytesAsync(Uri uri, string kind, string contentType, string hash, byte[] bytes)
    {
        var extension = ExtensionFor(uri, contentType, kind);
        var directory = kind switch { "HTML" => Path.Combine(_output, "pages"), "تصویر" => Path.Combine(_output, "Img"), "CSS" => Path.Combine(_output, "CSS"), "JS" => Path.Combine(_output, "JS"), "فونت" => Path.Combine(_output, "Fonts"), _ => Path.Combine(_output, "Files") };
        Directory.CreateDirectory(directory);
        var isRoot = uri.AbsoluteUri.Equals(_root.AbsoluteUri, StringComparison.OrdinalIgnoreCase) || (uri.AbsolutePath is "/" or "" && uri.Host.Equals(_root.Host, StringComparison.OrdinalIgnoreCase));
        var stem = UrlTools.CleanName(Path.GetFileNameWithoutExtension(uri.AbsolutePath), kind == "HTML" ? "page" : "asset");
        var path = kind == "HTML" && isRoot ? Path.Combine(_output, "index.html") : Path.Combine(directory, $"{stem}-{UrlTools.Hash(uri.AbsoluteUri)}{extension}");
        if (kind == "تصویر" && _hashToFile.TryGetValue(hash, out var existing) && File.Exists(existing)) return existing;
        if (_hashToFile.TryGetValue(hash, out existing) && File.Exists(existing) && kind != "CSS") return existing;
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllBytesAsync(temporary, bytes).ConfigureAwait(false);
        File.Move(temporary, path, true);
        _hashToFile.TryAdd(hash, path);
        lock (_manifestGate) SaveManifest();
        return path;
    }

    private ListViewItem AddRow(string url, string status, string file)
    {
        if (IsDisposed) return new ListViewItem();
        if (InvokeRequired) return (ListViewItem)Invoke(() => AddRow(url, status, file));
        var row = new ListViewItem(status); row.SubItems.Add("-"); row.SubItems.Add(url); row.Tag = url; row.BackColor = Color.FromArgb(255, 251, 235); _resources.Items.Add(row); return row;
    }

    private void UpdateRow(ListViewItem row, string status, string kind, string file)
    {
        if (IsDisposed) return;
        void Update()
        {
            row.Text = status; row.SubItems[1].Text = kind; row.SubItems[2].Text = file;
            row.BackColor = status.StartsWith("موفق", StringComparison.Ordinal) ? Color.FromArgb(220, 252, 231) : Color.FromArgb(254, 226, 226);
        }
        if (InvokeRequired) BeginInvoke((Action)Update); else Update();
    }

    /// <summary>
    /// Converts references in the captured markup from their original web URLs
    /// to the corresponding local files.  This is deliberately repeated while
    /// the capture is running: a page is often saved before its images, fonts
    /// or CSS responses arrive.
    /// </summary>
    private void ScheduleRewrite()
    {
        if (_closing || Interlocked.Exchange(ref _rewriteScheduled, 1) != 0) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(250).ConfigureAwait(false);
                await RewriteSavedFilesAsync().ConfigureAwait(false);
            }
            catch { }
            finally
            {
                Interlocked.Exchange(ref _rewriteScheduled, 0);
                if (!_closing && Volatile.Read(ref _pending) > 0) ScheduleRewrite();
            }
        });
    }

    private async Task RewriteSavedFilesAsync()
    {
        if (_closing && _htmlPages.IsEmpty && _fileUrls.IsEmpty) return;
        await _rewriteGate.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (var page in _htmlPages.ToArray())
            {
                if (!File.Exists(page.Key)) continue;
                try
                {
                    var markup = await File.ReadAllTextAsync(page.Key, Encoding.UTF8).ConfigureAwait(false);
                    var rewritten = RewriteMarkup(markup, page.Value, page.Key);
                    if (!string.Equals(markup, rewritten, StringComparison.Ordinal))
                        await AtomicWriteTextAsync(page.Key, rewritten).ConfigureAwait(false);
                }
                catch { }
            }

            foreach (var file in _fileUrls.Keys.Where(path => path.Contains(Path.DirectorySeparatorChar + "CSS" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                if (!File.Exists(file)) continue;
                var sourceUrl = _fileUrls[file];
                try
                {
                    var css = await File.ReadAllTextAsync(file, Encoding.UTF8).ConfigureAwait(false);
                    var rewritten = RewriteCss(css, sourceUrl, file);
                    if (!string.Equals(css, rewritten, StringComparison.Ordinal))
                        await AtomicWriteTextAsync(file, rewritten).ConfigureAwait(false);
                }
                catch { }
            }
        }
        finally { _rewriteGate.Release(); }
    }

    private static async Task AtomicWriteTextAsync(string path, string text)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllTextAsync(temporary, text, new UTF8Encoding(false)).ConfigureAwait(false);
        File.Move(temporary, path, true);
    }

    private string RewriteMarkup(string markup, string pageUrl, string pagePath)
    {
        if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out var pageUri)) return markup;
        var attributePattern = new Regex("(?<prefix>\\b(?:src|href|poster|data-src|data-original|data-lazy-src)\\s*=\\s*[\\\"'])(?<value>[^\\\"']+)(?<suffix>[\\\"'])", RegexOptions.IgnoreCase);
        markup = attributePattern.Replace(markup, match =>
        {
            var value = match.Groups["value"].Value;
            var local = LocalReference(value, pageUri, pagePath);
            return local is null ? match.Value : match.Groups["prefix"].Value + local + match.Groups["suffix"].Value;
        });

        var setPattern = new Regex("(?<prefix>\\b(?:srcset|data-srcset)\\s*=\\s*[\\\"'])(?<value>[^\\\"']+)(?<suffix>[\\\"'])", RegexOptions.IgnoreCase);
        markup = setPattern.Replace(markup, match =>
        {
            var value = RewriteSrcSet(match.Groups["value"].Value, pageUri, pagePath);
            return match.Groups["prefix"].Value + value + match.Groups["suffix"].Value;
        });

        var stylePattern = new Regex("(?<prefix>\\bstyle\\s*=\\s*[\\\"'])(?<value>[^\\\"']*)(?<suffix>[\\\"'])", RegexOptions.IgnoreCase);
        return stylePattern.Replace(markup, match =>
            match.Groups["prefix"].Value + RewriteCss(match.Groups["value"].Value, pageUrl, pagePath) + match.Groups["suffix"].Value);
    }

    private string RewriteSrcSet(string value, Uri pageUri, string pagePath)
    {
        var parts = value.Split(',', StringSplitOptions.None);
        for (var i = 0; i < parts.Length; i++)
        {
            var item = parts[i].Trim();
            if (item.Length == 0) continue;
            var space = item.IndexOfAny([' ', '\t']);
            var candidate = space < 0 ? item : item[..space];
            var local = LocalReference(candidate, pageUri, pagePath);
            if (local is not null)
                parts[i] = space < 0 ? local : local + item[space..];
        }
        return string.Join(',', parts);
    }

    private string RewriteCss(string css, string sourceUrl, string sourcePath)
    {
        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var sourceUri)) return css;
        var pattern = new Regex("(?<prefix>url\\(\\s*[\\\"']?)(?<value>[^\\\"'\\)]+)(?<suffix>[\\\"']?\\s*\\))", RegexOptions.IgnoreCase);
        return pattern.Replace(css, match =>
        {
            var local = LocalReference(match.Groups["value"].Value.Trim(), sourceUri, sourcePath);
            return local is null ? match.Value : match.Groups["prefix"].Value + local + match.Groups["suffix"].Value;
        });
    }

    private string? LocalReference(string raw, Uri sourceUri, string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith("data:", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("#", StringComparison.Ordinal)) return null;
        if (!Uri.TryCreate(sourceUri, raw.Trim(), out var absolute) || absolute.Scheme is not ("http" or "https")) return null;
        var key = UrlTools.ResourceCacheKey(absolute);
        if (!_urlToFile.TryGetValue(key, out var localPath) || !File.Exists(localPath)) return null;
        var relative = Path.GetRelativePath(Path.GetDirectoryName(sourcePath) ?? _output, localPath);
        return relative.Replace('\\', '/');
    }

    private void UpdateRowProgress(ListViewItem row, int percent)
    {
        if (IsDisposed) return;
        percent = Math.Clamp(percent, 0, 100);
        void Update()
        {
            if (row.ListView is null) return;
            row.Text = $"در انتظار {percent}%";
            row.BackColor = Color.FromArgb(255, 251, 235);
        }
        if (InvokeRequired) BeginInvoke((Action)Update); else Update();
    }

    private void UpdateStatus()
    {
        if (IsDisposed) return;
        void Update() => _status.Text = $"ذخیره زنده فعال — موفق: {_saved:N0} | در انتظار: {_pending:N0} | خطا: {_failed:N0}";
        if (InvokeRequired) BeginInvoke((Action)Update); else Update();
    }

    private void MarkUrl(string url, string state, string? message = null)
    {
        if (IsDisposed || _browser.CoreWebView2 is null) return;
        var payload = JsonSerializer.Serialize(new { url, state, message });
        try { _ = _browser.CoreWebView2.ExecuteScriptAsync($"window.CopyWebMark && window.CopyWebMark({payload});"); } catch { }
    }

    private void SaveManifest()
    {
        try
        {
            Directory.CreateDirectory(_output);
            var manifest = new { Root = _root.AbsoluteUri, SavedAt = DateTimeOffset.Now, VisitedPages = _visitedPages, Files = _urlToFile, Hashes = _hashToFile };
            File.WriteAllText(Path.Combine(_output, "live-capture-manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
        }
        catch { }
    }

    private void OpenFolder()
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{_output}\"") { UseShellExecute = true }); } catch { }
    }

    private void GoBackOrClose()
    {
        try
        {
            if (_browser.CanGoBack) _browser.GoBack();
            else Close();
        }
        catch { Close(); }
    }

    private static string KindFor(Uri uri, string contentType)
    {
        var ext = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
        // Prefer a known file extension over a misleading CDN MIME type. Some
        // image CDNs answer a .webp request with text/html when a challenge is
        // returned; it must not be classified as an HTML page.
        if (ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".avif" or ".svg" or ".ico" or ".bmp" or ".jxl") return "تصویر";
        if (ext == ".css") return "CSS";
        if (ext is ".js" or ".mjs") return "JS";
        if (ext is ".woff" or ".woff2" or ".ttf" or ".otf" or ".eot") return "فونت";
        if (contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase) || ext is ".html" or ".htm" or "") return "HTML";
        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return "تصویر";
        if (contentType.Contains("css", StringComparison.OrdinalIgnoreCase)) return "CSS";
        if (contentType.Contains("javascript", StringComparison.OrdinalIgnoreCase)) return "JS";
        if (contentType.Contains("font", StringComparison.OrdinalIgnoreCase)) return "فونت";
        return "فایل";
    }

    private static string ExtensionFor(Uri uri, string contentType, string kind)
    {
        var ext = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(ext) && ext.Length <= 8) return ext;
        return kind switch
        {
            "HTML" => ".html", "CSS" => ".css", "JS" => ".js", "تصویر" when contentType.Contains("webp", StringComparison.OrdinalIgnoreCase) => ".webp", "تصویر" when contentType.Contains("png", StringComparison.OrdinalIgnoreCase) => ".png", "تصویر" => ".img", "فونت" => ".bin", _ => ".bin"
        };
    }

    private const string OverlayScript = """
(() => {
  const style = document.createElement('style');
  style.textContent = `.copyweb-saved{outline:2px solid #86efac!important;background-color:rgba(134,239,172,.22)!important;transition:background-color .2s} .copyweb-pending{outline:2px solid #fcd34d!important;background-color:rgba(253,230,138,.20)!important} .copyweb-failed{outline:2px solid #fca5a5!important;background-color:rgba(254,202,202,.24)!important}`;
  (document.head || document.documentElement).appendChild(style);
  const states = new Map();
  const applyMarks = () => {
    for (const [url, state] of states) {
      try {
        const absolute = new URL(url, location.href).href;
        const cls = 'copyweb-' + state;
        let matched = false;
        for (const node of document.querySelectorAll('[src],[href],[poster],a')) {
          for (const attr of ['src','href','poster']) {
            const value = node.getAttribute(attr); if (!value) continue;
            try {
              if (new URL(value, location.href).href === absolute) {
                node.classList.remove('copyweb-saved','copyweb-pending','copyweb-failed');
                node.classList.add(cls); matched = true;
              }
            } catch {}
          }
        }
        if (!matched && (absolute === location.href || absolute.split('#')[0] === location.href.split('#')[0])) {
          document.documentElement.classList.remove('copyweb-saved','copyweb-pending','copyweb-failed');
          document.documentElement.classList.add(cls);
        }
      } catch {}
    }
  };
  window.CopyWebMark = (data) => { if (data && data.url) { states.set(data.url, data.state || 'pending'); applyMarks(); } };
  new MutationObserver(applyMarks).observe(document.documentElement, { childList: true, subtree: true, attributes: true, attributeFilter: ['src','href','poster'] });
})();
""";
}
