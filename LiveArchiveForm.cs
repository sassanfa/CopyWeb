using System.Collections.Concurrent;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using CopyWeb.Models;
using CopyWeb.Services;
using ProjectLinkState = CopyWeb.Models.LinkState;

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
    private readonly bool _suppressStartupErrorDialog;
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
    private readonly DateTime _startedAt = DateTime.UtcNow;
    private Label? _resourcesTitle;
    private Label? _savedSummary;
    private Label? _pendingSummary;
    private Label? _queuedSummary;
    private Label? _failedSummary;
    private Label? _overallPercent;
    private Label? _speedSummary;
    private Label? _etaSummary;
    private DashboardProgressBar? _overallProgress;
    private ProgressChartPanel? _chart;
    private ProgressDonutPanel? _donut;

    public LiveArchiveForm(Uri root, string outputDirectory, bool manualNavigation = false, bool suppressStartupErrorDialog = false)
    {
        _root = root;
        _output = Path.GetFullPath(outputDirectory);
        _manualNavigation = manualNavigation;
        _suppressStartupErrorDialog = suppressStartupErrorDialog;
        Text = manualNavigation ? "CopyWeb — کپی وبی" : "CopyWeb — ذخیره زنده سایت";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1180, 760);
        MinimumSize = new Size(860, 560);
        Font = UiTheme.NormalFont;
        BackColor = UiTheme.Background;
        RightToLeft = RightToLeft.Yes;
        BuildUi();
        UiTheme.StyleDialog(this);
        Shown += InitializeAsync;
        FormClosed += (_, _) =>
        {
            _closing = true;
            try { RewriteSavedFilesAsync().GetAwaiter().GetResult(); } catch { }
            SaveManifest();
            SaveProjectCheckpoint();
            _saveGate.Dispose();
            _rewriteGate.Dispose();
            _fallbackClient.Dispose();
        };
    }

    private void BuildUi()
    {
        var canvas = UiTheme.Background;
        var ink = UiTheme.Text;
        var muted = UiTheme.Muted;
        var secondary = ControlPaint.Light(UiTheme.Surface, 0.055F);
        var browserChrome = Color.FromArgb(17, 22, 34);

        BackColor = canvas;
        var top = new Panel { Dock = DockStyle.Top, Height = _manualNavigation ? 118 : 76, BackColor = UiTheme.Surface, Padding = new Padding(20, 10, 20, 8), RightToLeft = RightToLeft.No };
        var title = UiTheme.Label(_manualNavigation ? "حالت کپی وبی" : "حالت ذخیره زنده", 17, FontStyle.Bold, ink);
        title.AutoSize = false; title.Location = new Point(18, 8); title.Size = new Size(430, 30); title.TextAlign = ContentAlignment.MiddleLeft;
        _status.AutoSize = false; _status.Location = new Point(18, 40); _status.Size = new Size(760, 24); _status.TextAlign = ContentAlignment.MiddleLeft; _status.ForeColor = muted;
        top.Controls.Add(title); top.Controls.Add(_status);

        if (_manualNavigation)
        {
            var navigation = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 38, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(0, 3, 0, 0), BackColor = UiTheme.Surface, RightToLeft = RightToLeft.No };
            var go = UiTheme.Button("رفتن", UiTheme.Action); go.Width = 76; go.Height = 30; go.Click += (_, _) => NavigateAddress();
            var back = UiTheme.Button("عقب", secondary); back.Width = 70; back.Height = 30; back.ForeColor = ink; back.Click += (_, _) => { if (_browser.CanGoBack) _browser.GoBack(); };
            var forward = UiTheme.Button("جلو", secondary); forward.Width = 70; forward.Height = 30; forward.ForeColor = ink; forward.Click += (_, _) => { if (_browser.CanGoForward) _browser.GoForward(); };
            _address = new TextBox { Width = 650, Height = 28, Margin = new Padding(8, 0, 8, 0), RightToLeft = RightToLeft.No, TextAlign = HorizontalAlignment.Left, BorderStyle = BorderStyle.FixedSingle, BackColor = secondary, ForeColor = ink };
            _address.Text = _root.AbsoluteUri;
            _address.KeyDown += (_, args) => { if (args.KeyCode == Keys.Enter) { args.SuppressKeyPress = true; NavigateAddress(); } };
            navigation.Controls.Add(go); navigation.Controls.Add(back); navigation.Controls.Add(forward); navigation.Controls.Add(_address); top.Controls.Add(navigation);
        }

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Padding = new Padding(18, 6, 18, 8), BackColor = canvas, RightToLeft = RightToLeft.No };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23));

        var resourcesCard = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Surface, Padding = new Padding(14), BorderStyle = BorderStyle.None, RightToLeft = RightToLeft.No };
        _resourcesTitle = UiTheme.Label("صفحات در انتظار ذخیره (۰)", 11, FontStyle.Bold, ink); _resourcesTitle.Dock = DockStyle.Top; _resourcesTitle.Height = 34; _resourcesTitle.TextAlign = ContentAlignment.MiddleLeft;
        var legend = UiTheme.Label("سبز: ذخیره شد  |  آبی: در حال ذخیره  |  قرمز: خطا", 8, color: muted); legend.Dock = DockStyle.Bottom; legend.Height = 24; legend.TextAlign = ContentAlignment.MiddleLeft;
        _resources.Columns.Add("وضعیت", 88); _resources.Columns.Add("نوع", 70); _resources.Columns.Add("آدرس", 250);
        _resources.BackColor = UiTheme.Surface; _resources.ForeColor = ink; _resources.BorderStyle = BorderStyle.None; _resources.RightToLeft = RightToLeft.No; _resources.RightToLeftLayout = false;
        resourcesCard.Controls.Add(_resources); resourcesCard.Controls.Add(legend); resourcesCard.Controls.Add(_resourcesTitle);

        var browserCard = new Panel { Dock = DockStyle.Fill, BackColor = browserChrome, Padding = new Padding(1), BorderStyle = BorderStyle.None, RightToLeft = RightToLeft.No };
        var browserBar = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = browserChrome, Padding = new Padding(14, 7, 14, 7) };
        var browserAddress = UiTheme.Label("🔒  " + _root.AbsoluteUri, 9, color: Color.White); browserAddress.Dock = DockStyle.Fill; browserAddress.TextAlign = ContentAlignment.MiddleLeft; browserAddress.RightToLeft = RightToLeft.No; browserBar.Controls.Add(browserAddress);
        browserCard.Controls.Add(_browser); browserCard.Controls.Add(browserBar);

        var dashboard = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Surface, Padding = new Padding(14), BorderStyle = BorderStyle.None, RightToLeft = RightToLeft.No };
        var dashboardTitle = UiTheme.Label("نمودار پیشرفت ذخیره‌سازی", 11, FontStyle.Bold, ink); dashboardTitle.Dock = DockStyle.Top; dashboardTitle.Height = 30; dashboardTitle.TextAlign = ContentAlignment.MiddleLeft;
        _chart = new ProgressChartPanel { Dock = DockStyle.Top, Height = 170, BackColor = UiTheme.Surface };
        _overallPercent = UiTheme.Label("۰٪", 18, FontStyle.Bold, UiTheme.Accent); _overallPercent.Dock = DockStyle.Top; _overallPercent.Height = 32; _overallPercent.TextAlign = ContentAlignment.MiddleCenter;
        _overallProgress = new DashboardProgressBar { Dock = DockStyle.Top, Height = 10, TrackColor = UiTheme.Border, FillColor = UiTheme.Action };
        var summaryTitle = UiTheme.Label("خلاصه ذخیره‌سازی", 11, FontStyle.Bold, ink); summaryTitle.Dock = DockStyle.Top; summaryTitle.Height = 32; summaryTitle.TextAlign = ContentAlignment.MiddleLeft;
        var metrics = new TableLayoutPanel { Dock = DockStyle.Top, Height = 142, ColumnCount = 2, RowCount = 2, BackColor = UiTheme.Surface };
        metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55)); metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        _savedSummary = MetricLabel("تکمیل شده\n۰", UiTheme.Accent);
        _pendingSummary = MetricLabel("در حال ذخیره\n۰", Color.FromArgb(117, 119, 255));
        _queuedSummary = MetricLabel("در صف\n۰", muted);
        _failedSummary = MetricLabel("خطا\n۰", Color.FromArgb(236, 116, 126));
        metrics.Controls.Add(_savedSummary, 0, 0); metrics.Controls.Add(_pendingSummary, 1, 0); metrics.Controls.Add(_queuedSummary, 0, 1); metrics.Controls.Add(_failedSummary, 1, 1);
        _speedSummary = UiTheme.Label("سرعت فعلی: —", 9, color: muted); _speedSummary.Dock = DockStyle.Top; _speedSummary.Height = 28; _speedSummary.TextAlign = ContentAlignment.MiddleLeft;
        _etaSummary = UiTheme.Label("زمان باقی‌مانده: —", 9, color: muted); _etaSummary.Dock = DockStyle.Top; _etaSummary.Height = 28; _etaSummary.TextAlign = ContentAlignment.MiddleLeft;
        _donut = new ProgressDonutPanel { Dock = DockStyle.Fill, BackColor = UiTheme.Surface };
        dashboard.Controls.Add(_donut); dashboard.Controls.Add(_etaSummary); dashboard.Controls.Add(_speedSummary); dashboard.Controls.Add(metrics); dashboard.Controls.Add(summaryTitle); dashboard.Controls.Add(_overallProgress); dashboard.Controls.Add(_overallPercent); dashboard.Controls.Add(_chart); dashboard.Controls.Add(dashboardTitle);

        body.Controls.Add(resourcesCard, 0, 0); body.Controls.Add(browserCard, 1, 0); body.Controls.Add(dashboard, 2, 0);

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 66, Padding = new Padding(20, 10, 20, 10), FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = UiTheme.Surface, RightToLeft = RightToLeft.No };
        var close = LiveButton("بستن", UiTheme.Action, Color.White, 148); close.Click += (_, _) => Close();
        var open = LiveButton("باز کردن پوشه", secondary, ink, 178); open.Click += (_, _) => OpenFolder();
        var retry = LiveButton("تلاش دوباره", UiTheme.Accent, Color.White, 162); retry.Click += async (_, _) => await RetrySelectedAsync();
        var remove = LiveButton("حذف مورد", UiTheme.Danger, Color.White, 162); remove.Click += (_, _) => DeleteSelected();
        var backButton = LiveButton("بازگشت", secondary, ink, 140); backButton.Click += (_, _) => GoBackOrClose();
        bottom.Controls.Add(close); bottom.Controls.Add(open); bottom.Controls.Add(retry); bottom.Controls.Add(remove); bottom.Controls.Add(backButton);
        Controls.Add(body); Controls.Add(bottom); Controls.Add(top);
    }

    private static Label MetricLabel(string text, Color color) => new()
    {
        Text = text, Dock = DockStyle.Fill, AutoSize = false, TextAlign = ContentAlignment.MiddleCenter,
        Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = color, BackColor = ControlPaint.Light(UiTheme.Surface, 0.035F), Margin = new Padding(3)
    };

    private static Button LiveButton(string text, Color backColor, Color foreColor, int width)
    {
        var button = UiTheme.Button(text, backColor); button.Width = width; button.Height = 42; button.ForeColor = foreColor; button.FlatAppearance.BorderSize = 0; return button;
    }

    private void BuildLegacyUi()
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
            var userDataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CopyWeb",
                "WebView2",
                $"Live-{Environment.ProcessId}");
            Directory.CreateDirectory(userDataDirectory);
            var environment = await CoreWebView2Environment.CreateAsync(null, userDataDirectory, options);
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
            _browser.CoreWebView2.NavigationCompleted += async (_, args) =>
            {
                var current = _browser.Source?.AbsoluteUri ?? _root.AbsoluteUri;
                if (_address is not null) _address.Text = current;
                if (Uri.TryCreate(current, UriKind.Absolute, out var pageUri) && pageUri.Scheme is ("http" or "https")) _visitedPages[pageUri.AbsoluteUri] = args.IsSuccess ? "ذخیره شد" : "خطا";
                if (!args.IsSuccess) MarkUrl(current, "failed", args.WebErrorStatus.ToString());
                else
                {
                    MarkUrl(current, "saved");
                    await RecoverRenderedImagesAsync();
                }
                SaveProjectCheckpoint();
            };
            _browser.CoreWebView2.Navigate(_root.AbsoluteUri);
            _status.Text = $"در حال نمایش {_root.AbsoluteUri} — منابع ذخیره‌شده: ۰";
        }
        catch (Exception ex)
        {
            _status.Text = "WebView2 در دسترس نیست: " + ex.Message;
            if (!_suppressStartupErrorDialog)
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
            if (statusCode >= 400) throw new InvalidOperationException($"HTTP {statusCode}");
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
                var sniffed = SniffImageExtension(bytes);
                if (sniffed is not null) contentType = MimeForImageExtension(sniffed);
                var kind = sniffed is not null ? "تصویر" : KindFor(uri, contentType);
                if (kind == "تصویر" && LooksLikeHtml(bytes)) throw new InvalidDataException("به‌جای تصویر، صفحهٔ HTML یا پاسخ امنیتی دریافت شد");
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
            _seenUrls.TryRemove(key, out _);
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

    private async Task RecoverRenderedImagesAsync()
    {
        if (_closing || _browser.CoreWebView2 is null) return;
        const string script = """
        (() => {
          const found = new Set();
          const add = value => {
            if (!value) return;
            try { found.add(new URL(value, location.href).href); } catch {}
          };
          for (const node of document.querySelectorAll('img,source,picture,video,[style]')) {
            add(node.currentSrc);
            for (const name of ['src','poster','data-src','data-lazy-src','data-original','data-url','data-image','data-image-src','data-bg','data-background','data-background-image']) add(node.getAttribute?.(name));
            for (const name of ['srcset','data-srcset','data-lazy-srcset']) {
              const set = node.getAttribute?.(name) || '';
              for (const part of set.split(',')) add(part.trim().split(/\s+/, 1)[0]);
            }
            const background = getComputedStyle(node).backgroundImage || '';
            for (const match of background.matchAll(/url\((['"]?)(.*?)\1\)/gi)) add(match[2]);
          }
          for (const entry of performance.getEntriesByType('resource')) {
            const name = entry.name || '';
            const initiator = (entry.initiatorType || '').toLowerCase();
            if (initiator === 'img' || /\.(webp|avif)(?:$|[?#])/i.test(name) || /(?:format|fm)=webp/i.test(name)) add(name);
          }
          const markup = (document.documentElement?.outerHTML || '')
            .replaceAll('\\/', '/')
            .replace(/\\u002f/gi, '/');
          for (const match of markup.matchAll(/(?:(?:https?:)?\/\/|\/|\.{1,2}\/)?[a-z0-9_%.-]+(?:\/[a-z0-9_%.-]+)+\.(?:webp|avif)(?:\?[^"'\\s<>),;]*)?/gi)) add(match[0]);
          return [...found];
        })()
        """;
        try
        {
            var raw = await _browser.CoreWebView2.ExecuteScriptAsync(script);
            var urls = JsonSerializer.Deserialize<List<string>>(raw) ?? [];
            foreach (var rawUrl in urls.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (_closing || !Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) continue;
                await CaptureRecoveredImageAsync(uri);
            }
        }
        catch
        {
            // Network response capture remains the primary path. This DOM sweep
            // is a recovery path for memory/disk cache and service-worker images.
        }
    }

    private async Task CaptureRecoveredImageAsync(Uri uri)
    {
        var key = UrlTools.ResourceCacheKey(uri);
        if (!_seenUrls.TryAdd(key, 0)) return;
        var row = AddRow(uri.AbsoluteUri, "در انتظار ۰٪", "بازیابی تصویر مرورگر");
        Interlocked.Increment(ref _pending);
        try
        {
            var responseData = await DownloadWithBrowserSessionAsync(uri, percent => UpdateRowProgress(row, percent));
            if (responseData is null || responseData.Value.Bytes.Length == 0) throw new InvalidOperationException("تصویر رندرشده قابل دریافت نبود");
            var bytes = responseData.Value.Bytes;
            var extension = SniffImageExtension(bytes);
            var contentType = extension is null ? responseData.Value.ContentType : MimeForImageExtension(extension);
            if (extension is null && !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("پاسخ دریافت‌شده تصویر نیست");
            if (LooksLikeHtml(bytes)) throw new InvalidDataException("به‌جای تصویر، صفحهٔ HTML یا پاسخ امنیتی دریافت شد");
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
            var path = await SaveBytesAsync(uri, "تصویر", contentType, hash, bytes);
            _urlToFile[key] = path;
            _fileUrls[path] = uri.AbsoluteUri;
            ScheduleRewrite();
            UpdateRow(row, "موفق ۱۰۰٪", "تصویر", path);
            Interlocked.Increment(ref _saved);
            MarkUrl(uri.AbsoluteUri, "saved");
        }
        catch (Exception ex)
        {
            _seenUrls.TryRemove(key, out _);
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

    private async Task<(byte[] Bytes, string ContentType)?> DownloadWithBrowserSessionAsync(Uri uri, Action<int> progress)
    {
        try
        {
            var cookieHeader = await ReadCookieHeaderAsync(uri).ConfigureAwait(false);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36");
            request.Headers.TryAddWithoutValidation("Accept", "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");
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
            return bytes.Length == 0 ? null : (bytes, response.Content.Headers.ContentType?.MediaType ?? string.Empty);
        }
        catch { return null; }
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
            var sniffed = SniffImageExtension(bytes);
            if (sniffed is not null) contentType = MimeForImageExtension(sniffed);
            var kind = sniffed is not null ? "تصویر" : KindFor(uri, contentType);
            if (kind == "تصویر" && LooksLikeHtml(bytes)) throw new InvalidDataException("به‌جای تصویر، صفحهٔ HTML یا پاسخ امنیتی دریافت شد");
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
            _seenUrls.TryRemove(key, out _);
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
        var row = new ListViewItem(status) { ForeColor = UiTheme.Text };
        row.SubItems.Add("-");
        row.SubItems.Add(url);
        row.Tag = url;
        row.BackColor = UiTheme.DarkMode
            ? ControlPaint.Light(UiTheme.Surface, 0.055F)
            : Color.FromArgb(239, 246, 255);
        _resources.Items.Add(row);
        UpdateDashboard();
        return row;
    }

    private void UpdateRow(ListViewItem row, string status, string kind, string file)
    {
        if (IsDisposed) return;
        void Update()
        {
            row.Text = status; row.SubItems[1].Text = kind; row.SubItems[2].Text = file;
            row.ForeColor = UiTheme.Text;
            row.BackColor = status.StartsWith("موفق", StringComparison.Ordinal)
                ? UiTheme.DarkMode ? Color.FromArgb(35, 76, 68) : Color.FromArgb(220, 252, 231)
                : UiTheme.DarkMode ? Color.FromArgb(78, 45, 59) : Color.FromArgb(254, 226, 226);
            UpdateDashboard();
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
            row.ForeColor = UiTheme.Text;
            row.BackColor = UiTheme.DarkMode
                ? ControlPaint.Light(UiTheme.Surface, 0.055F)
                : Color.FromArgb(239, 246, 255);
            UpdateDashboard();
        }
        if (InvokeRequired) BeginInvoke((Action)Update); else Update();
    }

    private void UpdateStatus()
    {
        if (IsDisposed) return;
        void Update() { _status.Text = $"ذخیره زنده فعال — موفق: {_saved:N0} | در انتظار: {_pending:N0} | خطا: {_failed:N0}"; UpdateDashboard(); }
        if (InvokeRequired) BeginInvoke((Action)Update); else Update();
    }

    private void UpdateDashboard()
    {
        if (IsDisposed || !IsHandleCreated) return;
        if (InvokeRequired) { try { BeginInvoke((Action)UpdateDashboard); } catch { } return; }
        var total = _resources.Items.Count;
        var completed = _saved + _failed;
        var queued = Math.Max(0, total - completed - _pending);
        var percent = total == 0 ? 0 : Math.Clamp((int)Math.Round(completed * 100d / total), 0, 100);
        if (_resourcesTitle is not null) _resourcesTitle.Text = $"صفحات در انتظار ذخیره ({total:N0})";
        if (_savedSummary is not null) _savedSummary.Text = $"تکمیل شده\n{_saved:N0}";
        if (_pendingSummary is not null) _pendingSummary.Text = $"در حال ذخیره\n{_pending:N0}";
        if (_queuedSummary is not null) _queuedSummary.Text = $"در صف\n{queued:N0}";
        if (_failedSummary is not null) _failedSummary.Text = $"خطا\n{_failed:N0}";
        if (_overallPercent is not null) _overallPercent.Text = $"{percent}%";
        if (_overallProgress is not null) _overallProgress.Value = percent;
        var seconds = Math.Max(1, (DateTime.UtcNow - _startedAt).TotalSeconds);
        var rate = completed / seconds;
        if (_speedSummary is not null) _speedSummary.Text = $"سرعت فعلی: {rate:0.00} فایل/ثانیه";
        if (_etaSummary is not null) _etaSummary.Text = $"زمان باقی‌مانده: {(rate > 0 ? TimeSpan.FromSeconds((queued + _pending) / rate).ToString(@"hh\:mm\:ss") : "—")}";
        _chart?.SetProgress(percent);
        _donut?.SetCounts(_saved, _pending, queued, _failed);
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

    private void SaveProjectCheckpoint()
    {
        try
        {
            var resources = _urlToFile
                .Where(pair => File.Exists(pair.Value))
                .Where(pair => !pair.Value.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                .Select(pair => new ResourceItem
                {
                    Url = _fileUrls.TryGetValue(pair.Value, out var source) ? source : pair.Key,
                    Kind = ResourceKindForPath(pair.Value),
                    SizeBytes = new FileInfo(pair.Value).Length,
                    State = ProjectLinkState.Downloaded,
                    IsSelected = true
                })
                .ToList();
            var pages = _visitedPages
                .Select(pair => new DownloadItem
                {
                    Url = pair.Key,
                    Title = "ذخیره زنده",
                    State = pair.Value == "ذخیره شد" ? ProjectLinkState.Downloaded :
                        pair.Value == "خطا" ? ProjectLinkState.Failed : ProjectLinkState.Pending,
                    Error = pair.Value == "خطا" ? "ذخیره صفحه کامل نشد" : null,
                    Resources = pair.Key.Equals(_root.AbsoluteUri, StringComparison.OrdinalIgnoreCase) ? resources : []
                })
                .ToList();
            if (pages.Count == 0)
            {
                pages.Add(new DownloadItem
                {
                    Url = _root.AbsoluteUri,
                    Title = "ذخیره زنده",
                    State = File.Exists(Path.Combine(_output, "index.html")) ? ProjectLinkState.Downloaded : ProjectLinkState.Pending,
                    Resources = resources
                });
            }
            else if (!pages.Any(page => page.Resources.Count > 0))
            {
                pages[0].Resources = resources;
            }
            ProjectStorage.SaveAsync(Path.Combine(_output, "links.json"), _root, pages).GetAwaiter().GetResult();
        }
        catch
        {
            // The manifest remains usable if the optional Projects checkpoint
            // cannot be updated while the browser is shutting down.
        }
    }

    private static ResourceKind ResourceKindForPath(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".avif" or ".svg" or ".ico" or ".bmp" or ".jxl" => ResourceKind.Image,
            ".css" => ResourceKind.Stylesheet,
            ".js" or ".mjs" => ResourceKind.Script,
            ".woff" or ".woff2" or ".ttf" or ".otf" or ".eot" => ResourceKind.Font,
            ".mp4" or ".webm" or ".mp3" or ".ogg" => ResourceKind.Media,
            _ => ResourceKind.Other
        };

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
            "HTML" => ".html",
            "CSS" => ".css",
            "JS" => ".js",
            "تصویر" when contentType.Contains("webp", StringComparison.OrdinalIgnoreCase) => ".webp",
            "تصویر" when contentType.Contains("avif", StringComparison.OrdinalIgnoreCase) => ".avif",
            "تصویر" when contentType.Contains("png", StringComparison.OrdinalIgnoreCase) => ".png",
            "تصویر" when contentType.Contains("jpeg", StringComparison.OrdinalIgnoreCase) => ".jpg",
            "تصویر" when contentType.Contains("gif", StringComparison.OrdinalIgnoreCase) => ".gif",
            "تصویر" when contentType.Contains("svg", StringComparison.OrdinalIgnoreCase) => ".svg",
            "تصویر" => ".img",
            "فونت" => ".bin",
            _ => ".bin"
        };
    }

    private static string? SniffImageExtension(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8)) return ".webp";
        if (bytes.Length >= 12 && bytes.Slice(4, 4).SequenceEqual("ftyp"u8))
        {
            var brand = Encoding.ASCII.GetString(bytes.Slice(8, Math.Min(24, bytes.Length - 8)));
            if (brand.Contains("avif", StringComparison.Ordinal) || brand.Contains("avis", StringComparison.Ordinal)) return ".avif";
        }
        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return ".png";
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return ".jpg";
        if (bytes.Length >= 6 && (bytes[..6].SequenceEqual("GIF87a"u8) || bytes[..6].SequenceEqual("GIF89a"u8))) return ".gif";
        return null;
    }

    private static string MimeForImageExtension(string extension) => extension switch
    {
        ".webp" => "image/webp",
        ".avif" => "image/avif",
        ".png" => "image/png",
        ".jpg" => "image/jpeg",
        ".gif" => "image/gif",
        _ => "image/*"
    };

    private static bool LooksLikeHtml(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty) return false;
        var sample = Encoding.UTF8.GetString(bytes[..Math.Min(bytes.Length, 512)]).TrimStart('\uFEFF', '\0', ' ', '\t', '\r', '\n');
        return sample.StartsWith("<!doctype html", StringComparison.OrdinalIgnoreCase) ||
               sample.StartsWith("<html", StringComparison.OrdinalIgnoreCase);
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

    private sealed class ProgressChartPanel : Panel
    {
        private readonly List<int> _values = [0];

        public ProgressChartPanel() => DoubleBuffered = true;

        public void SetProgress(int value)
        {
            _values.Add(Math.Clamp(value, 0, 100));
            if (_values.Count > 18) _values.RemoveAt(0);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var grid = new Pen(UiTheme.Border, 1);
            for (var i = 1; i <= 4; i++)
            {
                var y = Height - 18 - (Height - 32) * i / 4f;
                g.DrawLine(grid, 26, y, Width - 10, y);
            }
            if (_values.Count < 2) return;
            var points = new PointF[_values.Count];
            for (var i = 0; i < _values.Count; i++)
            {
                var x = 26 + (Width - 38) * i / (float)Math.Max(1, _values.Count - 1);
                var y = Height - 18 - (Height - 32) * _values[i] / 100f;
                points[i] = new PointF(x, y);
            }
            using var area = new SolidBrush(Color.FromArgb(52, UiTheme.Action));
            var polygon = new PointF[points.Length + 2]; Array.Copy(points, polygon, points.Length); polygon[^2] = new PointF(points[^1].X, Height - 18); polygon[^1] = new PointF(points[0].X, Height - 18); g.FillPolygon(area, polygon);
            using var line = new Pen(UiTheme.Action, 2.5f); g.DrawLines(line, points);
            using var dot = new SolidBrush(UiTheme.Accent);
            foreach (var p in points) g.FillEllipse(dot, p.X - 4, p.Y - 4, 8, 8);
        }
    }

    private sealed class ProgressDonutPanel : Panel
    {
        private int _saved;
        private int _pending;
        private int _queued;
        private int _failed;

        public ProgressDonutPanel() => DoubleBuffered = true;

        public void SetCounts(int saved, int pending, int queued, int failed)
        {
            _saved = saved; _pending = pending; _queued = queued; _failed = failed; Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var total = Math.Max(1, _saved + _pending + _queued + _failed);
            var completed = (int)Math.Round(_saved * 360d / total);
            var pending = (int)Math.Round(_pending * 360d / total);
            var queued = (int)Math.Round(_queued * 360d / total);
            var bounds = new Rectangle(Math.Max(8, Width / 2 - 58), 12, 116, 116);
            using var track = new Pen(UiTheme.Border, 14); e.Graphics.DrawArc(track, bounds, 0, 360);
            using var green = new Pen(UiTheme.Accent, 14); e.Graphics.DrawArc(green, bounds, -90, completed);
            using var blue = new Pen(UiTheme.Action, 14); e.Graphics.DrawArc(blue, bounds, -90 + completed, pending);
            using var gray = new Pen(UiTheme.Muted, 14); e.Graphics.DrawArc(gray, bounds, -90 + completed + pending, queued);
            var percent = _saved * 100 / total;
            using var text = new SolidBrush(UiTheme.Text);
            using var font = new Font("Segoe UI", 16, FontStyle.Bold);
            var value = percent + "%"; var size = e.Graphics.MeasureString(value, font); e.Graphics.DrawString(value, font, text, Width / 2f - size.Width / 2f, 58);
            using var small = new Font("Segoe UI", 8); var caption = "تکمیل شده"; var captionSize = e.Graphics.MeasureString(caption, small); e.Graphics.DrawString(caption, small, text, Width / 2f - captionSize.Width / 2f, 82);
        }
    }
}
