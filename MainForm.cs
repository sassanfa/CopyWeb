using CopyWeb.Models;
using CopyWeb.Services;
using LinkState = CopyWeb.Models.LinkState;
using System.Diagnostics;
using System.Text;

namespace CopyWeb;

public partial class MainForm : Form
{
    private readonly TextBox _url = new();
    private readonly TextBox _output = new();
    private readonly NumericUpDown _depth = new();
    private readonly NumericUpDown _maxPages = new();
    private readonly CheckBox _subdomains = new();
    private readonly CheckBox _robots = new();
    private readonly CheckBox _sitemaps = new();
    private readonly CheckBox _canonical = new();
    private readonly CheckBox _proxyEnabled = new();
    private readonly ComboBox _proxyType = new();
    private readonly TextBox _proxyAddress = new();
    private readonly TextBox _proxyPort = new();
    private readonly TextBox _proxyUser = new();
    private readonly TextBox _proxyPassword = new();
    private readonly NumericUpDown _timeoutSeconds = new();
    private readonly NumericUpDown _retryCount = new();
    private readonly NumericUpDown _requestDelay = new();
    private readonly NumericUpDown _concurrency = new();
    private readonly NumericUpDown _minFreeDisk = new();
    private readonly NumericUpDown _speedLimit = new();
    private readonly NumericUpDown _domainConnections = new();
    private readonly ProgressBar _progress = new();
    private readonly ProgressBar _fileProgress = new();
    private readonly Label _progressCaption = UiTheme.Label("پیشرفت کل پروژه", 8, color: UiTheme.Muted);
    private readonly Label _fileProgressCaption = UiTheme.Label("پیشرفت فایل جاری", 8, color: UiTheme.Muted);
    private readonly Label _status = UiTheme.Label("آماده شروع", 9, color: UiTheme.Muted);
    private readonly Label _currentFile = UiTheme.Label("فایل فعلی: -", 9, color: UiTheme.Muted);
    private readonly Label _stats = UiTheme.Label("هنوز عملیاتی انجام نشده است", 10, FontStyle.Bold);
    private readonly Label _counts = UiTheme.Label("صفحات: ۰ | دانلودشده: ۰ | ناموفق: ۰", 9, color: UiTheme.Muted);
    private readonly Label _speed = UiTheme.Label("دانلود: - | ارسال: ۰ B/s", 9, color: UiTheme.Muted);
    private readonly Label _eta = UiTheme.Label("زمان باقی‌مانده: -", 9, color: UiTheme.Muted);
    private readonly RichTextBox _log = new();
    private readonly Button _start = UiTheme.Button("شروع بررسی سایت");
    private readonly Button _resume = UiTheme.Button("ادامه پروژه", UiTheme.Accent);
    private readonly Button _stop = UiTheme.Button("توقف و ذخیره", UiTheme.Danger);
    private readonly Button _testProxy = UiTheme.Button("تست پروکسی", Color.FromArgb(210, 222, 238));
    private CancellationTokenSource? _cts;
    private string? _activeLogPath;
    private Stopwatch? _operationClock;
    private bool _approveCaptchaForOperation;
    private IReadOnlyList<BrowserCookie> _captchaCookies = [];
    private DownloadMonitorForm? _downloadMonitor;
    private LocalApiServer? _apiServer;

    static MainForm()
    {
        UiTheme.Apply(AppSettingsStore.Load());
    }

    public MainForm()
    {
        Text = "CopyWeb | دریافت نسخه آفلاین سایت";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1120, 760);
        Size = new Size(1240, 850);
        Font = UiTheme.NormalFont;
        RightToLeft = RightToLeft.Yes;
        BackColor = UiTheme.Background;
        InitializeComponent();
        Icon = LoadApplicationIcon();
        ShowIcon = true;
        BuildUi();
        Localization.Apply(this, AppSettingsStore.Load().Language);
        AllowDrop = true;
        DragEnter += (_, e) => e.Effect = (e.Data?.GetDataPresent(DataFormats.Text) == true || e.Data?.GetDataPresent(DataFormats.FileDrop) == true) ? DragDropEffects.Copy : DragDropEffects.None;
        DragDrop += (_, e) => HandleDrop(e.Data);
        FormClosed += (_, _) => _apiServer?.Dispose();
        StartLocalApi();
    }

    private void BuildUi()
    {
        var root = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Background, Tag = "background" };
        var sidebar = BuildSidebar();
        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(26, 22, 26, 18), BackColor = UiTheme.Background };
        root.Controls.Add(content);
        root.Controls.Add(sidebar);

        var top = new Panel { Dock = DockStyle.Top, Height = 104, BackColor = Color.Transparent };
        var topLine = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = UiTheme.Border, Tag = "border" };
        var heading = UiTheme.Label("شروع دانلود سایت", 21, FontStyle.Bold);
        heading.AutoSize = false;
        heading.Dock = DockStyle.Top;
        heading.Height = 40;
        heading.Padding = new Padding(0, 0, 72, 0);
        heading.TextAlign = ContentAlignment.MiddleRight;
        var subheading = UiTheme.Label("آدرس سایت مورد نظر خود را وارد کنید و تنظیمات را انتخاب نمایید.", 10, color: UiTheme.Muted);
        subheading.AutoSize = false;
        subheading.Dock = DockStyle.Top;
        subheading.Height = 32;
        subheading.Padding = new Padding(0, 0, 72, 0);
        subheading.TextAlign = ContentAlignment.MiddleRight;
        var globe = UiTheme.Label("◎", 32, FontStyle.Bold, UiTheme.Primary);
        globe.AutoSize = false;
        globe.Dock = DockStyle.Right;
        globe.Size = new Size(58, 58);
        globe.TextAlign = ContentAlignment.MiddleCenter;
        top.Controls.Add(topLine);
        top.Controls.Add(subheading);
        top.Controls.Add(heading);
        top.Controls.Add(globe);
        content.Controls.Add(top);

        var rightColumn = new Panel { Dock = DockStyle.Right, Width = 298, Padding = new Padding(14, 0, 0, 0) };
        var leftColumn = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 14, 0) };
        content.Controls.Add(leftColumn);
        content.Controls.Add(rightColumn);

        var operations = UiTheme.Card(); operations.Dock = DockStyle.Top; operations.Height = 376;
        var operationsTitle = UiTheme.Label("عملیات", 13, FontStyle.Bold); operationsTitle.Location = new Point(22, 18);
        _start.Width = 242; _start.Height = 48; _start.Location = new Point(22, 58); _start.Click += StartClick;
        _resume.Tag = "accent-button"; _resume.Width = 242; _resume.Height = 48; _resume.Location = new Point(22, 116); _resume.Click += ResumeClick;
        _stop.Tag = "danger-button"; _stop.Width = 242; _stop.Height = 48; _stop.Location = new Point(22, 174); _stop.Enabled = false; _stop.Click += (_, _) => _cts?.Cancel();
        _testProxy.Tag = "secondary-button"; _testProxy.ForeColor = UiTheme.Text; _testProxy.Width = 242; _testProxy.Height = 48; _testProxy.Location = new Point(22, 232); _testProxy.Click += TestProxyClick;
        var tutorial = UiTheme.Button("آموزش", Color.FromArgb(226, 231, 239)); tutorial.Tag = "secondary-button"; tutorial.ForeColor = UiTheme.Text; tutorial.Width = 242; tutorial.Height = 48; tutorial.Location = new Point(22, 290); tutorial.Click += (_, _) => ShowTutorial();
        operations.Controls.AddRange([operationsTitle, _start, _resume, _stop, _testProxy, tutorial]);

        var info = UiTheme.Card(); info.Dock = DockStyle.Fill; info.Margin = new Padding(0, 14, 0, 0);
        var infoTitle = UiTheme.Label("اطلاعات پروژه", 12, FontStyle.Bold); infoTitle.Location = new Point(22, 18); infoTitle.AutoSize = false; infoTitle.Width = 242; infoTitle.Height = 24; infoTitle.TextAlign = ContentAlignment.MiddleRight;
        _status.Location = new Point(22, 55); _currentFile.Location = new Point(22, 86); _stats.Location = new Point(22, 122);
        _counts.Location = new Point(22, 150); _speed.Location = new Point(22, 176); _eta.Location = new Point(22, 202);
        foreach (var label in new[] { _status, _currentFile, _stats, _counts, _speed, _eta })
        {
            label.AutoSize = false;
            label.Height = 22;
            label.Width = 242;
            label.TextAlign = ContentAlignment.MiddleRight;
            label.AutoEllipsis = true;
        }
        // Keep the progress captions below the ETA line so their text is never clipped.
        _progressCaption.Location = new Point(22, 230); _progressCaption.Width = 242;
        _fileProgressCaption.Location = new Point(22, 278); _fileProgressCaption.Width = 242;
        _progress.Location = new Point(22, 248); _progress.Width = 242; _progress.Height = 20;
        _fileProgress.Location = new Point(22, 296); _fileProgress.Width = 242; _fileProgress.Height = 20;
        info.Controls.AddRange([infoTitle, _status, _currentFile, _stats, _counts, _speed, _eta, _progressCaption, _progress, _fileProgressCaption, _fileProgress]);
        rightColumn.Controls.Add(info); rightColumn.Controls.Add(operations);

        var settings = UiTheme.Card(); settings.Dock = DockStyle.Top; settings.Height = 342;
        var settingsTitle = UiTheme.Label("تنظیمات دانلود", 13, FontStyle.Bold); settingsTitle.Location = new Point(22, 18);
        var urlLabel = UiTheme.Label("آدرس سایت", 10, FontStyle.Bold); urlLabel.Location = new Point(22, 52);
        ConfigureInput(_url); _url.PlaceholderText = "https://example.com"; _url.Multiline = false; _url.AutoSize = false; _url.Height = 30; _url.Location = new Point(22, 125); _url.Width = 460;
        var pasteUrl = UiTheme.Button("چسباندن", Color.FromArgb(238, 243, 250)); pasteUrl.Tag = "secondary-button"; pasteUrl.ForeColor = UiTheme.Text; pasteUrl.Width = 82; pasteUrl.Height = 30; pasteUrl.Location = new Point(490, 125); pasteUrl.Click += (_, _) => PasteUrlFromClipboard();
        var urlLine = new Panel { BackColor = UiTheme.Border, Height = 1, Width = 460, Location = new Point(22, 175), Tag = "border" };
        var maxLabel = UiTheme.Label("حداکثر صفحه", 9, color: UiTheme.Muted); maxLabel.Location = new Point(22, 197);
        _maxPages.Minimum = 1; _maxPages.Maximum = 10000; _maxPages.Value = 500; _maxPages.Width = 130; _maxPages.Location = new Point(22, 223);
        var depthLabel = UiTheme.Label("عمق لینک", 9, color: UiTheme.Muted); depthLabel.Location = new Point(172, 197);
        _depth.Minimum = 0; _depth.Maximum = 20; _depth.Value = 3; _depth.Width = 130; _depth.Location = new Point(172, 223);
        _subdomains.Text = "شامل زیردامنه‌ها"; _subdomains.Checked = true; _subdomains.AutoSize = true; _subdomains.Location = new Point(322, 228);
        _robots.Text = "رعایت robots.txt"; _robots.Checked = true; _robots.AutoSize = true; _robots.Location = new Point(440, 228);
        _sitemaps.Text = "خواندن Sitemap"; _sitemaps.Checked = true; _sitemaps.AutoSize = true; _sitemaps.Location = new Point(22, 270);
        _canonical.Text = "پیروی از Canonical"; _canonical.Checked = true; _canonical.AutoSize = true; _canonical.Location = new Point(160, 270);
        settings.Controls.AddRange([settingsTitle, urlLabel, _url, pasteUrl, urlLine, maxLabel, _maxPages, depthLabel, _depth, _subdomains, _robots, _sitemaps, _canonical]);

        var proxy = UiTheme.Card(); proxy.Dock = DockStyle.Top; proxy.Height = 270;
        var proxyTitle = UiTheme.Label("احراز هویت پروکسی (اختیاری)", 12, FontStyle.Bold); proxyTitle.Location = new Point(22, 16);
        _proxyEnabled.Text = "فعال"; _proxyEnabled.AutoSize = true; _proxyEnabled.Location = new Point(235, 19); _proxyEnabled.CheckedChanged += (_, _) => UpdateProxyControls();
        _proxyType.DropDownStyle = ComboBoxStyle.DropDownList; _proxyType.Items.AddRange(["HTTP", "HTTPS", "SOCKS5"]); _proxyType.SelectedIndex = 0; _proxyType.Width = 95; _proxyType.Location = new Point(22, 58);
        ConfigureInput(_proxyAddress); _proxyAddress.PlaceholderText = "آدرس پروکسی"; _proxyAddress.Width = 125; _proxyAddress.Location = new Point(125, 58);
        ConfigureInput(_proxyPort); _proxyPort.PlaceholderText = "پورت"; _proxyPort.Width = 58; _proxyPort.Location = new Point(258, 58);
        ConfigureInput(_proxyUser); _proxyUser.PlaceholderText = "نام کاربری"; _proxyUser.Width = 104; _proxyUser.Location = new Point(324, 58);
        ConfigureInput(_proxyPassword); _proxyPassword.PlaceholderText = "رمز عبور"; _proxyPassword.Width = 104; _proxyPassword.UseSystemPasswordChar = true; _proxyPassword.Location = new Point(436, 58);
        var timeoutLabel = UiTheme.Label("Timeout (ثانیه)", 8, color: UiTheme.Muted); timeoutLabel.Location = new Point(22, 108);
        _timeoutSeconds.Minimum = 5; _timeoutSeconds.Maximum = 600; _timeoutSeconds.Value = 45; _timeoutSeconds.Width = 95; _timeoutSeconds.Location = new Point(22, 132);
        var retryLabel = UiTheme.Label("تلاش مجدد", 8, color: UiTheme.Muted); retryLabel.Location = new Point(130, 108);
        _retryCount.Minimum = 0; _retryCount.Maximum = 10; _retryCount.Value = 2; _retryCount.Width = 75; _retryCount.Location = new Point(130, 132);
        var delayLabel = UiTheme.Label("تأخیر درخواست (ms)", 8, color: UiTheme.Muted); delayLabel.Location = new Point(220, 108);
        _requestDelay.Minimum = 0; _requestDelay.Maximum = 60000; _requestDelay.Value = 150; _requestDelay.Width = 120; _requestDelay.Location = new Point(220, 132);
        var proxyHint = UiTheme.Label("HTTP / HTTPS / SOCKS5 — رمز عبور با Windows DPAPI ذخیره می‌شود.", 8, color: UiTheme.Muted); proxyHint.Location = new Point(22, 174);
        var profiles = UiTheme.Button("پروفایل‌ها", Color.FromArgb(232, 237, 245)); profiles.Tag = "secondary-button"; profiles.ForeColor = UiTheme.Text; profiles.Width = 112; profiles.Height = 30; profiles.Location = new Point(420, 174); profiles.Click += (_, _) => ShowProxyProfiles();
        var concurrencyLabel = UiTheme.Label("دانلود هم‌زمان", 8, color: UiTheme.Muted); concurrencyLabel.Location = new Point(22, 205);
        _concurrency.Minimum = 1; _concurrency.Maximum = 16; _concurrency.Width = 70; _concurrency.Location = new Point(22, 220);
        var diskLabel = UiTheme.Label("حداقل فضای آزاد (MB)", 8, color: UiTheme.Muted); diskLabel.Location = new Point(112, 205);
        _minFreeDisk.Minimum = 0; _minFreeDisk.Maximum = 1024 * 1024; _minFreeDisk.Width = 120; _minFreeDisk.Location = new Point(112, 220);
        var speedLabel = UiTheme.Label("سقف سرعت (KB/s، صفر=نامحدود)", 8, color: UiTheme.Muted); speedLabel.Location = new Point(250, 205);
        _speedLimit.Minimum = 0; _speedLimit.Maximum = 1024 * 1024; _speedLimit.Width = 145; _speedLimit.Location = new Point(250, 220);
        var domainLabel = UiTheme.Label("اتصال هر دامنه", 8, color: UiTheme.Muted); domainLabel.Location = new Point(410, 205);
        _domainConnections.Minimum = 1; _domainConnections.Maximum = 32; _domainConnections.Width = 80; _domainConnections.Location = new Point(410, 220);
        proxy.Controls.AddRange([proxyTitle, _proxyEnabled, _proxyType, _proxyAddress, _proxyPort, _proxyUser, _proxyPassword, timeoutLabel, _timeoutSeconds, retryLabel, _retryCount, delayLabel, _requestDelay, proxyHint, profiles, concurrencyLabel, _concurrency, diskLabel, _minFreeDisk, speedLabel, _speedLimit, domainLabel, _domainConnections]);

        var output = UiTheme.Card(); output.Dock = DockStyle.Top; output.Height = 120;
        var outputTitle = UiTheme.Label("محل ذخیره", 12, FontStyle.Bold); outputTitle.Location = new Point(22, 16);
        ConfigureInput(_output); _output.PlaceholderText = "مسیر پوشه خروجی"; _output.Multiline = false; _output.AutoSize = false; _output.Height = 30; _output.Location = new Point(22, 52); _output.Width = 350;
        var browse = UiTheme.Button("انتخاب مسیر", Color.FromArgb(238, 243, 250)); browse.Tag = "secondary-button"; browse.ForeColor = UiTheme.Text; browse.Width = 115; browse.Height = 30; browse.Location = new Point(387, 49); browse.Click += BrowseOutput;
        output.Controls.AddRange([outputTitle, _output, browse]);

        var logCard = UiTheme.Card(); logCard.Dock = DockStyle.Fill;
        var logTitle = UiTheme.Label("گزارش فعالیت‌ها", 12, FontStyle.Bold); logTitle.Location = new Point(22, 16);
        var clear = UiTheme.Button("پاک کردن", Color.Transparent); clear.Tag = "secondary-button"; clear.ForeColor = UiTheme.Muted; clear.Width = 95; clear.Height = 30; clear.Location = new Point(670, 10); clear.Click += (_, _) => _log.Clear();
        _log.ReadOnly = true; _log.BackColor = Color.White; _log.BorderStyle = BorderStyle.None; _log.Font = new Font("Consolas", 9); _log.Location = new Point(22, 52); _log.Size = new Size(700, 100); _log.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left; _log.ScrollBars = RichTextBoxScrollBars.Vertical; _log.Tag = "log";
        logCard.Controls.AddRange([logTitle, clear, _log]);
        leftColumn.Controls.Add(logCard); leftColumn.Controls.Add(output); leftColumn.Controls.Add(proxy); leftColumn.Controls.Add(settings);

        LoadSavedSettings();
        UpdateProxyControls();
        Controls.Add(root);
    }

    private Panel BuildSidebar()
    {
        var sidebar = new Panel { Dock = DockStyle.Left, Width = 270, BackColor = UiTheme.Primary, Padding = new Padding(24, 24, 24, 18), Tag = "sidebar" };
        var brand = new Panel { Dock = DockStyle.Top, Height = 132, BackColor = Color.Transparent };
        var cloud = UiTheme.Label("☁", 42, FontStyle.Bold, Color.White); cloud.AutoSize = false; cloud.Location = new Point(8, 4); cloud.Size = new Size(68, 58); cloud.RightToLeft = RightToLeft.No; cloud.TextAlign = ContentAlignment.MiddleCenter;
        var name = UiTheme.Label("CopyWeb", 20, FontStyle.Bold, Color.White); name.AutoSize = true; name.Location = new Point(72, 8); name.RightToLeft = RightToLeft.No; name.TextAlign = ContentAlignment.MiddleLeft;
        var tagline = UiTheme.Label("دانلود نسخه آفلاین سایت", 9, color: Color.FromArgb(220, 235, 255)); tagline.AutoSize = false; tagline.Location = new Point(78, 48); tagline.Size = new Size(150, 24); tagline.RightToLeft = RightToLeft.No;
        brand.Controls.AddRange([cloud, name, tagline]);
        var nav = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 300, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(0, 12, 0, 0) };
        var home = NavButton("⌂   شروع", true); home.Click += (_, _) => FocusHome();
        var projects = NavButton("🌐   پروژه‌ها", false); projects.Click += (_, _) => ShowProjects();
        var settings = NavButton("⚙   تنظیمات", false); settings.Click += (_, _) => ShowSettings();
        var reports = NavButton("📊   گزارش‌ها", false); reports.Click += (_, _) => ShowReports();
        var about = NavButton("ⓘ   درباره برنامه", false); about.Click += (_, _) => ShowAbout();
        nav.Controls.AddRange([home, projects, settings, reports, about]);
        var version = UiTheme.Label("نسخه 1.3.0", 9, color: Color.FromArgb(215, 232, 255)); version.Dock = DockStyle.Bottom; version.TextAlign = ContentAlignment.MiddleCenter; version.Height = 30;
        sidebar.Controls.Add(version); sidebar.Controls.Add(nav); sidebar.Controls.Add(brand);
        return sidebar;
    }

    private static Button NavButton(string text, bool selected)
    {
        var button = UiTheme.Button(text, selected ? Color.FromArgb(112, 130, 158) : Color.FromArgb(101, 119, 147));
        button.Tag = "sidebar-button";
        button.Width = 222; button.Height = 48; button.Margin = new Padding(0, 0, 0, 10); button.TextAlign = ContentAlignment.MiddleLeft; button.Padding = new Padding(18, 0, 12, 0); button.Font = new Font(UiTheme.NormalFont, FontStyle.Bold);
        return button;
    }

    private static Icon? LoadApplicationIcon()
    {
        try
        {
            var file = Path.Combine(AppContext.BaseDirectory, "CopyWeb.ico");
            if (File.Exists(file)) return new Icon(file);
            var executable = Environment.ProcessPath ?? Application.ExecutablePath;
            return File.Exists(executable) ? Icon.ExtractAssociatedIcon(executable) : null;
        }
        catch { return null; }
    }

    private static void ConfigureInput(TextBox input)
    {
        input.Tag = "input";
        input.BackColor = Color.White;
        input.ForeColor = UiTheme.Text;
        input.BorderStyle = BorderStyle.FixedSingle;
        input.RightToLeft = RightToLeft.No;
        input.TextAlign = HorizontalAlignment.Left;
        input.Font = UiTheme.NormalFont;
        input.Padding = new Padding(6, 2, 6, 2);
    }

    private void UpdateProxyControls()
    {
        var enabled = _proxyEnabled.Checked;
        _proxyType.Enabled = enabled;
        _proxyAddress.Enabled = enabled;
        _proxyPort.Enabled = enabled;
        _proxyUser.Enabled = enabled;
        _proxyPassword.Enabled = enabled;
    }

    private void LoadSavedSettings()
    {
        var settings = AppSettingsStore.Load();
        _proxyEnabled.Checked = settings.ProxyEnabled;
        _proxyType.SelectedItem = settings.ProxyKind switch { ProxyKind.Https => "HTTPS", ProxyKind.Socks5 => "SOCKS5", _ => "HTTP" };
        _proxyAddress.Text = settings.ProxyAddress;
        _proxyPort.Text = settings.ProxyPort.ToString();
        _proxyUser.Text = SecureStorage.Unprotect(settings.EncryptedProxyUsername) ?? string.Empty;
        _proxyPassword.Text = SecureStorage.Unprotect(settings.EncryptedProxyPassword) ?? string.Empty;
        _timeoutSeconds.Value = Math.Clamp(settings.RequestTimeoutSeconds, (int)_timeoutSeconds.Minimum, (int)_timeoutSeconds.Maximum);
        _retryCount.Value = Math.Clamp(settings.RetryCount, (int)_retryCount.Minimum, (int)_retryCount.Maximum);
        _requestDelay.Value = Math.Clamp(settings.DelayMilliseconds, (int)_requestDelay.Minimum, (int)_requestDelay.Maximum);
        _concurrency.Value = Math.Clamp(settings.MaxConcurrentDownloads, (int)_concurrency.Minimum, (int)_concurrency.Maximum);
        _minFreeDisk.Value = Math.Clamp(settings.MinimumFreeDiskSpaceMb, (long)_minFreeDisk.Minimum, (long)_minFreeDisk.Maximum);
        _speedLimit.Value = Math.Clamp(settings.MaxDownloadSpeedKbps, (int)_speedLimit.Minimum, (int)_speedLimit.Maximum);
        _domainConnections.Value = Math.Clamp(settings.MaxConnectionsPerDomain, (int)_domainConnections.Minimum, (int)_domainConnections.Maximum);
        _sitemaps.Checked = settings.ReadSitemaps;
        _canonical.Checked = settings.FollowCanonicalLinks;
    }

    private ProxyKind SelectedProxyKind() => _proxyType.SelectedIndex switch
    {
        1 => ProxyKind.Https,
        2 => ProxyKind.Socks5,
        _ => ProxyKind.Http
    };

    private ProxySnapshot CurrentProxySnapshot() => new()
    {
        Enabled = _proxyEnabled.Checked,
        Kind = SelectedProxyKind(),
        Address = _proxyAddress.Text.Trim(),
        Port = int.TryParse(_proxyPort.Text.Trim(), out var port) ? port : 8080,
        EncryptedUsername = SecureStorage.Protect(_proxyUser.Text.Trim()),
        EncryptedPassword = SecureStorage.Protect(_proxyPassword.Text)
    };

    private void RestoreProxySnapshot(ProxySnapshot? snapshot)
    {
        if (snapshot is null) return;
        _proxyEnabled.Checked = snapshot.Enabled;
        _proxyType.SelectedItem = snapshot.Kind switch { ProxyKind.Https => "HTTPS", ProxyKind.Socks5 => "SOCKS5", _ => "HTTP" };
        _proxyAddress.Text = snapshot.Address;
        _proxyPort.Text = snapshot.Port.ToString();
        _proxyUser.Text = SecureStorage.Unprotect(snapshot.EncryptedUsername) ?? string.Empty;
        _proxyPassword.Text = SecureStorage.Unprotect(snapshot.EncryptedPassword) ?? string.Empty;
    }

    private void FocusHome()
    {
        Activate();
        _url.Focus();
    }

    private void ShowProjects()
    {
        using var form = new ProjectsForm();
        if (form.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(form.SelectedProjectFile))
            _ = ResumeFromFileAsync(form.SelectedProjectFile);
    }

    private void ShowSettings()
    {
        using var form = new SettingsForm(AppSettingsStore.Load());
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            ApplyThemeToControls();
            _apiServer?.Dispose(); _apiServer = null;
            StartLocalApi();
        }
    }

    private void ShowReports()
    {
        using var form = new ReportsForm();
        form.ShowDialog(this);
    }

    private void ShowAbout()
    {
        using var form = new AboutForm();
        form.ShowDialog(this);
    }

    private void ShowTutorial()
    {
        using var form = new TutorialForm();
        form.ShowDialog(this);
    }

    private void ApplyThemeToControls()
    {
        BackColor = UiTheme.Background;
        ApplyThemeToControl(this, false);
        Localization.Apply(this, AppSettingsStore.Load().Language);
    }

    private void StartLocalApi()
    {
        var settings = AppSettingsStore.Load();
        if (!settings.EnableLocalApi) return;
        try
        {
            _apiServer = new LocalApiServer(settings.LocalApiPort,
                () => new { running = _cts is not null, url = _url.Text, status = _status.Text, currentFile = _currentFile.Text, version = UpdateChecker.CurrentVersion },
                () => _cts?.Cancel());
            _apiServer.Start();
            AppendLog($"API محلی روی http://127.0.0.1:{settings.LocalApiPort} فعال شد.");
        }
        catch (Exception ex) { AppendLog($"فعال‌سازی API محلی ناموفق بود: {ex.Message}", ActivitySeverity.Warning); }
    }

    private static void ApplyThemeToControl(Control control, bool inSidebar)
    {
        var sidebar = inSidebar || control.Tag is "sidebar";
        switch (control.Tag)
        {
            case "sidebar":
                control.BackColor = UiTheme.Primary;
                break;
            case "sidebar-button":
                control.BackColor = ControlPaint.Light(UiTheme.Primary, 0.18F);
                control.ForeColor = Color.White;
                break;
            case "background":
                control.BackColor = UiTheme.Background;
                break;
            case "surface":
                control.BackColor = UiTheme.Surface;
                control.ForeColor = UiTheme.Text;
                break;
            case "border":
                control.BackColor = UiTheme.Border;
                break;
            case "input":
                control.BackColor = UiTheme.Surface;
                control.ForeColor = UiTheme.Text;
                break;
            case "log":
                control.BackColor = UiTheme.Surface;
                control.ForeColor = UiTheme.Text;
                break;
            case "primary-button":
                control.BackColor = UiTheme.Primary;
                control.ForeColor = Color.White;
                break;
            case "accent-button":
                control.BackColor = UiTheme.Accent;
                control.ForeColor = Color.White;
                break;
            case "danger-button":
                control.BackColor = UiTheme.Danger;
                control.ForeColor = Color.White;
                break;
            case "secondary-button":
                control.BackColor = UiTheme.Surface;
                control.ForeColor = UiTheme.Text;
                break;
            case "muted":
                control.ForeColor = sidebar ? Color.FromArgb(220, 235, 255) : UiTheme.Muted;
                break;
            case "text":
                control.ForeColor = sidebar ? Color.White : UiTheme.Text;
                break;
            case "on-primary":
                control.ForeColor = Color.White;
                break;
        }

        foreach (Control child in control.Controls)
            ApplyThemeToControl(child, sidebar);
    }

    private void BeginLog(string path, bool append)
    {
        _activeLogPath = path;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        if (!append && AppSettingsStore.Load().SaveDetailedLogs)
            File.WriteAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] شروع عملیات{Environment.NewLine}", Encoding.UTF8);
    }

    private void BrowseOutput(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog { Description = "پوشه ذخیره نسخه آفلاین را انتخاب کنید" };
        if (dialog.ShowDialog(this) == DialogResult.OK) _output.Text = dialog.SelectedPath;
    }

    private void PasteUrlFromClipboard()
    {
        if (!Clipboard.ContainsText())
        {
            MessageBox.Show(this, "متن معتبری در Clipboard وجود ندارد.", "چسباندن آدرس", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        _url.Text = Clipboard.GetText().Trim();
        _url.Focus();
        _url.SelectionStart = _url.TextLength;
        _url.SelectionLength = 0;
    }

    private void HandleDrop(IDataObject? data)
    {
        if (data is null) return;
        if (data.GetDataPresent(DataFormats.Text)) { _url.Text = (data.GetData(DataFormats.Text)?.ToString() ?? string.Empty).Trim(); return; }
        if (!data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = data.GetData(DataFormats.FileDrop) as string[];
        var file = files?.FirstOrDefault(File.Exists);
        if (file is null) return;
        var firstLine = File.ReadLines(file).FirstOrDefault()?.Trim();
        if (!string.IsNullOrWhiteSpace(firstLine)) _url.Text = firstLine;
    }

    private void ShowProxyProfiles()
    {
        using var form = new ProxyProfilesForm(AppSettingsStore.Load().ProxyProfiles);
        if (form.ShowDialog(this) != DialogResult.OK || form.SelectedProfile is not { } profile) return;
        _proxyEnabled.Checked = true;
        _proxyType.SelectedIndex = profile.Kind switch { ProxyKind.Https => 1, ProxyKind.Socks5 => 2, _ => 0 };
        _proxyAddress.Text = profile.Address;
        _proxyPort.Text = profile.Port.ToString();
        _proxyUser.Text = SecureStorage.Unprotect(profile.EncryptedUsername) ?? string.Empty;
        _proxyPassword.Text = SecureStorage.Unprotect(profile.EncryptedPassword) ?? string.Empty;
        UpdateProxyControls();
    }

    private async void StartClick(object? sender, EventArgs e)
    {
        if (!Uri.TryCreate(_url.Text.Trim(), UriKind.Absolute, out var root) || root.Scheme is not ("http" or "https"))
        { MessageBox.Show(this, "لطفاً یک آدرس معتبر HTTP یا HTTPS وارد کنید.", "آدرس نامعتبر", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        if (string.IsNullOrWhiteSpace(_output.Text)) _output.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CopyWeb", root.Host);
        Directory.CreateDirectory(_output.Text); BeginLog(Path.Combine(_output.Text, "activity.log"), append: false); PrepareOperation();
        _ = CaptureAutomaticScreenshotAsync(root, Path.Combine(_output.Text, "screenshots", "before.png"));
        try
        {
            using var session = CreateSession(); var crawler = new SiteCrawler(session);
            var crawlProgress = new Progress<CrawlProgress>(p => { _status.Text = p.Message; _stats.Text = $"{p.Processed} صفحه بررسی | {p.Discovered} لینک پیدا شد"; _counts.Text = $"صفحات پیدا‌شده: {p.Discovered}"; AppendLog(p.Message); });
            var links = await crawler.CrawlAsync(root, BuildCrawlOptions(), ShowCaptchaAsync, crawlProgress, _cts!.Token, checkpoint: found => ProjectStorage.SaveAsync(Path.Combine(_output.Text, "links.json"), root, found, CurrentProxySnapshot()), renderHandler: RenderPageAsync);
            using var linksForm = new LinksForm(root, links); if (linksForm.ShowDialog(this) != DialogResult.OK) return;
            await ProjectStorage.SaveAsync(Path.Combine(_output.Text, "links.json"), root, linksForm.Items, CurrentProxySnapshot(), _cts.Token);
            await DownloadItemsAsync(session, root, linksForm.Items, _output.Text); CompleteOperation();
        }
        catch (OperationCanceledException) { CancelledOperation(); }
        catch (Exception ex) { FailedOperation(ex); }
        finally { FinishOperation(); }
    }

    private async void ResumeClick(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog { Filter = "فایل پروژه CopyWeb|links.json;*.json", FileName = "links.json" }; if (dialog.ShowDialog(this) != DialogResult.OK) return;
        await ResumeFromFileAsync(dialog.FileName);
    }

    private async Task ResumeFromFileAsync(string fileName)
    {
        try
        {
            var project = await ProjectStorage.LoadAsync(fileName); if (!Uri.TryCreate(project.RootUrl, UriKind.Absolute, out var root)) throw new InvalidDataException("آدرس ریشه پروژه معتبر نیست.");
            _url.Text = root.AbsoluteUri; _output.Text = Path.GetDirectoryName(fileName) ?? _output.Text; RestoreProxySnapshot(project.Proxy); BeginLog(Path.Combine(_output.Text, "activity.log"), append: true); PrepareOperation(); _ = CaptureAutomaticScreenshotAsync(root, Path.Combine(_output.Text, "screenshots", "before.png")); using var session = CreateSession();
            var crawlCheckpoint = project.Links.Any(x => x.State is LinkState.Pending or LinkState.Failed or LinkState.Downloading) && !project.Links.Any(x => x.State == LinkState.Downloaded);
            if (crawlCheckpoint)
            {
                var crawler = new SiteCrawler(session); var crawlProgress = new Progress<CrawlProgress>(p => { _status.Text = p.Message; _stats.Text = $"{p.Processed} صفحه بررسی | {p.Discovered} لینک پیدا شد"; _counts.Text = $"صفحات پیدا‌شده: {p.Discovered}"; AppendLog(p.Message); });
                project.Links = await crawler.CrawlAsync(root, BuildCrawlOptions(), ShowCaptchaAsync, crawlProgress, _cts!.Token, project.Links, found => ProjectStorage.SaveAsync(Path.Combine(_output.Text, "links.json"), root, found, CurrentProxySnapshot()), RenderPageAsync);
            }
            using var linksForm = new LinksForm(root, project.Links); if (linksForm.ShowDialog(this) != DialogResult.OK) return;
            await ProjectStorage.SaveAsync(Path.Combine(_output.Text, "links.json"), root, linksForm.Items, CurrentProxySnapshot(), _cts!.Token); await DownloadItemsAsync(session, root, linksForm.Items, _output.Text); CompleteOperation();
        }
        catch (OperationCanceledException) { CancelledOperation(); }
        catch (Exception ex) { FailedOperation(ex); }
        finally { FinishOperation(); }
    }

    private async Task DownloadItemsAsync(SiteSession session, Uri root, IReadOnlyCollection<DownloadItem> links, string output, bool reuseMonitor = false)
    {
        var downloader = new SiteDownloader(session);
        if (!reuseMonitor)
        {
            _downloadMonitor?.Close();
            _downloadMonitor?.Dispose();
            _downloadMonitor = new DownloadMonitorForm(links, output, () => _cts?.Cancel(), downloader.CancelItem, failed => RetryFailedItemsAsync(root, output, failed));
            _downloadMonitor.Show(this);
        }
        else _downloadMonitor?.SetCancelItem(downloader.CancelItem);
        var downloadProgress = new Progress<DownloadProgress>(p =>
        {
            _status.Text = p.Message;
            _currentFile.Text = $"فایل فعلی ({p.CurrentPercent}%): {p.CurrentUrl ?? "-"}";
            _fileProgress.Value = Math.Clamp(p.CurrentPercent, 0, 100);
            var totalPercent = p.Total == 0 ? 100 : Math.Clamp(p.Completed * 100 / p.Total, 0, 100);
            _progress.Value = totalPercent;
            _stats.Text = $"پیشرفت کل صفحات: {p.Completed} از {p.Total} ({totalPercent}%)";
            var successful = Math.Max(0, p.Completed - p.Failed);
            _counts.Text = $"کل {p.Total} | موفق {successful} | خطا {p.Failed} | فعال {p.ActiveDownloads} | صف {p.Queued} | آزاد {FormatBytes(p.FreeDiskBytes)}";
            UpdateSpeedAndEta(p.Completed, p.Total, p.TotalBytesDownloaded);
            var severity = p.Message.Contains("ناموفق", StringComparison.OrdinalIgnoreCase) ? ActivitySeverity.Warning : ActivitySeverity.Info;
            AppendLog($"{p.CurrentPercent}% | {p.Message}", severity, p.CurrentUrl);
            _downloadMonitor?.UpdateProgress(p);
        });
        try
        {
            await downloader.DownloadAsync(root, links, output, downloadProgress, _cts!.Token, (int)_requestDelay.Value, (int)_concurrency.Value, (long)_minFreeDisk.Value, (int)_speedLimit.Value, (int)_domainConnections.Value);
        }
        finally
        {
            _downloadMonitor?.MarkCompleted();
        }
    }

    private async Task RetryFailedItemsAsync(Uri root, string output, IReadOnlyList<DownloadItem> failed)
    {
        if (_cts is not null)
        {
            MessageBox.Show(this, "ابتدا عملیات فعلی تمام شود و سپس تلاش مجدد را بزنید.", "تلاش مجدد", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        _cts = new CancellationTokenSource();
        _operationClock = Stopwatch.StartNew();
        _start.Enabled = false; _resume.Enabled = false; _stop.Enabled = true;
        try
        {
            using var session = CreateSession();
            await DownloadItemsAsync(session, root, failed, output, reuseMonitor: true);
        }
        catch (OperationCanceledException) { CancelledOperation(); }
        catch (Exception ex) { FailedOperation(ex); }
        finally { FinishOperation(); }
    }

    private CrawlOptions BuildCrawlOptions() => new()
    {
        MaxDepth = (int)_depth.Value,
        MaxPages = (int)_maxPages.Value,
        IncludeSubdomains = _subdomains.Checked,
        RespectRobotsTxt = _robots.Checked,
        ReadSitemaps = _sitemaps.Checked,
        FollowCanonicalLinks = _canonical.Checked,
        RenderJavaScript = AppSettingsStore.Load().RenderJavaScript,
        DelayMilliseconds = (int)_requestDelay.Value
    };

    private SiteSession CreateSession()
    {
        var port = int.TryParse(_proxyPort.Text.Trim(), out var parsedPort) ? parsedPort : 8080;
        var address = _proxyAddress.Text.Trim();
        if (_proxyEnabled.Checked && (string.IsNullOrWhiteSpace(address) || port is < 1 or > 65535)) throw new InvalidOperationException("آدرس یا پورت پروکسی معتبر نیست.");
        var settings = AppSettingsStore.Load();
        settings.ProxyEnabled = _proxyEnabled.Checked;
        settings.ProxyKind = SelectedProxyKind();
        settings.ProxyAddress = address;
        settings.ProxyPort = port;
        settings.EncryptedProxyUsername = SecureStorage.Protect(_proxyUser.Text.Trim());
        settings.EncryptedProxyPassword = SecureStorage.Protect(_proxyPassword.Text);
        settings.RequestTimeoutSeconds = (int)_timeoutSeconds.Value;
        settings.RetryCount = (int)_retryCount.Value;
        settings.DelayMilliseconds = (int)_requestDelay.Value;
        settings.MaxConcurrentDownloads = (int)_concurrency.Value;
        settings.MinimumFreeDiskSpaceMb = (long)_minFreeDisk.Value;
        settings.MaxDownloadSpeedKbps = (int)_speedLimit.Value;
        settings.MaxConnectionsPerDomain = (int)_domainConnections.Value;
        settings.ReadSitemaps = _sitemaps.Checked;
        settings.FollowCanonicalLinks = _canonical.Checked;
        AppSettingsStore.Save(settings);
        var rotatingProxies = (_proxyEnabled.Checked ? settings.ProxyProfiles : [])
            .Where(profile => profile.Enabled && !string.IsNullOrWhiteSpace(profile.Address))
            .Select(profile => new ProxyOptions
            {
                Enabled = true,
                Kind = profile.Kind,
                Address = profile.Address,
                Port = profile.Port,
                Username = SecureStorage.Unprotect(profile.EncryptedUsername),
                Password = SecureStorage.Unprotect(profile.EncryptedPassword),
                TimeoutSeconds = (int)_timeoutSeconds.Value,
                RetryCount = (int)_retryCount.Value,
                RetryDelayMilliseconds = 750,
                MaxDownloadSpeedKbps = (int)_speedLimit.Value,
                MaxConnectionsPerDomain = (int)_domainConnections.Value,
                UserAgent = settings.UserAgent,
                Headers = settings.CustomHeaders,
                CookieHeader = settings.CustomCookies
            }).ToList();
        return new SiteSession(new ProxyOptions
        {
            Enabled = _proxyEnabled.Checked,
            Address = address,
            Port = port,
            Username = _proxyUser.Text.Trim(),
            Password = _proxyPassword.Text,
            Kind = SelectedProxyKind(),
            TimeoutSeconds = (int)_timeoutSeconds.Value,
            RetryCount = (int)_retryCount.Value,
            RetryDelayMilliseconds = 750,
            MaxDownloadSpeedKbps = (int)_speedLimit.Value,
            MaxConnectionsPerDomain = (int)_domainConnections.Value,
            UserAgent = settings.UserAgent,
            Headers = settings.CustomHeaders,
            CookieHeader = settings.CustomCookies
        }, rotatingProxies);
    }

    private void SetProxyTestColor(Color color)
    {
        _testProxy.BackColor = color;
        _testProxy.FlatAppearance.MouseOverBackColor = ControlPaint.Light(color, 0.08F);
        _testProxy.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(color, 0.08F);
    }

    private async void TestProxyClick(object? sender, EventArgs e)
    {
        if (!_proxyEnabled.Checked)
        {
            SetProxyTestColor(Color.FromArgb(210, 222, 238));
            MessageBox.Show(this, "ابتدا گزینه فعال را برای پروکسی انتخاب کنید.", "تست پروکسی", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _testProxy.Enabled = false;
        SetProxyTestColor(Color.FromArgb(210, 222, 238));
        try
        {
            using var session = CreateSession();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var response = await session.GetAsync(new Uri("https://api.ipify.org?format=json"), timeout.Token);
            response.EnsureSuccessStatusCode();
            using var directClient = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(15) };
            var directIp = await directClient.GetStringAsync("https://api.ipify.org?format=json", timeout.Token);
            var proxyIp = await response.Content.ReadAsStringAsync(timeout.Token);
            SetProxyTestColor(Color.FromArgb(137, 177, 153));
            MessageBox.Show(this, $"پروکسی با موفقیت پاسخ داد.\nIP واقعی: {directIp}\nIP پروکسی: {proxyIp}", "تست موفق", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            SetProxyTestColor(Color.FromArgb(210, 222, 238));
            MessageBox.Show(this, $"تست پروکسی ناموفق بود:\n{ex.Message}", "خطای پروکسی", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _testProxy.Enabled = true;
        }
    }

    private async void TestProxyClickLegacy(object? sender, EventArgs e)
    {
        if (!_proxyEnabled.Checked) { MessageBox.Show(this, "ابتدا گزینه فعال را برای پروکسی انتخاب کنید.", "تست پروکسی", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        try { using var session = CreateSession(); using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15)); using var response = await session.GetAsync(new Uri("https://api.ipify.org?format=json"), timeout.Token); response.EnsureSuccessStatusCode(); MessageBox.Show(this, $"پروکسی با موفقیت پاسخ داد.\n{await response.Content.ReadAsStringAsync(timeout.Token)}", "تست موفق", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        catch (Exception ex) { MessageBox.Show(this, $"تست پروکسی ناموفق بود:\n{ex.Message}", "خطای پروکسی", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task<string?> RenderPageAsync(Uri uri, CancellationToken token)
    {
        if (!AppSettingsStore.Load().RenderJavaScript) return null;
        using var browser = new BrowserSnapshotForm(uri);
        return await browser.CaptureAsync(token);
    }

    private void PrepareOperation()
    {
        _cts = new CancellationTokenSource();
        _operationClock = Stopwatch.StartNew();
        _approveCaptchaForOperation = false;
        _captchaCookies = [];
        _start.Enabled = false; _resume.Enabled = false; _stop.Enabled = true;
        _progress.Value = 0; _fileProgress.Value = 0;
        _counts.Text = "صفحات: ۰ | دانلودشده: ۰ | ناموفق: ۰"; _speed.Text = "دانلود: ۰ B/s | ارسال: ۰ B/s"; _eta.Text = "زمان باقی‌مانده: -";
        _log.Clear();
    }

    private void FinishOperation()
    {
        _operationClock?.Stop();
        _operationClock = null;
        _cts?.Dispose(); _cts = null;
        _start.Enabled = true; _resume.Enabled = true; _stop.Enabled = false;
    }

    private void CompleteOperation()
    {
        _status.Text = "دانلود با موفقیت پایان یافت";
        AppendLog("عملیات با موفقیت پایان یافت.", ActivitySeverity.Success);
        var settings = AppSettingsStore.Load();
        if (settings.EnableCompletionNotification)
            _ = NotificationService.NotifyAsync("CopyWeb", "دانلود نسخه آفلاین سایت با موفقیت پایان یافت.", settings.CompletionWebhook, settings.CompletionEmail);
        if (Uri.TryCreate(_url.Text.Trim(), UriKind.Absolute, out var root))
            _ = CaptureAutomaticScreenshotAsync(root, Path.Combine(_output.Text, "screenshots", "after.png"), local: true);
        MessageBox.Show(this, "نسخه آفلاین سایت ذخیره شد.", "پایان عملیات", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task CaptureAutomaticScreenshotAsync(Uri uri, string path, bool local = false)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (!local)
            {
                using var browser = new BrowserSnapshotForm(uri);
                await browser.CaptureScreenshotAsync(path, CancellationToken.None);
                return;
            }
            using var server = new OfflinePreviewServer(_output.Text);
            server.Start();
            using var localBrowser = new BrowserSnapshotForm(new Uri(server.BaseUri, "index.html"));
            await localBrowser.CaptureScreenshotAsync(path, CancellationToken.None);
        }
        catch (Exception ex) { AppendLog($"اسکرین‌شات خودکار انجام نشد: {ex.Message}", ActivitySeverity.Warning); }
    }

    private void CancelledOperation()
    {
        _status.Text = "عملیات متوقف شد؛ وضعیت ذخیره شد";
        AppendLog("برای ادامه، روی «ادامه پروژه» کلیک کنید.", ActivitySeverity.Warning);
    }

    private void FailedOperation(Exception ex)
    {
        _status.Text = "خطا";
        AppendLog(ex.Message, ActivitySeverity.Error, _url.Text, ex.ToString());
        CrashLogger.Write(ex, "Download operation");
        MessageBox.Show(this, ex.Message, "خطا", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void UpdateSpeedAndEta(int completed, int total, long totalBytesDownloaded)
    {
        if (_operationClock is null || _operationClock.Elapsed.TotalSeconds < 0.5)
        {
            _speed.Text = "دانلود: ۰ B/s | ارسال: ۰ B/s";
            _eta.Text = "زمان باقی‌مانده: -";
            return;
        }
        var elapsedSeconds = _operationClock.Elapsed.TotalSeconds;
        var bytesPerSecond = totalBytesDownloaded / elapsedSeconds;
        _speed.Text = $"دانلود: {FormatBytes(bytesPerSecond)}/s | ارسال: ۰ B/s";
        var perSecond = completed / elapsedSeconds;
        if (completed <= 0 || perSecond <= 0)
        {
            _eta.Text = "زمان باقی‌مانده: در حال محاسبه...";
            return;
        }
        var remaining = total - completed;
        _eta.Text = remaining <= 0 ? "زمان باقی‌مانده: پایان" : $"زمان باقی‌مانده: {TimeSpan.FromSeconds(remaining / perSecond):hh\\:mm\\:ss}";
    }

    private static string FormatBytes(double bytes)
    {
        if (bytes < 1024) return $"{bytes:0} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024:0.0} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024 * 1024):0.0} MB";
        return $"{bytes / (1024 * 1024 * 1024):0.0} GB";
    }
    private Task<IReadOnlyList<BrowserCookie>?> ShowCaptchaAsync(Uri uri, CancellationToken token)
    {
        if (_approveCaptchaForOperation)
            return Task.FromResult<IReadOnlyList<BrowserCookie>?>(_captchaCookies);

        using var form = new CaptchaForm(uri);
        if (form.ShowDialog(this) != DialogResult.OK) return Task.FromResult<IReadOnlyList<BrowserCookie>?>(null);
        _captchaCookies = form.Cookies;
        _approveCaptchaForOperation = form.ApproveAllPages;
        return Task.FromResult<IReadOnlyList<BrowserCookie>?>(_captchaCookies);
    }
    private void AppendLog(string message, ActivitySeverity severity = ActivitySeverity.Info, string? url = null, string? details = null)
    {
        if (IsDisposed) return;
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{severity}] {message}";
        _log.AppendText(line + Environment.NewLine);
        if (!AppSettingsStore.Load().SaveDetailedLogs || string.IsNullOrWhiteSpace(_activeLogPath)) return;
        ActivityLogStore.Append(_activeLogPath, new ActivityLogEntry { Severity = severity, Url = url ?? string.Empty, Message = message, Details = details });
    }
}
