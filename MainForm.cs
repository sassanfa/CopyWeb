using CopyWeb.Models;
using CopyWeb.Services;
using LinkState = CopyWeb.Models.LinkState;
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
    private readonly CheckBox _proxyEnabled = new();
    private readonly TextBox _proxyAddress = new();
    private readonly TextBox _proxyPort = new();
    private readonly TextBox _proxyUser = new();
    private readonly TextBox _proxyPassword = new();
    private readonly ProgressBar _progress = new();
    private readonly ProgressBar _fileProgress = new();
    private readonly Label _status = UiTheme.Label("آماده شروع", 9, color: UiTheme.Muted);
    private readonly Label _currentFile = UiTheme.Label("فایل فعلی: -", 9, color: UiTheme.Muted);
    private readonly Label _stats = UiTheme.Label("هنوز عملیاتی انجام نشده است", 10, FontStyle.Bold);
    private readonly RichTextBox _log = new();
    private readonly Button _start = UiTheme.Button("شروع بررسی سایت");
    private readonly Button _resume = UiTheme.Button("ادامه پروژه", Color.FromArgb(5, 150, 105));
    private readonly Button _stop = UiTheme.Button("توقف و ذخیره", UiTheme.Danger);
    private CancellationTokenSource? _cts;
    private string? _activeLogPath;

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
        BuildUi();
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

        var operations = UiTheme.Card(); operations.Dock = DockStyle.Top; operations.Height = 318;
        var operationsTitle = UiTheme.Label("عملیات", 13, FontStyle.Bold); operationsTitle.Location = new Point(22, 18);
        _start.Width = 242; _start.Height = 48; _start.Location = new Point(22, 58); _start.Click += StartClick;
        _resume.Tag = "accent-button"; _resume.Width = 242; _resume.Height = 48; _resume.Location = new Point(22, 116); _resume.Click += ResumeClick;
        _stop.Tag = "danger-button"; _stop.Width = 242; _stop.Height = 48; _stop.Location = new Point(22, 174); _stop.Enabled = false; _stop.Click += (_, _) => _cts?.Cancel();
        var testProxy = UiTheme.Button("تست پروکسی", Color.White); testProxy.Tag = "secondary-button"; testProxy.ForeColor = UiTheme.Text; testProxy.Width = 242; testProxy.Height = 48; testProxy.Location = new Point(22, 232); testProxy.Click += TestProxyClick;
        operations.Controls.AddRange([operationsTitle, _start, _resume, _stop, testProxy]);

        var info = UiTheme.Card(); info.Dock = DockStyle.Fill; info.Margin = new Padding(0, 14, 0, 0);
        var infoTitle = UiTheme.Label("اطلاعات پروژه", 12, FontStyle.Bold); infoTitle.Location = new Point(22, 18);
        _status.Location = new Point(22, 55); _currentFile.Location = new Point(22, 86); _stats.Location = new Point(22, 122);
        _progress.Location = new Point(22, 164); _progress.Width = 242; _progress.Height = 20;
        _fileProgress.Location = new Point(22, 204); _fileProgress.Width = 242; _fileProgress.Height = 20;
        info.Controls.AddRange([infoTitle, _status, _currentFile, _stats, _progress, _fileProgress]);
        rightColumn.Controls.Add(info); rightColumn.Controls.Add(operations);

        var settings = UiTheme.Card(); settings.Dock = DockStyle.Top; settings.Height = 300;
        var settingsTitle = UiTheme.Label("تنظیمات دانلود", 13, FontStyle.Bold); settingsTitle.Location = new Point(22, 18);
        var urlLabel = UiTheme.Label("آدرس سایت", 10, FontStyle.Bold); urlLabel.Location = new Point(22, 52);
        ConfigureInput(_url); _url.PlaceholderText = "https://example.com"; _url.Multiline = false; _url.AutoSize = false; _url.Height = 30; _url.Location = new Point(22, 125); _url.Width = 460;
        var urlLine = new Panel { BackColor = UiTheme.Border, Height = 1, Width = 460, Location = new Point(22, 175), Tag = "border" };
        var maxLabel = UiTheme.Label("حداکثر صفحه", 9, color: UiTheme.Muted); maxLabel.Location = new Point(22, 197);
        _maxPages.Minimum = 1; _maxPages.Maximum = 10000; _maxPages.Value = 500; _maxPages.Width = 130; _maxPages.Location = new Point(22, 223);
        var depthLabel = UiTheme.Label("عمق لینک", 9, color: UiTheme.Muted); depthLabel.Location = new Point(172, 197);
        _depth.Minimum = 0; _depth.Maximum = 20; _depth.Value = 3; _depth.Width = 130; _depth.Location = new Point(172, 223);
        _subdomains.Text = "شامل زیردامنه‌ها"; _subdomains.Checked = true; _subdomains.AutoSize = true; _subdomains.Location = new Point(322, 228);
        _robots.Text = "رعایت robots.txt"; _robots.Checked = true; _robots.AutoSize = true; _robots.Location = new Point(440, 228);
        settings.Controls.AddRange([settingsTitle, urlLabel, _url, urlLine, maxLabel, _maxPages, depthLabel, _depth, _subdomains, _robots]);

        var proxy = UiTheme.Card(); proxy.Dock = DockStyle.Top; proxy.Height = 142;
        var proxyTitle = UiTheme.Label("احراز هویت پروکسی (اختیاری)", 12, FontStyle.Bold); proxyTitle.Location = new Point(22, 16);
        _proxyEnabled.Text = "فعال"; _proxyEnabled.AutoSize = true; _proxyEnabled.Location = new Point(235, 19);
        ConfigureInput(_proxyAddress); _proxyAddress.PlaceholderText = "دامنه پروکسی"; _proxyAddress.Width = 130; _proxyAddress.Location = new Point(22, 58);
        ConfigureInput(_proxyPort); _proxyPort.PlaceholderText = "پورت"; _proxyPort.Width = 65; _proxyPort.Location = new Point(162, 58);
        ConfigureInput(_proxyUser); _proxyUser.PlaceholderText = "نام کاربری"; _proxyUser.Width = 130; _proxyUser.Location = new Point(237, 58);
        ConfigureInput(_proxyPassword); _proxyPassword.PlaceholderText = "رمز عبور"; _proxyPassword.Width = 105; _proxyPassword.UseSystemPasswordChar = true; _proxyPassword.Location = new Point(377, 58);
        proxy.Controls.AddRange([proxyTitle, _proxyEnabled, _proxyAddress, _proxyPort, _proxyUser, _proxyPassword]);

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

        Controls.Add(root);
    }

    private Panel BuildSidebar()
    {
        var sidebar = new Panel { Dock = DockStyle.Left, Width = 270, BackColor = UiTheme.Primary, Padding = new Padding(24, 24, 24, 18), Tag = "sidebar" };
        var brand = new Panel { Dock = DockStyle.Top, Height = 132, BackColor = Color.Transparent };
        var cloud = UiTheme.Label("☁", 42, FontStyle.Bold, Color.White); cloud.AutoSize = false; cloud.Location = new Point(8, 4); cloud.Size = new Size(68, 58); cloud.RightToLeft = RightToLeft.No; cloud.TextAlign = ContentAlignment.MiddleCenter;
        var name = UiTheme.Label("CopyWeb", 20, FontStyle.Bold, Color.White); name.AutoSize = false; name.Location = new Point(82, 8); name.Size = new Size(125, 38); name.RightToLeft = RightToLeft.No; name.TextAlign = ContentAlignment.MiddleLeft;
        var tagline = UiTheme.Label("دانلود نسخه آفلاین سایت", 9, color: Color.FromArgb(220, 235, 255)); tagline.Location = new Point(82, 48);
        brand.Controls.AddRange([cloud, name, tagline]);
        var nav = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 300, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(0, 12, 0, 0) };
        var home = NavButton("⌂   شروع", true); home.Click += (_, _) => FocusHome();
        var projects = NavButton("□   پروژه‌ها", false); projects.Click += (_, _) => ShowProjects();
        var settings = NavButton("⚙   تنظیمات", false); settings.Click += (_, _) => ShowSettings();
        var reports = NavButton("▥   گزارش‌ها", false); reports.Click += (_, _) => ShowReports();
        var about = NavButton("ⓘ   درباره برنامه", false); about.Click += (_, _) => ShowAbout();
        nav.Controls.AddRange([home, projects, settings, reports, about]);
        var version = UiTheme.Label("نسخه 1.0.13", 9, color: Color.FromArgb(215, 232, 255)); version.Dock = DockStyle.Bottom; version.TextAlign = ContentAlignment.MiddleCenter; version.Height = 30;
        sidebar.Controls.Add(version); sidebar.Controls.Add(nav); sidebar.Controls.Add(brand);
        return sidebar;
    }

    private static Button NavButton(string text, bool selected)
    {
        var button = UiTheme.Button(text, selected ? Color.FromArgb(67, 140, 235) : Color.FromArgb(47, 128, 237));
        button.Tag = "sidebar-button";
        button.Width = 222; button.Height = 48; button.Margin = new Padding(0, 0, 0, 10); button.TextAlign = ContentAlignment.MiddleLeft; button.Padding = new Padding(18, 0, 12, 0); button.Font = new Font(UiTheme.NormalFont, FontStyle.Bold);
        return button;
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
            ApplyThemeToControls();
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

    private void ApplyThemeToControls()
    {
        BackColor = UiTheme.Background;
        ApplyThemeToControl(this, false);
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
                control.BackColor = Color.FromArgb(5, 150, 105);
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

    private async void StartClick(object? sender, EventArgs e)
    {
        if (!Uri.TryCreate(_url.Text.Trim(), UriKind.Absolute, out var root) || root.Scheme is not ("http" or "https"))
        { MessageBox.Show(this, "لطفاً یک آدرس معتبر HTTP یا HTTPS وارد کنید.", "آدرس نامعتبر", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        if (string.IsNullOrWhiteSpace(_output.Text)) _output.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CopyWeb", root.Host);
        Directory.CreateDirectory(_output.Text); BeginLog(Path.Combine(_output.Text, "activity.log"), append: false); PrepareOperation();
        try
        {
            using var session = CreateSession(); var crawler = new SiteCrawler(session);
            var crawlProgress = new Progress<CrawlProgress>(p => { _status.Text = p.Message; _stats.Text = $"{p.Processed} صفحه بررسی | {p.Discovered} لینک پیدا شد"; AppendLog(p.Message); });
            var links = await crawler.CrawlAsync(root, new CrawlOptions { MaxDepth = (int)_depth.Value, MaxPages = (int)_maxPages.Value, IncludeSubdomains = _subdomains.Checked, RespectRobotsTxt = _robots.Checked }, ShowCaptchaAsync, crawlProgress, _cts!.Token, checkpoint: found => ProjectStorage.SaveAsync(Path.Combine(_output.Text, "links.json"), root, found));
            using var linksForm = new LinksForm(root, links); if (linksForm.ShowDialog(this) != DialogResult.OK) return;
            await ProjectStorage.SaveAsync(Path.Combine(_output.Text, "links.json"), root, linksForm.Items, _cts.Token);
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
            _url.Text = root.AbsoluteUri; _output.Text = Path.GetDirectoryName(fileName) ?? _output.Text; BeginLog(Path.Combine(_output.Text, "activity.log"), append: true); PrepareOperation(); using var session = CreateSession();
            var crawlCheckpoint = project.Links.Any(x => x.State is LinkState.Pending or LinkState.Failed or LinkState.Downloading) && !project.Links.Any(x => x.State == LinkState.Downloaded);
            if (crawlCheckpoint)
            {
                var crawler = new SiteCrawler(session); var crawlProgress = new Progress<CrawlProgress>(p => { _status.Text = p.Message; _stats.Text = $"{p.Processed} صفحه بررسی | {p.Discovered} لینک پیدا شد"; AppendLog(p.Message); });
                project.Links = await crawler.CrawlAsync(root, new CrawlOptions { MaxDepth = (int)_depth.Value, MaxPages = (int)_maxPages.Value, IncludeSubdomains = _subdomains.Checked, RespectRobotsTxt = _robots.Checked }, ShowCaptchaAsync, crawlProgress, _cts!.Token, project.Links, found => ProjectStorage.SaveAsync(Path.Combine(_output.Text, "links.json"), root, found));
            }
            using var linksForm = new LinksForm(root, project.Links); if (linksForm.ShowDialog(this) != DialogResult.OK) return;
            await ProjectStorage.SaveAsync(Path.Combine(_output.Text, "links.json"), root, linksForm.Items, _cts!.Token); await DownloadItemsAsync(session, root, linksForm.Items, _output.Text); CompleteOperation();
        }
        catch (OperationCanceledException) { CancelledOperation(); }
        catch (Exception ex) { FailedOperation(ex); }
        finally { FinishOperation(); }
    }

    private async Task DownloadItemsAsync(SiteSession session, Uri root, IReadOnlyCollection<DownloadItem> links, string output)
    {
        var downloader = new SiteDownloader(session); var downloadProgress = new Progress<DownloadProgress>(p =>
        {
            _status.Text = p.Message; _currentFile.Text = $"فایل فعلی: {p.CurrentUrl ?? "-"}"; _fileProgress.Value = Math.Clamp(p.CurrentPercent, 0, 100); _progress.Value = p.Total == 0 ? 100 : Math.Clamp(p.Completed * 100 / p.Total, 0, 100); _stats.Text = $"پیشرفت کل: {p.Completed} از {p.Total} صفحه"; AppendLog($"{p.CurrentPercent}% | {p.Message}");
        });
        await downloader.DownloadAsync(root, links, output, downloadProgress, _cts!.Token);
    }

    private SiteSession CreateSession()
    {
        var port = int.TryParse(_proxyPort.Text.Trim(), out var parsedPort) ? parsedPort : 8080; var address = _proxyAddress.Text.Trim(); if (address.Length > 0 && !address.Contains("://", StringComparison.Ordinal)) address = "http://" + address;
        if (_proxyEnabled.Checked && (!Uri.TryCreate(address, UriKind.Absolute, out _) || port is < 1 or > 65535)) throw new InvalidOperationException("آدرس یا پورت پروکسی معتبر نیست.");
        return new SiteSession(new ProxyOptions { Enabled = _proxyEnabled.Checked, Address = address, Port = port, Username = _proxyUser.Text.Trim(), Password = _proxyPassword.Text });
    }

    private async void TestProxyClick(object? sender, EventArgs e)
    {
        if (!_proxyEnabled.Checked) { MessageBox.Show(this, "ابتدا گزینه فعال را برای پروکسی انتخاب کنید.", "تست پروکسی", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        try { using var session = CreateSession(); using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15)); using var response = await session.GetAsync(new Uri("https://api.ipify.org?format=json"), timeout.Token); response.EnsureSuccessStatusCode(); MessageBox.Show(this, $"پروکسی با موفقیت پاسخ داد.\n{await response.Content.ReadAsStringAsync(timeout.Token)}", "تست موفق", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        catch (Exception ex) { MessageBox.Show(this, $"تست پروکسی ناموفق بود:\n{ex.Message}", "خطای پروکسی", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void PrepareOperation() { _cts = new CancellationTokenSource(); _start.Enabled = false; _resume.Enabled = false; _stop.Enabled = true; _progress.Value = 0; _fileProgress.Value = 0; _log.Clear(); }
    private void FinishOperation() { _cts?.Dispose(); _cts = null; _start.Enabled = true; _resume.Enabled = true; _stop.Enabled = false; }
    private void CompleteOperation() { _status.Text = "دانلود با موفقیت پایان یافت"; MessageBox.Show(this, "نسخه آفلاین سایت ذخیره شد.", "پایان عملیات", MessageBoxButtons.OK, MessageBoxIcon.Information); }
    private void CancelledOperation() { _status.Text = "عملیات متوقف شد؛ وضعیت ذخیره شد"; AppendLog("برای ادامه، روی «ادامه پروژه» کلیک کنید."); }
    private void FailedOperation(Exception ex) { _status.Text = "خطا"; AppendLog(ex.ToString()); MessageBox.Show(this, ex.Message, "خطا", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    private Task<IReadOnlyList<BrowserCookie>?> ShowCaptchaAsync(Uri uri, CancellationToken token) { using var form = new CaptchaForm(uri); return Task.FromResult<IReadOnlyList<BrowserCookie>?>(form.ShowDialog(this) == DialogResult.OK ? form.Cookies : null); }
    private void AppendLog(string message)
    {
        if (IsDisposed) return;
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
        _log.AppendText(line + Environment.NewLine);
        if (!AppSettingsStore.Load().SaveDetailedLogs || string.IsNullOrWhiteSpace(_activeLogPath)) return;
        try { File.AppendAllText(_activeLogPath, line + Environment.NewLine, Encoding.UTF8); }
        catch { /* logging must not stop a download */ }
    }
}
