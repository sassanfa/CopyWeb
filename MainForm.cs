using CopyWeb.Models;
using CopyWeb.Services;
using LinkState = CopyWeb.Models.LinkState;
using System.Diagnostics;
using System.Text;

namespace CopyWeb;

public partial class MainForm : Form
{
    internal event EventHandler? StartupReady;
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
    private readonly Button _login = UiTheme.Button("ورود به سایت", Color.FromArgb(232, 237, 245));
    private readonly Button _advancedModeButton = UiTheme.Button("پیشرفته", UiTheme.Danger);
    private readonly Button _copyWeb = UiTheme.Button("کپی وبی", Color.FromArgb(216, 231, 246));
    private readonly Button _liveArchive = UiTheme.Button("ذخیره زنده", Color.FromArgb(226, 239, 231));
    private readonly List<Control> _advancedControls = [];
    private Panel? _settingsCard;
    private Panel? _proxyCard;
    private Panel? _outputCard;
    private Panel? _dashboardPanel;
    private Panel? _editorPanel;
    private Panel? _editorGeneralPage;
    private Panel? _editorProxyPage;
    private Panel? _editorFiltersPage;
    private Panel? _editorAdvancedPage;
    private FlowLayoutPanel? _editorTabBar;
    private TextBox? _dashboardUrl;
    private TableLayoutPanel? _dashboardRecent;
    private Label? _dashboardRecentTitle;
    private Label? _dashboardStatusValue;
    private Label? _dashboardCurrentValue;
    private Label? _dashboardCountsValue;
    private Label? _dashboardSpeedValue;
    private Label? _dashboardEtaValue;
    private Label? _dashboardPercentValue;
    private DashboardProgressBar? _dashboardProgress;
    private DashboardProgressBar? _dashboardFileProgress;
    private DashboardProgressBar? _editorProgress;
    private DashboardProgressBar? _editorFileProgress;
    private Label? _sidebarStatus;
    private Label? _sidebarProgressLabel;
    private Label? _sidebarDetailLabel;
    private DashboardProgressBar? _sidebarProgress;
    private DashboardCloudStatus? _sidebarCloudStatus;
    private int _dashboardSucceeded;
    private int _dashboardFailed;
    private int _dashboardQueued;
    private int _dashboardActive;
    private bool _advancedMode;
    private CancellationTokenSource? _cts;
    private string? _activeLogPath;
    private Stopwatch? _operationClock;
    private bool _approveCaptchaForOperation;
    private IReadOnlyList<BrowserCookie> _captchaCookies = [];
    private IReadOnlyList<BrowserCookie> _authCookies = [];
    private DownloadMonitorForm? _downloadMonitor;
    private LocalApiServer? _apiServer;

    static MainForm()
    {
        UiTheme.Apply(AppSettingsStore.Load());
    }

    public MainForm()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        DoubleBuffered = true;
        Opacity = 0;
        SuspendLayout();
        Text = "CopyWeb | دریافت نسخه آفلاین سایت";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1120, 760);
        Size = new Size(1480, 900);
        Font = UiTheme.NormalFont;
        RightToLeft = RightToLeft.Yes;
        BackColor = UiTheme.Background;
        InitializeComponent();
        Icon = LoadApplicationIcon();
        ShowIcon = true;
        BuildUi();
        UiTheme.EnableActiveCaption(this);
        Localization.Apply(this, AppSettingsStore.Load().Language);
        ResumeLayout(true);
        AllowDrop = true;
        DragEnter += (_, e) => e.Effect = (e.Data?.GetDataPresent(DataFormats.Text) == true || e.Data?.GetDataPresent(DataFormats.FileDrop) == true) ? DragDropEffects.Copy : DragDropEffects.None;
        DragDrop += (_, e) => HandleDrop(e.Data);
        FormClosed += (_, _) => _apiServer?.Dispose();
        Shown += async (_, _) =>
        {
            var layoutSuspended = false;
            try
            {
                SuspendLayout();
                layoutSuspended = true;
                NormalizeDashboardDirection();
                NormalizeEditorDirection();
                ShowDashboard();
                ResumeLayout(true);
                layoutSuspended = false;
                PerformLayout();
                await RefreshDashboardProjectsAsync();
                Update();
            }
            catch (Exception ex)
            {
                CrashLogger.Write(ex, "MainForm.Startup");
            }
            finally
            {
                if (layoutSuspended) ResumeLayout(true);
                Opacity = 1;
                ShowInTaskbar = true;
                Activate();
                BringToFront();
                StartupReady?.Invoke(this, EventArgs.Empty);
            }
        };
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
        _advancedModeButton.Tag = "action-button";
        _advancedModeButton.ForeColor = Color.White;
        _advancedModeButton.Width = 138;
        _advancedModeButton.Height = 34;
        _advancedModeButton.Dock = DockStyle.Right;
        _advancedModeButton.Margin = new Padding(0, 8, 10, 0);
        _advancedModeButton.Click += (_, _) => SetAdvancedMode(!_advancedMode);
        top.Controls.Add(topLine);
        top.Controls.Add(subheading);
        top.Controls.Add(heading);
        top.Controls.Add(globe);
        top.Controls.Add(_advancedModeButton);
        content.Controls.Add(top);
        _editorPanel = content;

        var rightColumn = new Panel { Dock = DockStyle.Right, Width = 298, Padding = new Padding(14, 0, 0, 0) };
        var leftColumn = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 14, 0) };
        content.Controls.Add(leftColumn);
        content.Controls.Add(rightColumn);

        var operations = UiTheme.Card(); operations.Dock = DockStyle.Top; operations.Height = 494;
        var operationsTitle = UiTheme.Label("عملیات", 13, FontStyle.Bold); operationsTitle.Location = new Point(22, 18);
        _start.Tag = "action-button"; _start.BackColor = UiTheme.Action; _start.Width = 242; _start.Height = 48; _start.Location = new Point(22, 58); _start.Click += StartClick;
        _resume.Tag = "accent-button"; _resume.Width = 242; _resume.Height = 48; _resume.Location = new Point(22, 116); _resume.Click += ResumeClick;
        _stop.Tag = "danger-button"; _stop.Width = 242; _stop.Height = 48; _stop.Location = new Point(22, 174); _stop.Enabled = false; _stop.Click += (_, _) => _cts?.Cancel();
        _testProxy.Tag = "secondary-button"; _testProxy.ForeColor = UiTheme.Text; _testProxy.Width = 242; _testProxy.Height = 48; _testProxy.Location = new Point(22, 232); _testProxy.Click += TestProxyClick;
        var tutorial = UiTheme.Button("آموزش", Color.FromArgb(226, 231, 239)); tutorial.Tag = "secondary-button"; tutorial.ForeColor = UiTheme.Text; tutorial.Width = 242; tutorial.Height = 48; tutorial.Location = new Point(22, 290); tutorial.Click += (_, _) => ShowTutorial();
        _copyWeb.Tag = "secondary-button"; _copyWeb.ForeColor = UiTheme.Text; _copyWeb.Width = 242; _copyWeb.Height = 48; _copyWeb.Location = new Point(22, 348); _copyWeb.Click += CopyWebClick;
        _liveArchive.Tag = "secondary-button"; _liveArchive.ForeColor = UiTheme.Text; _liveArchive.Width = 242; _liveArchive.Height = 48; _liveArchive.Location = new Point(22, 406); _liveArchive.Click += LiveArchiveClick;
        operations.Controls.AddRange([operationsTitle, _start, _resume, _stop, _testProxy, tutorial, _copyWeb, _liveArchive]);

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
        _settingsCard = settings;
        var settingsTitle = UiTheme.Label("تنظیمات دانلود", 13, FontStyle.Bold); settingsTitle.Location = new Point(22, 18);
        var urlLabel = UiTheme.Label("آدرس سایت", 10, FontStyle.Bold); urlLabel.Location = new Point(22, 52);
        ConfigureInput(_url); _url.PlaceholderText = "https://example.com"; _url.Multiline = false; _url.AutoSize = false; _url.Height = 30; _url.Location = new Point(22, 125); _url.Width = 460;
        var pasteUrl = UiTheme.Button("چسباندن", Color.FromArgb(238, 243, 250)); pasteUrl.Tag = "secondary-button"; pasteUrl.ForeColor = UiTheme.Text; pasteUrl.Width = 82; pasteUrl.Height = 30; pasteUrl.Location = new Point(490, 125); pasteUrl.Click += (_, _) => PasteUrlFromClipboard();
        _login.Tag = "secondary-button"; _login.ForeColor = UiTheme.Text; _login.Width = 110; _login.Height = 30; _login.Location = new Point(490, 166); _login.Click += LoginClick;
        var urlLine = new Panel { BackColor = UiTheme.Border, Height = 1, Width = 460, Location = new Point(22, 205), Tag = "border" };
        var maxLabel = UiTheme.Label("حداکثر صفحه", 9, color: UiTheme.Muted); maxLabel.Location = new Point(22, 225);
        _maxPages.Minimum = 1; _maxPages.Maximum = 10000; _maxPages.Value = 500; _maxPages.Width = 130; _maxPages.Location = new Point(22, 251);
        var depthLabel = UiTheme.Label("عمق لینک", 9, color: UiTheme.Muted); depthLabel.Location = new Point(172, 225);
        _depth.Minimum = 0; _depth.Maximum = 20; _depth.Value = 3; _depth.Width = 130; _depth.Location = new Point(172, 251);
        _subdomains.Text = "شامل زیردامنه‌ها"; _subdomains.Checked = true; _subdomains.AutoSize = true; _subdomains.Location = new Point(322, 256);
        _robots.Text = "رعایت robots.txt"; _robots.Checked = true; _robots.AutoSize = true; _robots.Location = new Point(440, 256);
        _sitemaps.Text = "خواندن Sitemap"; _sitemaps.Checked = true; _sitemaps.AutoSize = true; _sitemaps.Location = new Point(22, 298);
        _canonical.Text = "پیروی از Canonical"; _canonical.Checked = true; _canonical.AutoSize = true; _canonical.Location = new Point(160, 298);
        foreach (var checkBox in new[] { _subdomains, _robots, _sitemaps, _canonical }) { checkBox.ForeColor = UiTheme.Text; checkBox.BackColor = Color.Transparent; }
        settings.Controls.AddRange([settingsTitle, urlLabel, _url, pasteUrl, _login, urlLine, maxLabel, _maxPages, depthLabel, _depth, _subdomains, _robots, _sitemaps, _canonical]);

        var proxy = UiTheme.Card(); proxy.Dock = DockStyle.Top; proxy.Height = 270;
        _proxyCard = proxy;
        var proxyTitle = UiTheme.Label("احراز هویت پروکسی (اختیاری)", 12, FontStyle.Bold); proxyTitle.Location = new Point(22, 16);
        _proxyEnabled.Text = "فعال"; _proxyEnabled.AutoSize = true; _proxyEnabled.Location = new Point(235, 19); _proxyEnabled.ForeColor = UiTheme.Text; _proxyEnabled.BackColor = Color.Transparent; _proxyEnabled.CheckedChanged += (_, _) => UpdateProxyControls();
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
        _outputCard = output;
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

        // Live capture and CopyWeb mode are quick actions, not advanced settings.
        // Keep them visible for new users so the main dashboard exposes the full workflow.
        _advancedControls.AddRange([_login, maxLabel, _maxPages, depthLabel, _depth, _subdomains, _robots, _sitemaps, _canonical, proxy, output]);
        SetAdvancedMode(false);

        LoadSavedSettings();
        UpdateProxyControls();

        // The dashboard is the first screen.  The existing editor remains
        // available behind the "شروع دانلود جدید" action, so all of the
        // mature download/proxy controls keep working without duplicating
        // their event handlers.
        _editorPanel = BuildModernProjectEditor();
        _editorPanel.Visible = false;
        root.Controls.Add(_editorPanel);
        _dashboardPanel = BuildModernDashboard();
        root.Controls.Add(_dashboardPanel);
        // The legacy editor was only used to initialize the mature controls
        // that are re-parented into the modern pages above.  Keep its helper
        // controls alive, but remove the legacy visual tree completely so a
        // navigation round-trip can never reveal the old UI.
        content.Visible = false;
        root.Controls.Remove(content);
        _dashboardPanel.BringToFront();
        ApplyModernTheme(root);
        Controls.Add(root);
    }

    private Panel BuildModernProjectEditor()
    {
        var panel = new GradientPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 18, 24, 20),
            RightToLeft = RightToLeft.No,
            StartColor = Color.FromArgb(7, 11, 35),
            EndColor = Color.FromArgb(16, 23, 57)
        };

        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            RightToLeft = RightToLeft.No,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 76));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var workspace = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 12, 0), BackColor = Color.Transparent };
        var headingBar = new DashboardCard
        {
            Dock = DockStyle.Top,
            Height = 78,
            BackColor = Color.FromArgb(16, 23, 54),
            BorderColor = Color.FromArgb(38, 48, 91),
            CornerRadius = 13,
            Padding = new Padding(14, 11, 14, 11),
            Margin = Padding.Empty
        };
        var headingLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = Color.Transparent, RightToLeft = RightToLeft.No, GrowStyle = TableLayoutPanelGrowStyle.FixedSize };
        headingLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
        headingLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        headingLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 134));
        headingLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
        headingLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var headingText = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Margin = Padding.Empty };
        var title = UiTheme.Label("تنظیمات پروژه جدید", 16.5F, FontStyle.Bold, Color.White);
        title.AutoSize = false; title.Location = new Point(37, 0); title.Size = new Size(195, 32); title.TextAlign = ContentAlignment.MiddleLeft;
        var subtitle = UiTheme.Label("تنظیمات دانلود و ذخیره سایت", 8.2F, color: Color.FromArgb(160, 173, 207));
        subtitle.AutoSize = false; subtitle.Location = new Point(37, 32); subtitle.Size = new Size(195, 23); subtitle.TextAlign = ContentAlignment.MiddleLeft;
        var titleIcon = UiTheme.Label("⚙", 18, FontStyle.Bold, Color.FromArgb(143, 92, 255));
        titleIcon.AutoSize = false; titleIcon.Location = new Point(0, 4); titleIcon.Size = new Size(34, 34); titleIcon.TextAlign = ContentAlignment.MiddleCenter;
        headingText.Controls.AddRange([title, subtitle, titleIcon]);

        PrepareModernEditorInput(_url);
        _url.PlaceholderText = "https://example.com";
        var urlHost = new DashboardCard { Dock = DockStyle.Fill, Margin = new Padding(8, 5, 10, 5), Padding = new Padding(12, 8, 12, 5), BackColor = Color.FromArgb(14, 20, 49), BorderColor = Color.FromArgb(57, 70, 124), CornerRadius = 9 };
        _url.Dock = DockStyle.Fill;
        urlHost.Controls.Add(_url);
        _start.Dock = DockStyle.Fill; _start.Margin = new Padding(0, 5, 10, 5); _start.Text = "تأیید و شروع"; _start.BackColor = Color.FromArgb(105, 75, 220); _start.ForeColor = Color.White;
        var close = ModernButton("×", Color.Transparent, Color.FromArgb(207, 215, 238), 42, 40);
        close.Dock = DockStyle.Fill; close.Margin = new Padding(0, 5, 0, 5); close.Click += (_, _) => ShowDashboard();
        headingLayout.Controls.Add(headingText, 0, 0); headingLayout.Controls.Add(urlHost, 1, 0); headingLayout.Controls.Add(_start, 2, 0); headingLayout.Controls.Add(close, 3, 0);
        headingBar.Controls.Add(headingLayout);

        var tabsHost = new DashboardCard { Dock = DockStyle.Top, Height = 62, BackColor = Color.FromArgb(16, 23, 54), BorderColor = Color.FromArgb(38, 48, 91), CornerRadius = 12, Padding = new Padding(8, 8, 8, 8), Margin = new Padding(0, 10, 0, 10) };
        _editorTabBar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.Transparent, Padding = Padding.Empty, Margin = Padding.Empty };
        _editorTabBar.Controls.AddRange([
            CreateEditorTab("عمومی", "general", DashboardButtonIcon.Folder),
            CreateEditorTab("پیشرفته", "advanced", DashboardButtonIcon.Settings),
            CreateEditorTab("فیلترها", "filters", DashboardButtonIcon.Shield),
            CreateEditorTab("پروکسی", "proxy", DashboardButtonIcon.Globe)
        ]);
        void ArrangeTabs()
        {
            if (_editorTabBar is null || _editorTabBar.Controls.Count == 0) return;
            var width = Math.Max(110, (_editorTabBar.ClientSize.Width - 24) / _editorTabBar.Controls.Count);
            foreach (Control tab in _editorTabBar.Controls) tab.Width = width;
        }
        _editorTabBar.Resize += (_, _) => ArrangeTabs();
        ArrangeTabs();
        tabsHost.Controls.Add(_editorTabBar);

        var pageHost = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(0, 10, 0, 0), BackColor = Color.Transparent };
        _editorGeneralPage = BuildEditorGeneralPage();
        _editorAdvancedPage = BuildEditorAdvancedPage();
        _editorFiltersPage = BuildEditorFiltersPage();
        _editorProxyPage = BuildEditorProxyPage();
        pageHost.Controls.AddRange([_editorProxyPage, _editorFiltersPage, _editorAdvancedPage, _editorGeneralPage]);

        workspace.Controls.Add(pageHost);
        workspace.Controls.Add(tabsHost);
        workspace.Controls.Add(headingBar);
        var info = BuildEditorProjectInfo();
        shell.Controls.Add(workspace, 0, 0);
        shell.Controls.Add(info, 1, 0);
        panel.Controls.Add(shell);
        SelectEditorTab("general");
        return panel;
    }

    private DashboardButton CreateEditorTab(string text, string key, DashboardButtonIcon icon)
    {
        var tab = (DashboardButton)ModernButton(text, Color.Transparent, Color.FromArgb(190, 201, 231), 170, 40, icon);
        tab.Tag = key; tab.Margin = new Padding(0, 0, 8, 0); tab.Click += (_, _) => SelectEditorTab(key);
        return tab;
    }

    private void SelectEditorTab(string key)
    {
        if (_editorGeneralPage is null) return;
        _editorGeneralPage.Visible = key == "general";
        if (_editorAdvancedPage is not null) _editorAdvancedPage.Visible = key == "advanced";
        if (_editorFiltersPage is not null) _editorFiltersPage.Visible = key == "filters";
        if (_editorProxyPage is not null) _editorProxyPage.Visible = key == "proxy";
        if (_editorTabBar is null) return;
        foreach (var tab in _editorTabBar.Controls.OfType<DashboardButton>())
        {
            var selected = string.Equals(tab.Tag as string, key, StringComparison.OrdinalIgnoreCase);
            tab.FillStart = selected ? UiTheme.Action : UiTheme.Surface;
            tab.FillEnd = selected ? ControlPaint.Light(UiTheme.Action, 0.10F) : ControlPaint.Light(UiTheme.Surface, 0.04F);
            tab.OutlineColor = selected ? ControlPaint.Light(UiTheme.Action, 0.24F) : UiTheme.Border;
            tab.ForeColor = selected ? Color.White : Color.FromArgb(190, 201, 231);
            tab.Invalidate();
        }
    }

    private Panel BuildEditorGeneralPage()
    {
        var page = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, AutoScroll = true };
        var layout = new TableLayoutPanel { Dock = DockStyle.Top, Height = 520, ColumnCount = 2, RowCount = 2, BackColor = Color.Transparent, RightToLeft = RightToLeft.No };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 56)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 44));

        var download = EditorCard("تنظیمات دانلود", Color.FromArgb(111, 82, 255), new Padding(16), new Padding(0, 0, 6, 8));
        var downloadGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, BackColor = Color.Transparent, RightToLeft = RightToLeft.No };
        downloadGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); downloadGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        downloadGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 34)); downloadGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 33)); downloadGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 33));
        downloadGrid.Controls.Add(EditorField("عمق لینک", _depth), 0, 0); downloadGrid.Controls.Add(EditorField("حداکثر صفحات", _maxPages), 1, 0);
        downloadGrid.Controls.Add(PrepareEditorCheckBox(_subdomains), 0, 1); downloadGrid.Controls.Add(PrepareEditorCheckBox(_robots), 1, 1);
        downloadGrid.Controls.Add(PrepareEditorCheckBox(_sitemaps), 0, 2); downloadGrid.Controls.Add(PrepareEditorCheckBox(_canonical), 1, 2);
        download.Controls.Add(downloadGrid);

        var advanced = EditorCard("تنظیمات پیشرفته", Color.FromArgb(92, 121, 255), new Padding(16), new Padding(6, 0, 0, 8));
        var advancedGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, BackColor = Color.Transparent, RightToLeft = RightToLeft.No };
        advancedGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); advancedGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        advancedGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 33)); advancedGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 33)); advancedGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 34));
        advancedGrid.Controls.Add(EditorField("اتصالات هم‌زمان", _concurrency), 0, 0); advancedGrid.Controls.Add(EditorField("تایم‌اوت (ثانیه)", _timeoutSeconds), 1, 0);
        advancedGrid.Controls.Add(EditorField("تأخیر درخواست (ms)", _requestDelay), 0, 1); advancedGrid.Controls.Add(EditorField("تلاش مجدد", _retryCount), 1, 1);
        advancedGrid.Controls.Add(EditorField("سقف سرعت (KB/s)", _speedLimit), 0, 2); advancedGrid.Controls.Add(EditorField("اتصال هر دامنه", _domainConnections), 1, 2);
        advanced.Controls.Add(advancedGrid);

        var output = EditorCard("محل ذخیره", Color.FromArgb(61, 206, 159), new Padding(16), new Padding(0, 0, 6, 0));
        var outputLayout = new TableLayoutPanel { Dock = DockStyle.Top, Height = 58, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent, RightToLeft = RightToLeft.No };
        outputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); outputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        PrepareModernEditorInput(_output); _output.Dock = DockStyle.Fill; _output.Margin = new Padding(0, 8, 8, 8);
        var browse = ModernButton("انتخاب", Color.Transparent, Color.White, 108, 36, DashboardButtonIcon.Folder); browse.Dock = DockStyle.Fill; browse.Margin = new Padding(0, 8, 0, 8); browse.Click += BrowseOutput;
        outputLayout.Controls.Add(_output, 0, 0); outputLayout.Controls.Add(browse, 1, 0); output.Controls.Add(outputLayout);

        var helper = EditorCard("شروع سریع", Color.FromArgb(126, 94, 240), new Padding(16), new Padding(6, 0, 0, 0));
        var helperText = UiTheme.Label("برای بیشتر سایت‌ها همین تنظیمات کافی است. CopyWeb لینک‌ها، تصاویر، CSS، JavaScript و فونت‌ها را به‌صورت خودکار ذخیره می‌کند.", 9, color: Color.FromArgb(174, 187, 219));
        helperText.AutoSize = false; helperText.Dock = DockStyle.Fill; helperText.TextAlign = ContentAlignment.MiddleCenter;
        helper.Controls.Add(helperText);

        layout.Controls.Add(download, 0, 0); layout.Controls.Add(advanced, 1, 0); layout.Controls.Add(output, 0, 1); layout.Controls.Add(helper, 1, 1);
        page.Controls.Add(layout);
        return page;
    }

    private Panel BuildEditorAdvancedPage()
    {
        var page = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        var card = EditorCard("گزارش زنده پروژه", Color.FromArgb(51, 171, 255), new Padding(18), Padding.Empty);
        _log.Dock = DockStyle.Fill; _log.Margin = Padding.Empty; _log.BackColor = Color.FromArgb(11, 17, 43); _log.ForeColor = Color.FromArgb(218, 225, 242); _log.BorderStyle = BorderStyle.None;
        card.Controls.Add(_log);
        var hint = UiTheme.Label("وضعیت درخواست‌ها، فایل فعلی، خطاها و تلاش‌های مجدد در این بخش نمایش داده می‌شود.", 9, color: Color.FromArgb(168, 181, 214));
        hint.AutoSize = false; hint.Dock = DockStyle.Bottom; hint.Height = 38; hint.TextAlign = ContentAlignment.MiddleRight;
        card.Controls.Add(hint);
        page.Controls.Add(card);
        return page;
    }

    private Panel BuildEditorFiltersPage()
    {
        var page = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        var card = EditorCard("ورود و فیلترهای دسترسی", Color.FromArgb(251, 191, 36), new Padding(20), Padding.Empty);
        var description = UiTheme.Label("اگر سایت به حساب کاربری نیاز دارد، ابتدا وارد شوید. نشست، Cookie و Headerهای مجاز در همان پروژه استفاده می‌شوند. انتخاب دقیق لینک‌ها نیز بعد از بررسی سایت در پنجره مدیریت لینک‌ها انجام می‌شود.", 10, color: Color.FromArgb(190, 201, 229));
        description.AutoSize = false; description.Dock = DockStyle.Top; description.Height = 90; description.TextAlign = ContentAlignment.MiddleRight;
        _login.Visible = false;
        var loginAction = ModernButton("ورود به حساب کاربری", UiTheme.Action, Color.White, 260, 42, DashboardButtonIcon.Globe);
        loginAction.Dock = DockStyle.Top; loginAction.Height = 42; loginAction.Margin = new Padding(0, 12, 0, 0); loginAction.Tag = "modern-primary";
        loginAction.Click += LoginClick;
        card.Controls.Add(loginAction); card.Controls.Add(description); page.Controls.Add(card);
        return page;
    }

    private Panel BuildEditorProxyPage()
    {
        var page = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        var card = EditorCard("پروکسی پروژه", Color.FromArgb(96, 165, 250), new Padding(18), Padding.Empty);
        var proxyEnabled = PrepareEditorCheckBox(_proxyEnabled); proxyEnabled.Dock = DockStyle.Top; proxyEnabled.Height = 34;
        var grid = new TableLayoutPanel { Dock = DockStyle.Top, Height = 178, ColumnCount = 3, RowCount = 2, BackColor = Color.Transparent, RightToLeft = RightToLeft.No };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        grid.Controls.Add(EditorField("نوع پروکسی", _proxyType), 0, 0); grid.Controls.Add(EditorField("آدرس پروکسی", _proxyAddress), 1, 0); grid.Controls.Add(EditorField("پورت", _proxyPort), 2, 0);
        grid.Controls.Add(EditorField("نام کاربری", _proxyUser), 0, 1); grid.Controls.Add(EditorField("رمز عبور", _proxyPassword), 1, 1); grid.Controls.Add(EditorField("حداقل فضای آزاد (MB)", _minFreeDisk), 2, 1);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 54, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(0, 8, 0, 0) };
        var test = ModernButton("تست پروکسی", Color.FromArgb(42, 55, 101), Color.White, 160, 38, DashboardButtonIcon.Shield); test.Click += TestProxyClick;
        var profiles = ModernButton("پروفایل‌ها", Color.Transparent, Color.White, 150, 38, DashboardButtonIcon.Folder); profiles.Click += (_, _) => ShowProxyProfiles();
        actions.Controls.AddRange([test, profiles]);
        var hint = UiTheme.Label("HTTP / HTTPS / SOCKS5 — نام کاربری و رمز عبور با Windows DPAPI ذخیره می‌شوند.", 9, color: Color.FromArgb(164, 177, 211));
        hint.AutoSize = false; hint.Dock = DockStyle.Top; hint.Height = 38; hint.TextAlign = ContentAlignment.MiddleRight;
        card.Controls.Add(hint); card.Controls.Add(actions); card.Controls.Add(grid); card.Controls.Add(proxyEnabled); page.Controls.Add(card);
        return page;
    }

    private DashboardCard BuildEditorProjectInfo()
    {
        var card = new DashboardCard { Dock = DockStyle.Fill, BackColor = Color.FromArgb(19, 26, 58), BorderColor = Color.FromArgb(42, 52, 96), AccentColor = Color.FromArgb(65, 208, 159), CornerRadius = 13, Padding = new Padding(16), Margin = Padding.Empty };
        var infoTitle = UiTheme.Label("اطلاعات پروژه", 11.5F, FontStyle.Bold, Color.White); infoTitle.AutoSize = false; infoTitle.Dock = DockStyle.Top; infoTitle.Height = 38; infoTitle.TextAlign = ContentAlignment.MiddleRight;
        var host = UiTheme.Label("پروژه جدید", 12, FontStyle.Bold, Color.White); host.AutoSize = false; host.Dock = DockStyle.Top; host.Height = 34; host.TextAlign = ContentAlignment.MiddleRight;
        _url.TextChanged += (_, _) => host.Text = Uri.TryCreate(_url.Text.Trim(), UriKind.Absolute, out var uri) ? uri.Host : "پروژه جدید";
        var flow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 230, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(0, 6, 0, 0) };
        foreach (var label in new[] { _status, _currentFile, _stats, _counts, _speed, _eta })
        {
            label.AutoSize = false; label.Width = 220; label.Height = 31; label.TextAlign = ContentAlignment.MiddleRight; label.ForeColor = label == _stats ? Color.White : Color.FromArgb(174, 187, 219); label.BackColor = Color.Transparent;
            flow.Controls.Add(label);
        }
        var progressPanel = new Panel { Dock = DockStyle.Top, Height = 122, Padding = new Padding(0, 6, 0, 0), BackColor = Color.Transparent };
        _progressCaption.Dock = DockStyle.Top; _progressCaption.Height = 22; _progressCaption.TextAlign = ContentAlignment.MiddleRight;
        _editorProgress = NewDashboardProgressBar(); _editorProgress.Dock = DockStyle.Top; _editorProgress.Height = 10; _editorProgress.Margin = Padding.Empty;
        _fileProgressCaption.Dock = DockStyle.Top; _fileProgressCaption.Height = 27; _fileProgressCaption.Padding = new Padding(0, 7, 0, 0); _fileProgressCaption.TextAlign = ContentAlignment.MiddleRight;
        _editorFileProgress = NewDashboardProgressBar(); _editorFileProgress.Dock = DockStyle.Top; _editorFileProgress.Height = 10; _editorFileProgress.Margin = Padding.Empty;
        progressPanel.Controls.Add(_editorFileProgress); progressPanel.Controls.Add(_fileProgressCaption); progressPanel.Controls.Add(_editorProgress); progressPanel.Controls.Add(_progressCaption);
        _stop.Dock = DockStyle.Bottom; _stop.Height = 46; _stop.Text = "توقف دانلود"; _stop.Margin = Padding.Empty;
        card.Controls.Add(_stop); card.Controls.Add(progressPanel); card.Controls.Add(flow); card.Controls.Add(host); card.Controls.Add(infoTitle);
        return card;
    }

    private static DashboardCard EditorCard(string title, Color accent, Padding padding, Padding margin)
    {
        var card = new DashboardCard { Dock = DockStyle.Fill, BackColor = Color.FromArgb(19, 26, 58), BorderColor = Color.FromArgb(42, 52, 96), AccentColor = accent, CornerRadius = 13, Padding = new Padding(padding.Left, padding.Top + 40, padding.Right, padding.Bottom), Margin = margin };
        var titleLabel = UiTheme.Label(title, 11.5F, FontStyle.Bold, Color.White); titleLabel.Name = "editor-card-title"; titleLabel.AutoSize = false; titleLabel.TextAlign = ContentAlignment.MiddleRight;
        void ArrangeTitle() => titleLabel.SetBounds(padding.Left, 7, Math.Max(80, card.ClientSize.Width - padding.Horizontal), 32);
        card.Resize += (_, _) => ArrangeTitle();
        card.Controls.Add(titleLabel);
        ArrangeTitle();
        titleLabel.BringToFront();
        return card;
    }

    private static Panel EditorField(string caption, Control input)
    {
        Control visibleInput;
        if (input is NumericUpDown numeric)
            visibleInput = new DashboardNumericInput(numeric);
        else
        {
            PrepareModernEditorInput(input);
            visibleInput = input;
        }
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5, 2, 5, 4), BackColor = Color.Transparent };
        var label = UiTheme.Label(caption, 8.5F, color: Color.FromArgb(174, 187, 219)); label.AutoSize = false; label.TextAlign = ContentAlignment.MiddleRight;
        visibleInput.Dock = DockStyle.None; visibleInput.Height = 32; visibleInput.Margin = Padding.Empty;
        void Arrange()
        {
            var width = Math.Max(20, panel.ClientSize.Width - 10);
            label.SetBounds(5, 2, width, 21);
            visibleInput.SetBounds(5, 25, width, 32);
        }
        panel.Resize += (_, _) => Arrange();
        panel.Controls.Add(visibleInput); panel.Controls.Add(label);
        Arrange();
        return panel;
    }

    private static void PrepareModernEditorInput(Control input)
    {
        input.BackColor = Color.FromArgb(28, 37, 76);
        input.ForeColor = Color.FromArgb(238, 242, 255);
        input.Font = new Font("Segoe UI", 9.5F);
        input.RightToLeft = RightToLeft.No;
        if (input is TextBox textBox) { textBox.BorderStyle = BorderStyle.FixedSingle; textBox.TextAlign = HorizontalAlignment.Left; }
        if (input is NumericUpDown numeric) { numeric.BorderStyle = BorderStyle.FixedSingle; numeric.TextAlign = HorizontalAlignment.Left; }
        if (input is ComboBox combo) combo.FlatStyle = FlatStyle.Flat;
    }

    private static Control PrepareEditorCheckBox(CheckBox checkBox)
    {
        return new DashboardCheckBox(checkBox) { Dock = DockStyle.Fill, Margin = new Padding(5, 1, 5, 1) };
    }

    private Panel BuildModernDashboard()
    {
        var panel = new GradientPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 18, 24, 20),
            RightToLeft = RightToLeft.No,
            StartColor = Color.FromArgb(7, 11, 35),
            EndColor = Color.FromArgb(16, 23, 57)
        };

        // The reference layout has a full-height actions rail on the right.
        // Only the center workspace owns the dashboard heading; keeping the
        // heading outside the shared grid used to push the actions card down.
        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            RightToLeft = RightToLeft.No,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 73));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var workspace = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 12, 0),
            BackColor = Color.Transparent
        };

        var header = new Panel { Dock = DockStyle.Top, Height = 75, BackColor = Color.Transparent, RightToLeft = RightToLeft.No };
        var heading = UiTheme.Label("داشبورد", 22, FontStyle.Bold, Color.White);
        heading.AutoSize = false; heading.Location = new Point(-14, 0); heading.Size = new Size(150, 42); heading.TextAlign = ContentAlignment.MiddleLeft;
        var subtitle = UiTheme.Label("آدرس سایت مورد نظر خود را وارد کنید و تنظیمات را انتخاب نمایید.", 9.2F, color: Color.FromArgb(162, 174, 207));
        subtitle.AutoSize = false; subtitle.Location = new Point(0, 42); subtitle.Size = new Size(300, 30); subtitle.TextAlign = ContentAlignment.MiddleLeft;
        var headerMark = UiTheme.Label("⠿", 18, FontStyle.Bold, Color.FromArgb(123, 92, 255));
        headerMark.AutoSize = false; headerMark.Location = new Point(0, 7); headerMark.Size = new Size(30, 30); headerMark.TextAlign = ContentAlignment.MiddleCenter;
        var headerPulse = UiTheme.Label("○", 18, FontStyle.Bold, Color.FromArgb(151, 83, 255));
        headerPulse.AutoSize = false; headerPulse.Location = new Point(125, 7); headerPulse.Size = new Size(28, 30); headerPulse.TextAlign = ContentAlignment.MiddleCenter;
        header.Controls.AddRange([subtitle, heading, headerPulse, headerMark]);

        var main = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
            BackColor = Color.Transparent
        };
        var urlCard = new DashboardCard
        {
            Dock = DockStyle.Top,
            Height = 82,
            BackColor = Color.FromArgb(25, 33, 70),
            BorderColor = Color.FromArgb(52, 64, 112),
            CornerRadius = 12,
            Padding = new Padding(14, 15, 14, 15)
        };
        var urlLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, GrowStyle = TableLayoutPanelGrowStyle.FixedSize, BackColor = Color.Transparent, RightToLeft = RightToLeft.No };
        urlLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
        urlLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        urlLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        urlLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var globe = UiTheme.Label("◎", 27, FontStyle.Bold, Color.FromArgb(113, 92, 255));
        globe.AutoSize = false; globe.Dock = DockStyle.Fill; globe.TextAlign = ContentAlignment.MiddleCenter;
        _dashboardUrl = new TextBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackColor = Color.FromArgb(14, 20, 49),
            ForeColor = Color.FromArgb(248, 250, 252),
            Font = new Font("Segoe UI", 10.5F),
            PlaceholderText = "https://example.com",
            RightToLeft = RightToLeft.No,
            TextAlign = HorizontalAlignment.Left,
            Margin = Padding.Empty,
            Tag = "dashboard-input"
        };
        _dashboardUrl.TextChanged += (_, _) => { if (_dashboardUrl.Focused) _url.Text = _dashboardUrl.Text; };
        _dashboardUrl.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { OpenProjectEditor(false); e.SuppressKeyPress = true; } };
        var add = ModernButton("افزودن به پروژه‌ها", Color.FromArgb(108, 76, 222), Color.White, 190, 44, DashboardButtonIcon.Plus);
        add.Dock = DockStyle.Fill; add.Margin = new Padding(4, 2, 0, 2); add.Click += (_, _) => OpenProjectEditor(false);
        var urlInputHost = new DashboardCard
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(8, 3, 8, 3),
            Padding = new Padding(12, 9, 12, 6),
            BackColor = Color.FromArgb(14, 20, 49),
            BorderColor = Color.FromArgb(57, 70, 124),
            CornerRadius = 9
        };
        urlInputHost.Controls.Add(_dashboardUrl);
        urlLayout.Controls.Add(globe, 0, 0); urlLayout.Controls.Add(urlInputHost, 1, 0); urlLayout.Controls.Add(add, 2, 0);
        urlCard.Controls.Add(urlLayout);

        _dashboardRecentTitle = UiTheme.Label("پروژه‌های اخیر", 12, FontStyle.Bold, Color.White);
        _dashboardRecentTitle.AutoSize = false; _dashboardRecentTitle.Dock = DockStyle.Top; _dashboardRecentTitle.Height = 58; _dashboardRecentTitle.TextAlign = ContentAlignment.MiddleRight;
        _dashboardRecent = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, BackColor = Color.Transparent, Padding = new Padding(0, 2, 0, 0), RightToLeft = RightToLeft.No };
        PopulateRecentProjects([]);
        main.Controls.Add(_dashboardRecent); main.Controls.Add(_dashboardRecentTitle); main.Controls.Add(urlCard);

        var quick = NewDashboardCard(new Padding(14), new Padding(0, 0, 0, 12), Color.FromArgb(70, 103, 255));
        var quickTitle = UiTheme.Label("عملیات سریع", 12, FontStyle.Bold, Color.White);
        quickTitle.Dock = DockStyle.Top; quickTitle.Height = 40; quickTitle.TextAlign = ContentAlignment.MiddleRight;
        var quickFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(2, 2, 2, 0), AutoScroll = false };
        var newDownload = ModernButton("شروع دانلود جدید", Color.FromArgb(105, 75, 220), Color.White, 210, 38, DashboardButtonIcon.Download); newDownload.Click += (_, _) => OpenProjectEditor(false);
        var advanced = ModernButton("تنظیمات پیشرفته", Color.FromArgb(43, 34, 86), Color.FromArgb(211, 201, 255), 210, 38, DashboardButtonIcon.Settings); advanced.Click += (_, _) => OpenProjectEditor(true);
        var resume = ModernButton("ادامه پروژه", Color.Transparent, Color.White, 210, 38, DashboardButtonIcon.Play); resume.Click += ResumeClick;
        var stop = ModernButton("توقف و ذخیره", Color.Transparent, Color.White, 210, 38, DashboardButtonIcon.Pause); stop.Click += (_, _) => _cts?.Cancel();
        var proxy = ModernButton("تست پروکسی", Color.Transparent, Color.White, 210, 38, DashboardButtonIcon.Shield); proxy.Click += TestProxyClick;
        var tutorial = ModernButton("آموزش", Color.Transparent, Color.White, 210, 38, DashboardButtonIcon.Book); tutorial.Click += (_, _) => ShowTutorial();
        var copy = ModernButton("کپی وب", Color.Transparent, Color.White, 210, 38, DashboardButtonIcon.Code); copy.Click += CopyWebClick;
        var live = ModernButton("ذخیره زنده", Color.Transparent, Color.White, 210, 38, DashboardButtonIcon.Live); live.Click += LiveArchiveClick;
        quickFlow.Controls.AddRange([newDownload, advanced, resume, stop, proxy, tutorial, copy, live]);
        foreach (Control item in quickFlow.Controls) item.Margin = new Padding(0, 0, 0, 3);
        quickFlow.SizeChanged += (_, _) =>
        {
            var width = Math.Max(155, quickFlow.ClientSize.Width - quickFlow.Padding.Horizontal - 8);
            foreach (Control item in quickFlow.Controls) item.Width = width;
        };
        quick.Controls.Add(quickFlow); quick.Controls.Add(quickTitle);

        quick.Dock = DockStyle.Top;
        quick.Height = 410;
        quick.Margin = new Padding(0, 0, 0, 12);

        var projectInfo = NewDashboardCard(new Padding(16), Padding.Empty, Color.FromArgb(65, 208, 159));
        var projectInfoTitle = UiTheme.Label("اطلاعات پروژه", 12, FontStyle.Bold, Color.White);
        projectInfoTitle.Dock = DockStyle.Top; projectInfoTitle.Height = 40; projectInfoTitle.TextAlign = ContentAlignment.MiddleRight;
        _dashboardStatusValue = DashboardMetric("آماده شروع", Color.FromArgb(74, 222, 128));
        _dashboardCurrentValue = DashboardMetric("فایل فعلی: —", Color.FromArgb(180, 193, 224));
        _dashboardCountsValue = DashboardMetric("کل ۰  •  موفق ۰  •  خطا ۰", Color.FromArgb(203, 213, 225));
        _dashboardSpeedValue = DashboardMetric("سرعت: —", Color.FromArgb(147, 197, 253));
        _dashboardEtaValue = DashboardMetric("زمان باقی‌مانده: —", Color.FromArgb(203, 213, 225));
        var metricPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 150, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(0, 4, 0, 0) };
        metricPanel.Controls.AddRange([_dashboardStatusValue, _dashboardCurrentValue, _dashboardCountsValue, _dashboardSpeedValue, _dashboardEtaValue]);
        metricPanel.SizeChanged += (_, _) =>
        {
            var width = Math.Max(110, metricPanel.ClientSize.Width - metricPanel.Padding.Horizontal - 4);
            foreach (Control item in metricPanel.Controls) item.Width = width;
        };
        var progressPanel = new Panel { Dock = DockStyle.Top, Height = 84, BackColor = Color.Transparent, Padding = new Padding(0, 4, 0, 0) };
        _dashboardProgress = NewDashboardProgressBar(); _dashboardProgress.Dock = DockStyle.Top; _dashboardProgress.Height = 9;
        _dashboardFileProgress = NewDashboardProgressBar(); _dashboardFileProgress.Dock = DockStyle.Top; _dashboardFileProgress.Height = 9;
        var totalCaption = UiTheme.Label("پیشرفت کل پروژه", 8.5F, color: Color.FromArgb(164, 178, 212)); totalCaption.Dock = DockStyle.Top; totalCaption.Height = 24; totalCaption.TextAlign = ContentAlignment.MiddleRight;
        var fileCaption = UiTheme.Label("پیشرفت فایل جاری", 8.5F, color: Color.FromArgb(164, 178, 212)); fileCaption.Dock = DockStyle.Top; fileCaption.Height = 27; fileCaption.Padding = new Padding(0, 5, 0, 0); fileCaption.TextAlign = ContentAlignment.MiddleRight;
        progressPanel.Controls.Add(_dashboardFileProgress); progressPanel.Controls.Add(fileCaption); progressPanel.Controls.Add(_dashboardProgress); progressPanel.Controls.Add(totalCaption);
        _dashboardPercentValue = UiTheme.Label("۰٪", 16, FontStyle.Bold, Color.FromArgb(110, 231, 183));
        _dashboardPercentValue.Dock = DockStyle.Bottom; _dashboardPercentValue.Height = 36; _dashboardPercentValue.TextAlign = ContentAlignment.MiddleRight;
        projectInfo.Controls.Add(_dashboardPercentValue); projectInfo.Controls.Add(progressPanel); projectInfo.Controls.Add(metricPanel); projectInfo.Controls.Add(projectInfoTitle);

        var rightStack = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Margin = Padding.Empty };
        rightStack.Controls.Add(projectInfo); rightStack.Controls.Add(quick);
        workspace.Controls.Add(main);
        workspace.Controls.Add(header);
        shell.Controls.Add(workspace, 0, 0);
        shell.Controls.Add(rightStack, 1, 0);
        panel.Controls.Add(shell);
        return panel;
    }

    private static DashboardCard NewDashboardCard(Padding padding, Padding margin, Color accent) => new()
    {
        Dock = DockStyle.Fill,
        Padding = padding,
        Margin = margin,
        BackColor = UiTheme.Surface,
        BorderColor = UiTheme.Border,
        AccentColor = accent,
        CornerRadius = 14
    };

    private static DashboardProgressBar NewDashboardProgressBar() => new()
    {
        Value = 0,
        FillColor = Color.FromArgb(99, 102, 241),
        TrackColor = Color.FromArgb(35, 44, 82)
    };

    private static Label DashboardMetric(string text, Color color)
    {
        var label = UiTheme.Label(text, 9, FontStyle.Regular, color);
        label.AutoSize = false; label.Width = 220; label.Height = 27; label.TextAlign = ContentAlignment.MiddleRight; label.AutoEllipsis = true;
        label.RightToLeft = RightToLeft.Yes;
        return label;
    }

    private static Label LegendLabel(string text, Color color)
    {
        var label = UiTheme.Label(text, 8.5F, FontStyle.Regular, color);
        label.AutoSize = false; label.Dock = DockStyle.Fill; label.TextAlign = ContentAlignment.MiddleCenter;
        return label;
    }

    private void AddRecentRow(TableLayoutPanel host, int rowIndex, DashboardProjectEntry project)
    {
        var row = new DashboardCard { Dock = DockStyle.Fill, BackColor = Color.FromArgb(25, 33, 70), BorderColor = Color.FromArgb(43, 55, 101), CornerRadius = 10, Margin = new Padding(0, 0, 0, 6), Padding = new Padding(12, 5, 12, 5), Cursor = Cursors.Hand, Tag = project.FileName };
        var iconColor = project.Failed > 0 ? Color.FromArgb(248, 113, 113) : project.Progress >= 100 ? Color.FromArgb(52, 211, 153) : Color.FromArgb(129, 140, 248);
        var icon = new DashboardButton { Dock = DockStyle.Left, Width = 34, IconKind = DashboardButtonIcon.Folder, IconAlignment = ContentAlignment.MiddleLeft, ForeColor = iconColor, FillStart = Color.FromArgb(36, 44, 91), FillEnd = Color.FromArgb(30, 39, 82), OutlineColor = Color.FromArgb(53, 65, 116), CornerRadius = 8, TabStop = false };
        var title = UiTheme.Label(project.Host, 9.5F, FontStyle.Bold, Color.White); title.AutoSize = false; title.Dock = DockStyle.Top; title.Height = 21; title.TextAlign = ContentAlignment.MiddleLeft; title.RightToLeft = RightToLeft.No;
        var detailLabel = UiTheme.Label(project.Detail, 8, color: Color.FromArgb(158, 172, 208)); detailLabel.AutoSize = false; detailLabel.Dock = DockStyle.Fill; detailLabel.TextAlign = ContentAlignment.MiddleLeft; detailLabel.RightToLeft = RightToLeft.Yes;
        var badgeColor = project.Failed > 0 ? Color.FromArgb(248, 113, 113) : project.Progress >= 100 ? Color.FromArgb(74, 222, 128) : Color.FromArgb(251, 191, 36);
        var badgeText = project.Failed > 0 ? $"{project.Failed} خطا" : project.IsLive ? "● زنده" : project.Progress >= 100 ? "تکمیل شده" : $"{project.Progress}%";
        var badge = UiTheme.Label(badgeText, 8, FontStyle.Bold, badgeColor); badge.AutoSize = false; badge.Dock = DockStyle.Fill; badge.TextAlign = ContentAlignment.MiddleCenter;
        var badgeHost = new DashboardCard { Dock = DockStyle.Right, Width = 94, Margin = new Padding(8, 5, 8, 5), Padding = Padding.Empty, CornerRadius = 8, BackColor = Color.FromArgb(31, badgeColor.R, badgeColor.G, badgeColor.B), BorderColor = Color.FromArgb(58, badgeColor.R, badgeColor.G, badgeColor.B) };
        badgeHost.Controls.Add(badge);
        var more = new DashboardButton { Dock = DockStyle.Fill, IconKind = DashboardButtonIcon.More, IconAlignment = ContentAlignment.MiddleRight, ForeColor = Color.FromArgb(172, 184, 215), FillStart = Color.Transparent, FillEnd = Color.Transparent, OutlineColor = Color.FromArgb(55, 68, 118), OutlineWidth = 1, CornerRadius = 8, TabStop = false, AccessibleName = "عملیات پروژه" };
        var moreHost = new Panel { Dock = DockStyle.Right, Width = 44, Padding = new Padding(4, 2, 4, 2), BackColor = Color.Transparent };
        moreHost.Controls.Add(more);
        var badgeGap = new Panel { Dock = DockStyle.Right, Width = 7, BackColor = Color.Transparent };
        var projectMenu = BuildRecentProjectMenu(project);
        more.Click += (_, _) => projectMenu.Show(more, new Point(0, more.Height));
        new ToolTip().SetToolTip(more, "بازکردن منوی پروژه");
        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 0, 8, 0), BackColor = Color.Transparent };
        body.Controls.Add(detailLabel); body.Controls.Add(title);
        row.Controls.Add(body); row.Controls.Add(badgeHost); row.Controls.Add(badgeGap); row.Controls.Add(moreHost); row.Controls.Add(icon);
        AttachProjectRowClick(row, project.FileName, moreHost);
        host.Controls.Add(row, 0, rowIndex);
    }

    private ContextMenuStrip BuildRecentProjectMenu(DashboardProjectEntry project)
    {
        var menu = new ContextMenuStrip
        {
            RightToLeft = RightToLeft.Yes,
            BackColor = Color.FromArgb(24, 32, 70),
            ForeColor = Color.White,
            ShowImageMargin = false,
            Font = UiTheme.NormalFont
        };
        var open = menu.Items.Add("باز کردن پروژه");
        open.Click += async (_, _) => { await LoadProjectForEditingAsync(project.FileName); OpenProjectEditor(false); };
        var folder = menu.Items.Add("باز کردن پوشه");
        folder.Click += (_, _) =>
        {
            var path = Path.GetDirectoryName(project.FileName);
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        };
        var resume = menu.Items.Add("ادامه دانلود");
        resume.Click += async (_, _) => await ResumeFromFileAsync(project.FileName);
        menu.Items.Add(new ToolStripSeparator());
        var delete = menu.Items.Add("حذف پروژه");
        delete.ForeColor = Color.FromArgb(248, 113, 113);
        delete.Click += async (_, _) => await DeleteRecentProjectAsync(project);
        return menu;
    }

    private async Task DeleteRecentProjectAsync(DashboardProjectEntry project)
    {
        var projectFile = Path.GetFullPath(project.FileName);
        var projectFolder = Path.GetDirectoryName(projectFile);
        if (string.IsNullOrWhiteSpace(projectFolder))
        {
            MessageBox.Show(this, "مسیر پروژه معتبر نیست.", "حذف پروژه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var answer = MessageBox.Show(
            this,
            $"پروژه «{project.Host}» و تمام فایل‌های پوشه زیر حذف شوند؟\n\n{projectFolder}\n\nفایل‌ها به سطل بازیافت ویندوز منتقل می‌شوند.",
            "تأیید حذف پروژه",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2,
            MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
        if (answer != DialogResult.Yes) return;

        try
        {
            if (Directory.Exists(projectFolder))
            {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                    projectFolder,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            }
            else if (File.Exists(projectFile))
            {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                    projectFile,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            }

            ProjectStorage.Forget(projectFile);
            await RefreshDashboardProjectsAsync();
            AppendLog($"پروژه حذف شد و به سطل بازیافت منتقل گردید: {projectFolder}", ActivitySeverity.Success);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"حذف پروژه انجام نشد:\n{ex.Message}",
                "خطا در حذف پروژه",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error,
                MessageBoxDefaultButton.Button1,
                MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
        }
    }

    private void PopulateRecentProjects(IReadOnlyList<DashboardProjectEntry> projects)
    {
        if (_dashboardRecent is null) return;
        _dashboardRecent.SuspendLayout();
        _dashboardRecent.Controls.Clear();
        _dashboardRecent.RowStyles.Clear();
        var visible = projects.Take(3).ToList();
        for (var i = 0; i < visible.Count; i++)
        {
            _dashboardRecent.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            AddRecentRow(_dashboardRecent, i, visible[i]);
        }

        var footerRow = visible.Count;
        _dashboardRecent.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        var spacerRow = footerRow + 1;
        _dashboardRecent.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _dashboardRecent.RowCount = spacerRow + 1;
        var allProjects = ModernButton("مشاهده همه پروژه‌ها", Color.FromArgb(29, 38, 78), Color.FromArgb(213, 221, 240), 220, 38);
        allProjects.Dock = DockStyle.Fill; allProjects.Margin = new Padding(0, 3, 0, 0); allProjects.Click += (_, _) => ShowProjects();
        _dashboardRecent.Controls.Add(allProjects, 0, footerRow);
        _dashboardRecent.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent }, 0, spacerRow);
        _dashboardRecent.ResumeLayout(true);
        if (_dashboardRecentTitle is not null) _dashboardRecentTitle.Text = projects.Count == 0 ? "پروژه‌های اخیر" : $"پروژه‌های اخیر  ({projects.Count:N0})";
    }

    private Control BuildRecentOverview(IReadOnlyList<DashboardProjectEntry> projects)
    {
        var totalFiles = projects.Sum(x => x.Total);
        var completed = projects.Sum(x => x.Downloaded);
        var failed = projects.Sum(x => x.Failed);
        var overview = new DashboardCard { Dock = DockStyle.Fill, BackColor = Color.FromArgb(22, 30, 65), BorderColor = Color.FromArgb(39, 52, 96), CornerRadius = 11, Margin = new Padding(0, 3, 0, 7), Padding = new Padding(16) };
        var title = UiTheme.Label(projects.Count == 0 ? "اولین آرشیو را بسازید" : "نمای کلی آرشیوها", 11, FontStyle.Bold, Color.White);
        title.AutoSize = false; title.Dock = DockStyle.Top; title.Height = 28; title.TextAlign = ContentAlignment.MiddleRight;
        var description = projects.Count == 0
            ? "آدرس سایت را در کادر بالا وارد کنید. حالت ساده همه تنظیمات را خودکار انجام می‌دهد و تنظیمات پیشرفته هم همیشه در دسترس است."
            : $"{projects.Count:N0} پروژه  •  {totalFiles:N0} فایل  •  {completed:N0} دانلود موفق  •  {failed:N0} خطا\nبرای بازکردن پروژه، روی ردیف آن کلیک کنید.";
        var text = UiTheme.Label(description, 9, color: Color.FromArgb(162, 176, 210));
        text.AutoSize = false; text.Dock = DockStyle.Fill; text.TextAlign = ContentAlignment.MiddleCenter;
        var action = ModernButton(projects.Count == 0 ? "شروع دانلود ساده" : "مدیریت کامل پروژه‌ها", Color.FromArgb(37, 47, 91), Color.FromArgb(220, 226, 244), 180, 34);
        action.Dock = DockStyle.Bottom; action.Margin = Padding.Empty;
        action.Click += (_, _) => { if (projects.Count == 0) OpenProjectEditor(false); else ShowProjects(); };
        overview.Controls.Add(text); overview.Controls.Add(action); overview.Controls.Add(title);
        return overview;
    }

    private void AttachProjectRowClick(Control control, string fileName, Control? excluded = null)
    {
        if (ReferenceEquals(control, excluded)) return;
        control.Click += async (_, _) =>
        {
            await LoadProjectForEditingAsync(fileName);
            OpenProjectEditor(false);
        };
        foreach (Control child in control.Controls)
        {
            child.Cursor = Cursors.Hand;
            AttachProjectRowClick(child, fileName, excluded);
        }
    }

    private async Task RefreshDashboardProjectsAsync()
    {
        if (_dashboardRecent is null || IsDisposed) return;
        try
        {
            var projects = await Task.Run(DiscoverDashboardProjects);
            if (!IsDisposed) PopulateRecentProjects(projects);
        }
        catch (Exception ex)
        {
            if (_dashboardRecentTitle is not null) _dashboardRecentTitle.Text = "خواندن پروژه‌ها انجام نشد";
            AppendLog($"داشبورد نتوانست فهرست پروژه‌ها را بخواند: {ex.Message}", ActivitySeverity.Warning);
        }
    }

    private static List<DashboardProjectEntry> DiscoverDashboardProjects()
    {
        var files = new HashSet<string>(ProjectStorage.GetKnownProjectFiles(), StringComparer.OrdinalIgnoreCase);
        var defaultRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CopyWeb");
        if (Directory.Exists(defaultRoot))
        {
            try
            {
                var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System };
                foreach (var file in Directory.EnumerateFiles(defaultRoot, "links.json", options)) files.Add(file);
            }
            catch { }
        }

        var results = new List<DashboardProjectEntry>();
        foreach (var file in files.Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc))
        {
            try
            {
                var project = ProjectStorage.LoadAsync(file).GetAwaiter().GetResult();
                var items = project.Links.Select(x => x.State)
                    .Concat(project.Links.SelectMany(x => x.Resources).Where(x => x.IsSelected).Select(x => x.State)).ToList();
                var total = items.Count;
                var downloaded = items.Count(x => x == LinkState.Downloaded);
                var failed = items.Count(x => x == LinkState.Failed);
                var progress = total == 0 ? 0 : Math.Clamp((downloaded + failed) * 100 / total, 0, 100);
                var host = Uri.TryCreate(project.RootUrl, UriKind.Absolute, out var root) ? root.Host : project.RootUrl;
                // Startup must not recursively enumerate every archived file.
                // Stored resource sizes are enough for the recent-project card
                // and keep the splash short even when archives are very large.
                var size = project.Links
                    .SelectMany(item => item.Resources)
                    .Where(resource => resource.IsSelected)
                    .Sum(resource => Math.Max(0, resource.SizeBytes));
                var modified = File.GetLastWriteTime(file);
                var isLive = File.Exists(Path.Combine(Path.GetDirectoryName(file) ?? string.Empty, "live-capture-manifest.json"));
                var detail = $"{modified:yyyy/MM/dd HH:mm}  •  {total:N0} فایل  •  {FormatBytes(size)}{(isLive ? "  •  ذخیره زنده" : string.Empty)}";
                results.Add(new DashboardProjectEntry(host, detail, file, total, downloaded, failed, progress, isLive));
            }
            catch
            {
                // A partially written or manually edited checkpoint must not break the dashboard.
            }
        }
        return results;
    }

    private static long DashboardDirectorySize(string path)
    {
        if (!Directory.Exists(path)) return 0;
        try
        {
            var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System };
            return Directory.EnumerateFiles(path, "*", options).Sum(file =>
            {
                try { return new FileInfo(file).Length; }
                catch { return 0L; }
            });
        }
        catch { return 0; }
    }

    private sealed record DashboardProjectEntry(string Host, string Detail, string FileName, int Total, int Downloaded, int Failed, int Progress, bool IsLive);

    private static Button ModernButton(string text, Color back, Color fore, int width, int height, DashboardButtonIcon icon = DashboardButtonIcon.None)
    {
        var transparent = back == Color.Transparent;
        var primaryGradient = !transparent && back.B > 175 && back.R > 70 && back.G < 110;
        var b = new DashboardButton
        {
            Text = text,
            Width = width,
            Height = height,
            ForeColor = fore,
            Margin = new Padding(0, 0, 0, 8),
            RightToLeft = RightToLeft.Yes,
            CornerRadius = 9,
            FillStart = primaryGradient ? UiTheme.Action : transparent ? UiTheme.Surface : back,
            FillEnd = primaryGradient ? ControlPaint.Light(UiTheme.Action, 0.12F) : transparent ? ControlPaint.Light(UiTheme.Surface, 0.05F) : ControlPaint.Light(back, 0.03F),
            OutlineColor = primaryGradient ? ControlPaint.Light(UiTheme.Action, 0.24F) : transparent ? UiTheme.Border : ControlPaint.Light(back, 0.12F),
            OutlineWidth = 1,
            IconKind = icon,
            IconAlignment = ContentAlignment.MiddleRight
        };
        b.Tag = primaryGradient ? "modern-primary" : transparent ? "modern-secondary" : "modern-custom";
        return b;
    }

    private void OpenProjectEditor(bool advanced)
    {
        if (_dashboardUrl is not null && !string.IsNullOrWhiteSpace(_dashboardUrl.Text)) _url.Text = _dashboardUrl.Text.Trim();

        var host = _editorPanel?.Parent;
        host?.SuspendLayout();
        try
        {
            SetAdvancedMode(advanced);
            NormalizeEditorDirection();
            SelectEditorTab("general");
            if (_editorPanel is null) return;

            // Prepare and reveal the editor before hiding the dashboard. This
            // avoids a blank legacy layer during synthetic clicks or a slow
            // layout pass on startup.
            _editorPanel.Dock = DockStyle.Fill;
            _editorPanel.Enabled = true;
            _editorPanel.Visible = true;
            _editorPanel.BringToFront();
            if (_dashboardPanel is not null) _dashboardPanel.Visible = false;
        }
        finally
        {
            host?.ResumeLayout(true);
        }

        _editorPanel?.PerformLayout();
        _editorPanel?.Invalidate(true);
        _url.Focus();
    }

    private void ShowDashboard()
    {
        if (_editorPanel is not null) _editorPanel.Visible = false;
        if (_dashboardPanel is not null)
        {
            _dashboardPanel.Visible = true;
            _dashboardPanel.BringToFront();
        }
        if (_dashboardUrl is not null && !string.IsNullOrWhiteSpace(_url.Text)) _dashboardUrl.Text = _url.Text;
        _ = RefreshDashboardProjectsAsync();
        Activate();
    }

    private void NormalizeDashboardDirection()
    {
        if (_dashboardPanel is null) return;
        var isEnglish = AppSettingsStore.Load().Language.Equals("en", StringComparison.OrdinalIgnoreCase);
        foreach (var control in EnumerateDashboardControls(_dashboardPanel).Prepend<Control>(_dashboardPanel))
        {
            control.RightToLeft = isEnglish ? RightToLeft.No : control switch
            {
                TextBox => RightToLeft.No,
                Label or Button => RightToLeft.Yes,
                _ => RightToLeft.No
            };
        }
        if (_dashboardUrl is not null) { _dashboardUrl.RightToLeft = RightToLeft.No; _dashboardUrl.TextAlign = HorizontalAlignment.Left; }
    }

    private void NormalizeEditorDirection()
    {
        if (_editorPanel is null) return;
        var isEnglish = AppSettingsStore.Load().Language.Equals("en", StringComparison.OrdinalIgnoreCase);
        foreach (var control in EnumerateDashboardControls(_editorPanel).Prepend<Control>(_editorPanel))
        {
            control.RightToLeft = isEnglish ? RightToLeft.No : control switch
            {
                TextBox or ComboBox or NumericUpDown => RightToLeft.No,
                Label or Button or CheckBox => RightToLeft.Yes,
                _ => RightToLeft.No
            };
        }
        _url.RightToLeft = RightToLeft.No; _url.TextAlign = HorizontalAlignment.Left;
        _output.RightToLeft = RightToLeft.No; _output.TextAlign = HorizontalAlignment.Left;
    }

    private static IEnumerable<Control> EnumerateDashboardControls(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in EnumerateDashboardControls(child)) yield return descendant;
        }
    }

    private Panel BuildSidebar()
    {
        var sidebar = new GradientPanel { Dock = DockStyle.Left, Width = 238, StartColor = Color.FromArgb(13, 18, 47), EndColor = Color.FromArgb(18, 26, 63), Padding = new Padding(18, 14, 18, 16), Tag = "sidebar" };
        var brand = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Color.Transparent };
        var logo = new LogoMarkControl { Location = new Point(4, 3), Size = new Size(56, 46) };
        var name = UiTheme.Label("CopyWeb", 18, FontStyle.Bold, Color.White); name.AutoSize = true; name.Location = new Point(60, 2); name.RightToLeft = RightToLeft.No; name.TextAlign = ContentAlignment.MiddleLeft;
        var tagline = UiTheme.Label("ذخیره‌ی هوشمند وب‌سایت", 8.1F, color: Color.FromArgb(177, 190, 222)); tagline.AutoSize = false; tagline.Location = new Point(58, 36); tagline.Size = new Size(145, 22); tagline.RightToLeft = RightToLeft.Yes; tagline.TextAlign = ContentAlignment.MiddleLeft;
        brand.Controls.AddRange([logo, name, tagline]);
        var nav = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 310, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent, Padding = Padding.Empty };
        var home = NavButton("شروع", true, DashboardButtonIcon.Home); home.Click += (_, _) => FocusHome();
        var projects = NavButton("پروژه‌ها", false, DashboardButtonIcon.Globe); projects.Click += (_, _) => ShowProjects();
        var settings = NavButton("تنظیمات", false, DashboardButtonIcon.Settings); settings.Click += (_, _) => ShowSettings();
        var reports = NavButton("گزارش‌ها", false, DashboardButtonIcon.Report); reports.Click += (_, _) => ShowReports();
        var about = NavButton("درباره برنامه", false, DashboardButtonIcon.Info); about.Click += (_, _) => ShowAbout();
        nav.Controls.AddRange([home, projects, settings, reports, about]);

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 190, BackColor = Color.Transparent, Padding = new Padding(0, 0, 0, 0) };
        var version = UiTheme.Label("نسخه 1.3.6", 9, color: Color.FromArgb(167, 180, 214)); version.Dock = DockStyle.Bottom; version.TextAlign = ContentAlignment.MiddleCenter; version.Height = 30;
        var stateCard = new DashboardCard { Dock = DockStyle.Fill, BackColor = Color.FromArgb(20, 29, 67), BorderColor = Color.FromArgb(43, 56, 104), CornerRadius = 13, Padding = new Padding(13), Margin = new Padding(0, 0, 0, 8) };
        var stateTitle = UiTheme.Label("وضعیت کلی", 10, FontStyle.Bold, Color.White); stateTitle.AutoSize = false; stateTitle.Dock = DockStyle.Top; stateTitle.Height = 25; stateTitle.TextAlign = ContentAlignment.MiddleRight;
        _sidebarStatus = UiTheme.Label("آماده برای شروع", 9, FontStyle.Bold, Color.FromArgb(74, 222, 128)); _sidebarStatus.AutoSize = false; _sidebarStatus.Dock = DockStyle.Top; _sidebarStatus.Height = 24; _sidebarStatus.TextAlign = ContentAlignment.MiddleRight; _sidebarStatus.AutoEllipsis = true;
        _sidebarProgressLabel = UiTheme.Label("۰٪", 18, FontStyle.Bold, Color.White);
        _sidebarProgressLabel.AutoSize = false; _sidebarProgressLabel.Dock = DockStyle.Top; _sidebarProgressLabel.Height = 42; _sidebarProgressLabel.TextAlign = ContentAlignment.MiddleCenter;
        _sidebarProgress = NewDashboardProgressBar();
        _sidebarProgress.Dock = DockStyle.Top; _sidebarProgress.Height = 10; _sidebarProgress.Margin = Padding.Empty;
        _sidebarCloudStatus = null;
        _sidebarDetailLabel = UiTheme.Label("بدون عملیات فعال", 8.5F, color: Color.FromArgb(170, 184, 218)); _sidebarDetailLabel.AutoSize = false; _sidebarDetailLabel.Dock = DockStyle.Bottom; _sidebarDetailLabel.Height = 24; _sidebarDetailLabel.TextAlign = ContentAlignment.MiddleCenter;
        var stateBody = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(4, 0, 4, 0) };
        stateBody.Controls.Add(_sidebarDetailLabel); stateBody.Controls.Add(_sidebarProgress); stateBody.Controls.Add(_sidebarProgressLabel);
        stateCard.Controls.Add(stateBody); stateCard.Controls.Add(_sidebarStatus); stateCard.Controls.Add(stateTitle);
        bottom.Controls.Add(stateCard); bottom.Controls.Add(version);
        sidebar.Controls.Add(bottom); sidebar.Controls.Add(nav); sidebar.Controls.Add(brand);
        return sidebar;
    }

    private static Button NavButton(string text, bool selected, DashboardButtonIcon icon)
    {
        var button = new DashboardButton
        {
            Text = text,
            FillStart = selected ? Color.FromArgb(63, 70, 175) : Color.FromArgb(19, 27, 62),
            FillEnd = selected ? Color.FromArgb(89, 44, 153) : Color.FromArgb(23, 32, 72),
            OutlineColor = selected ? Color.FromArgb(117, 91, 231) : Color.FromArgb(34, 45, 86),
            OutlineWidth = 1,
            CornerRadius = 9,
            ForeColor = selected ? Color.White : Color.FromArgb(220, 226, 242),
            RightToLeft = RightToLeft.Yes,
            IconKind = icon,
            IconAlignment = ContentAlignment.MiddleLeft
        };
        button.Tag = selected ? "sidebar-selected" : "sidebar-button";
        button.Width = 202; button.Height = 46; button.Margin = new Padding(0, 0, 0, 6); button.TextAlign = ContentAlignment.MiddleLeft; button.Padding = new Padding(18, 0, 12, 0); button.Font = new Font(UiTheme.NormalFont, FontStyle.Bold);
        return button;
    }

    internal static Icon? LoadApplicationIcon()
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
        // Inputs intentionally use a light surface in both themes so a URL
        // remains readable while typing.  UiTheme.Text is white in dark mode,
        // which would otherwise produce white-on-white text here.
        input.ForeColor = Color.FromArgb(30, 41, 59);
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

    private void SetAdvancedMode(bool enabled)
    {
        _advancedMode = enabled;
        foreach (var control in _advancedControls) control.Visible = enabled;
        if (_settingsCard is not null) _settingsCard.Height = enabled ? 342 : 180;
        if (_proxyCard is not null) _proxyCard.Visible = enabled;
        if (_outputCard is not null) _outputCard.Visible = enabled;
        _advancedModeButton.Text = enabled ? "حالت ساده" : "پیشرفته";
        _start.Text = enabled ? "تأیید و شروع" : "شروع دانلود سریع";
        _advancedModeButton.Tag = "action-button";
        _advancedModeButton.BackColor = UiTheme.Action;
        _advancedModeButton.ForeColor = Color.White;
        _advancedModeButton.BringToFront();
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
        ShowDashboard();
    }

    private void ShowProjects()
    {
        using var form = new ProjectsForm();
        if (form.ShowDialog(this) != DialogResult.OK) return;
        if (!string.IsNullOrWhiteSpace(form.LoadedProjectFile))
            _ = LoadProjectForEditingAsync(form.LoadedProjectFile);
        else if (!string.IsNullOrWhiteSpace(form.SelectedProjectFile))
            _ = ResumeFromFileAsync(form.SelectedProjectFile);
    }

    private async Task LoadProjectForEditingAsync(string fileName)
    {
        try
        {
            var project = await ProjectStorage.LoadAsync(fileName);
            if (!Uri.TryCreate(project.RootUrl, UriKind.Absolute, out var root)) throw new InvalidDataException("آدرس پروژه معتبر نیست.");
            _url.Text = root.AbsoluteUri;
            if (_dashboardUrl is not null) _dashboardUrl.Text = root.AbsoluteUri;
            _output.Text = Path.GetDirectoryName(fileName) ?? _output.Text;
            _authCookies = AuthCookieStore.Load(Path.Combine(_output.Text, "auth.cookies"));
            _login.Text = _authCookies.Count > 0 ? "نشست فعال" : "ورود به سایت";
            _status.Text = "پروژه در فرم اصلی بارگذاری شد؛ آدرس را می‌توانید ویرایش کنید.";
            FocusHome();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "خطای بارگذاری پروژه", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void ShowSettings()
    {
        using var form = new SettingsForm(AppSettingsStore.Load());
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            Localization.Apply(this, AppSettingsStore.Load().Language);
            NormalizeDashboardDirection();
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

    private void LiveArchiveClick(object? sender, EventArgs e)
    {
        if (_cts is not null)
        {
            MessageBox.Show(this, "ابتدا عملیات فعلی را متوقف کنید تا ذخیره زنده اجرا شود.", "ذخیره زنده", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!Uri.TryCreate(_url.Text.Trim(), UriKind.Absolute, out var root) || root.Scheme is not ("http" or "https"))
        {
            MessageBox.Show(this, "ابتدا یک آدرس معتبر وارد کنید.", "ذخیره زنده", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _url.Focus();
            return;
        }
        var output = _output.Text.Trim();
        if (string.IsNullOrWhiteSpace(output))
        {
            using var dialog = new FolderBrowserDialog { Description = "پوشه ذخیره حالت زنده را انتخاب کنید" };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            output = dialog.SelectedPath;
            _output.Text = output;
        }
        using var form = new LiveArchiveForm(root, output);
        form.ShowDialog(this);
    }

    private void CopyWebClick(object? sender, EventArgs e)
    {
        if (_cts is not null)
        {
            MessageBox.Show(this, "ابتدا عملیات فعلی را متوقف کنید تا حالت کپی وبی اجرا شود.", "کپی وبی", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!Uri.TryCreate(_url.Text.Trim(), UriKind.Absolute, out var root) || root.Scheme is not ("http" or "https"))
        {
            MessageBox.Show(this, "ابتدا یک آدرس معتبر وب وارد کنید.", "کپی وبی", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _url.Focus();
            return;
        }
        var output = _output.Text.Trim();
        if (string.IsNullOrWhiteSpace(output))
        {
            using var dialog = new FolderBrowserDialog { Description = "پوشه ذخیره حالت کپی وبی را انتخاب کنید" };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            output = dialog.SelectedPath;
            _output.Text = output;
        }
        using var form = new LiveArchiveForm(root, output, manualNavigation: true);
        form.ShowDialog(this);
    }

    private void ApplyThemeToControls()
    {
        BackColor = UiTheme.Background;
        ApplyThemeToControl(this, false);
        ApplyModernTheme(this);
        Localization.Apply(this, AppSettingsStore.Load().Language);
        NormalizeDashboardDirection();
        NormalizeEditorDirection();
        Invalidate(true);
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
            AppendLog($"API محلی روی http://localhost:{settings.LocalApiPort}/api/status فعال شد.");
        }
        catch (Exception ex) { AppendLog($"فعال‌سازی API محلی ناموفق بود: {ex.Message}", ActivitySeverity.Warning); }
    }

    private static void ApplyThemeToControl(Control control, bool inSidebar)
    {
        var sidebar = inSidebar || control.Tag is "sidebar";
        if (control is CheckBox checkBox)
        {
            checkBox.ForeColor = sidebar ? Color.White : UiTheme.Text;
            checkBox.BackColor = Color.Transparent;
        }
        switch (control.Tag)
        {
            case "sidebar":
                control.BackColor = UiTheme.Primary;
                break;
            case "sidebar-button":
                control.BackColor = UiTheme.Background;
                control.ForeColor = UiTheme.Muted;
                break;
            case "sidebar-selected":
                control.BackColor = Color.FromArgb(68, 52, 104);
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
                control.BackColor = Color.White;
                control.ForeColor = Color.FromArgb(30, 41, 59);
                break;
            case "log":
                control.BackColor = UiTheme.Surface;
                control.ForeColor = UiTheme.Text;
                break;
            case "primary-button":
                control.BackColor = UiTheme.Primary;
                control.ForeColor = Color.White;
                break;
            case "action-button":
                control.BackColor = UiTheme.Action;
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

    private static void ApplyModernTheme(Control control, bool inSidebar = false)
    {
        var sidebar = inSidebar || control.Tag is "sidebar";
        switch (control)
        {
            case GradientPanel gradient:
                gradient.StartColor = sidebar ? ControlPaint.Light(UiTheme.Background, 0.04F) : UiTheme.Background;
                gradient.EndColor = sidebar ? UiTheme.Background : ControlPaint.Light(UiTheme.Background, 0.055F);
                gradient.Invalidate();
                break;
            case DashboardCard card:
                card.BackColor = UiTheme.Surface;
                card.BorderColor = UiTheme.Border;
                card.Invalidate();
                break;
            case DashboardButton button:
                switch (button.Tag)
                {
                    case "sidebar-selected":
                    case "modern-primary":
                        button.FillStart = UiTheme.Action;
                        button.FillEnd = ControlPaint.Light(UiTheme.Action, 0.10F);
                        button.OutlineColor = ControlPaint.Light(UiTheme.Action, 0.24F);
                        button.ForeColor = Color.White;
                        break;
                    case "sidebar-button":
                    case "modern-secondary":
                        button.FillStart = UiTheme.Surface;
                        button.FillEnd = ControlPaint.Light(UiTheme.Surface, 0.05F);
                        button.OutlineColor = UiTheme.Border;
                        button.ForeColor = UiTheme.Text;
                        break;
                }
                button.Invalidate();
                break;
            case DashboardProgressBar progress:
                progress.TrackColor = ControlPaint.Light(UiTheme.Surface, 0.06F);
                progress.FillColor = UiTheme.Action;
                progress.BackColor = progress.TrackColor;
                progress.Invalidate();
                break;
            case DashboardNumericInput numeric:
                numeric.ApplyTheme(ControlPaint.Light(UiTheme.Surface, 0.045F), UiTheme.Border, UiTheme.Text, UiTheme.Action);
                break;
            case DashboardCheckBox checkBox:
                checkBox.ApplyTheme(UiTheme.Text, UiTheme.Border, UiTheme.Action);
                break;
            case TextBox textBox when !sidebar:
                textBox.BackColor = ControlPaint.Light(UiTheme.Surface, 0.045F);
                textBox.ForeColor = UiTheme.Text;
                break;
            case RichTextBox richTextBox:
                richTextBox.BackColor = ControlPaint.Dark(UiTheme.Background, 0.08F);
                richTextBox.ForeColor = UiTheme.Text;
                break;
            case ComboBox comboBox when !sidebar:
                comboBox.BackColor = ControlPaint.Light(UiTheme.Surface, 0.045F);
                comboBox.ForeColor = UiTheme.Text;
                break;
        }

        foreach (Control child in control.Controls)
            ApplyModernTheme(child, sidebar);
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
        Directory.CreateDirectory(_output.Text); _authCookies = _authCookies.Count > 0 ? _authCookies : AuthCookieStore.Load(Path.Combine(_output.Text, "auth.cookies")); BeginLog(Path.Combine(_output.Text, "activity.log"), append: false); PrepareOperation();
        _ = CaptureAutomaticScreenshotAsync(root, Path.Combine(_output.Text, "screenshots", "before.png"));
        try
        {
            using var session = CreateSession(); session.ImportCookies(_authCookies); var crawler = new SiteCrawler(session);
            var crawlProgress = new Progress<CrawlProgress>(p => { _status.Text = p.Message; _stats.Text = $"{p.Processed} صفحه بررسی | {p.Discovered} لینک پیدا شد"; _counts.Text = $"صفحات پیدا‌شده: {p.Discovered}"; UpdateModernDashboardCrawl(p); AppendLog(p.Message); });
            var links = await crawler.CrawlAsync(root, BuildCrawlOptions(), ShowCaptchaAsync, crawlProgress, _cts!.Token, checkpoint: found => ProjectStorage.SaveAsync(Path.Combine(_output.Text, "links.json"), root, found, CurrentProxySnapshot()), renderHandler: RenderPageAsync);
            IReadOnlyCollection<DownloadItem> selectedLinks;
            if (_advancedMode)
            {
                using var linksForm = new LinksForm(root, links);
                if (linksForm.ShowDialog(this) != DialogResult.OK) return;
                selectedLinks = linksForm.Items;
            }
            else
            {
                // Simple mode downloads every discovered internal page and resource
                // immediately, without opening the selection window.
                foreach (var link in links) { link.IsSelected = true; foreach (var resource in link.Resources) resource.IsSelected = true; }
                selectedLinks = links;
            }
            await ProjectStorage.SaveAsync(Path.Combine(_output.Text, "links.json"), root, selectedLinks, CurrentProxySnapshot(), _cts.Token);
            await DownloadItemsAsync(session, root, selectedLinks, _output.Text); CompleteOperation();
        }
        catch (OperationCanceledException) { CancelledOperation(); }
        catch (Exception ex) { FailedOperation(ex); }
        finally { FinishOperation(); }
    }

    private async void LoginClick(object? sender, EventArgs e)
    {
        if (!Uri.TryCreate(_url.Text.Trim(), UriKind.Absolute, out var root) || root.Scheme is not ("http" or "https"))
        {
            MessageBox.Show(this, "ابتدا آدرس سایت را وارد کنید.", "ورود به سایت", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(_output.Text)) _output.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CopyWeb", root.Host);
        try
        {
            using var form = new LoginForm(root);
            if (form.ShowDialog(this) != DialogResult.OK) return;
            _authCookies = form.Cookies;
            Directory.CreateDirectory(_output.Text);
            if (form.RememberSession) AuthCookieStore.Save(Path.Combine(_output.Text, "auth.cookies"), _authCookies);
            _login.Text = "نشست فعال";
            _login.BackColor = Color.FromArgb(177, 205, 190);
            MessageBox.Show(this, $"ورود با موفقیت ثبت شد. {_authCookies.Count:N0} Cookie برای دانلود استفاده می‌شود.", "نشست کاربر", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "خطای ورود", MessageBoxButtons.OK, MessageBoxIcon.Error); }
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
            _url.Text = root.AbsoluteUri; _output.Text = Path.GetDirectoryName(fileName) ?? _output.Text; _authCookies = AuthCookieStore.Load(Path.Combine(_output.Text, "auth.cookies")); RestoreProxySnapshot(project.Proxy); BeginLog(Path.Combine(_output.Text, "activity.log"), append: true); PrepareOperation(); _ = CaptureAutomaticScreenshotAsync(root, Path.Combine(_output.Text, "screenshots", "before.png")); using var session = CreateSession(); session.ImportCookies(_authCookies);
            var crawlCheckpoint = project.Links.Any(x => x.State is LinkState.Pending or LinkState.Failed or LinkState.Downloading) && !project.Links.Any(x => x.State == LinkState.Downloaded);
            if (crawlCheckpoint)
            {
                var crawler = new SiteCrawler(session); var crawlProgress = new Progress<CrawlProgress>(p => { _status.Text = p.Message; _stats.Text = $"{p.Processed} صفحه بررسی | {p.Discovered} لینک پیدا شد"; _counts.Text = $"صفحات پیدا‌شده: {p.Discovered}"; UpdateModernDashboardCrawl(p); AppendLog(p.Message); });
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
            var successful = links.Count(x => x.State == LinkState.Downloaded);
            _counts.Text = $"کل {p.Total} | موفق {successful} | خطا {p.Failed} | فعال {p.ActiveDownloads} | صف {p.Queued} | آزاد {FormatBytes(p.FreeDiskBytes)}";
            UpdateSpeedAndEta(p.Completed, p.Total, p.TotalBytesDownloaded);
            UpdateModernDashboard(p, successful, totalPercent);
            var severity = p.Message.Contains("ناموفق", StringComparison.OrdinalIgnoreCase) ? ActivitySeverity.Warning : ActivitySeverity.Info;
            AppendLog($"{p.CurrentPercent}% | {p.Message}", severity, p.CurrentUrl);
            _downloadMonitor?.UpdateProgress(p);
        });
        try
        {
            await downloader.DownloadAsync(root, links, output, downloadProgress, _cts!.Token, (int)_requestDelay.Value, (int)_concurrency.Value, (long)_minFreeDisk.Value, (int)_speedLimit.Value, (int)_domainConnections.Value, CurrentProxySnapshot());
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

    private void UpdateModernDashboardCrawl(CrawlProgress progress)
    {
        SetModernDashboardState(progress.Message, Color.FromArgb(96, 165, 250));
        _dashboardQueued = Math.Max(0, progress.Discovered - progress.Processed);
        _dashboardActive = progress.Processed < progress.Discovered ? 1 : 0;
        _dashboardSucceeded = progress.Processed;
        _dashboardFailed = 0;
        var percent = progress.Discovered == 0 ? 0 : Math.Clamp(progress.Processed * 100 / progress.Discovered, 0, 100);
        if (_dashboardProgress is not null) _dashboardProgress.Value = percent;
        if (_editorProgress is not null) _editorProgress.Value = percent;
        if (_dashboardPercentValue is not null) _dashboardPercentValue.Text = $"{percent}%";
        if (_dashboardCurrentValue is not null) _dashboardCurrentValue.Text = $"بررسی صفحات: {progress.Processed:N0} از {progress.Discovered:N0}";
        if (_dashboardCountsValue is not null) _dashboardCountsValue.Text = $"پیدا‌شده {progress.Discovered:N0}  •  بررسی‌شده {progress.Processed:N0}";
        if (_sidebarProgressLabel is not null) _sidebarProgressLabel.Text = $"{percent}%";
        _sidebarCloudStatus?.SetProgress(percent);
        if (_sidebarDetailLabel is not null) _sidebarDetailLabel.Text = $"{progress.Processed:N0} از {progress.Discovered:N0} صفحه";
        UpdateModernDashboardDonuts();
    }

    private void UpdateModernDashboard(DownloadProgress progress, int successful, int totalPercent)
    {
        _dashboardSucceeded = successful;
        _dashboardFailed = progress.Failed;
        _dashboardQueued = progress.Queued;
        _dashboardActive = progress.ActiveDownloads;
        SetModernDashboardState(progress.Message, progress.Failed > 0 ? Color.FromArgb(251, 191, 36) : Color.FromArgb(96, 165, 250));
        if (_dashboardProgress is not null) _dashboardProgress.Value = totalPercent;
        if (_dashboardFileProgress is not null) _dashboardFileProgress.Value = Math.Clamp(progress.CurrentPercent, 0, 100);
        if (_editorProgress is not null) _editorProgress.Value = totalPercent;
        if (_editorFileProgress is not null) _editorFileProgress.Value = Math.Clamp(progress.CurrentPercent, 0, 100);
        if (_dashboardPercentValue is not null) _dashboardPercentValue.Text = $"{totalPercent}%";
        if (_dashboardCurrentValue is not null) _dashboardCurrentValue.Text = $"فایل فعلی ({progress.CurrentPercent}%): {progress.CurrentUrl ?? "—"}";
        if (_dashboardCountsValue is not null) _dashboardCountsValue.Text = $"کل {progress.Total:N0}  •  موفق {successful:N0}  •  خطا {progress.Failed:N0}  •  صف {progress.Queued:N0}";
        if (_dashboardSpeedValue is not null) _dashboardSpeedValue.Text = _speed.Text;
        if (_dashboardEtaValue is not null) _dashboardEtaValue.Text = _eta.Text;
        if (_sidebarProgressLabel is not null) _sidebarProgressLabel.Text = $"{totalPercent}%";
        _sidebarCloudStatus?.SetProgress(totalPercent);
        if (_sidebarDetailLabel is not null) _sidebarDetailLabel.Text = $"{successful:N0} موفق  •  {progress.Failed:N0} خطا";
        UpdateModernDashboardDonuts();
    }

    private void UpdateModernDashboardDonuts()
    {
        var total = _dashboardSucceeded + _dashboardActive + _dashboardQueued + _dashboardFailed;
        var processed = _dashboardSucceeded + _dashboardFailed;
        var percent = total == 0 ? 0 : processed * 100 / total;
        if (_sidebarProgress is not null) _sidebarProgress.Value = percent;
        _sidebarCloudStatus?.SetProgress(percent);
    }

    private void SetModernDashboardState(string text, Color color)
    {
        if (_dashboardStatusValue is not null) { _dashboardStatusValue.Text = text; _dashboardStatusValue.ForeColor = color; }
        if (_sidebarStatus is not null) { _sidebarStatus.Text = text; _sidebarStatus.ForeColor = color; }
    }

    private void ResetModernDashboard()
    {
        _dashboardSucceeded = 0;
        _dashboardFailed = 0;
        _dashboardQueued = 0;
        _dashboardActive = 0;
        if (_dashboardProgress is not null) _dashboardProgress.Value = 0;
        if (_dashboardFileProgress is not null) _dashboardFileProgress.Value = 0;
        if (_editorProgress is not null) _editorProgress.Value = 0;
        if (_editorFileProgress is not null) _editorFileProgress.Value = 0;
        if (_dashboardPercentValue is not null) _dashboardPercentValue.Text = "۰٪";
        if (_dashboardCurrentValue is not null) _dashboardCurrentValue.Text = "فایل فعلی: —";
        if (_dashboardCountsValue is not null) _dashboardCountsValue.Text = "کل ۰  •  موفق ۰  •  خطا ۰";
        if (_dashboardSpeedValue is not null) _dashboardSpeedValue.Text = "سرعت: —";
        if (_dashboardEtaValue is not null) _dashboardEtaValue.Text = "زمان باقی‌مانده: —";
        if (_sidebarProgressLabel is not null) _sidebarProgressLabel.Text = "۰٪";
        _sidebarCloudStatus?.SetProgress(0);
        if (_sidebarDetailLabel is not null) _sidebarDetailLabel.Text = "در حال آماده‌سازی";
        SetModernDashboardState("در حال آماده‌سازی", Color.FromArgb(96, 165, 250));
        UpdateModernDashboardDonuts();
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
        ResetModernDashboard();
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
        SetModernDashboardState("دانلود با موفقیت پایان یافت", Color.FromArgb(74, 222, 128));
        if (_dashboardProgress is not null) _dashboardProgress.Value = 100;
        if (_editorProgress is not null) _editorProgress.Value = 100;
        if (_editorFileProgress is not null) _editorFileProgress.Value = 100;
        if (_dashboardPercentValue is not null) _dashboardPercentValue.Text = "۱۰۰٪";
        AppendLog("عملیات با موفقیت پایان یافت.", ActivitySeverity.Success);
        var settings = AppSettingsStore.Load();
        if (settings.EnableCompletionNotification)
            _ = NotificationService.NotifyAsync("CopyWeb", "دانلود نسخه آفلاین سایت با موفقیت پایان یافت.", settings.CompletionWebhook, settings.CompletionEmail);
        _ = CreateSnapshotAfterOperationAsync();
        if (Uri.TryCreate(_url.Text.Trim(), UriKind.Absolute, out var root))
            _ = CaptureAutomaticScreenshotAsync(root, Path.Combine(_output.Text, "screenshots", "after.png"), local: true);
        _ = RefreshDashboardProjectsAsync();
        MessageBox.Show(this, "نسخه آفلاین سایت ذخیره شد.", "پایان عملیات", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task CreateSnapshotAfterOperationAsync()
    {
        try { if (Directory.Exists(_output.Text)) await SnapshotVersionService.CreateAsync(_output.Text); }
        catch (Exception ex) { AppendLog($"ایجاد Snapshot انجام نشد: {ex.Message}", ActivitySeverity.Warning); }
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
        SetModernDashboardState("متوقف شد؛ آماده ادامه", Color.FromArgb(251, 191, 36));
        _ = RefreshDashboardProjectsAsync();
        AppendLog("برای ادامه، روی «ادامه پروژه» کلیک کنید.", ActivitySeverity.Warning);
    }

    private void FailedOperation(Exception ex)
    {
        _status.Text = "خطا";
        SetModernDashboardState("خطا: " + ex.Message, Color.FromArgb(248, 113, 113));
        _dashboardFailed = Math.Max(1, _dashboardFailed);
        UpdateModernDashboardDonuts();
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
