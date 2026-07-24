using System.Diagnostics;
using CopyWeb.Services;

namespace CopyWeb;

public sealed class OfflinePreviewForm : Form
{
    private readonly string _directory;
    private readonly OfflinePreviewServer _server;
    private readonly Label _url = UiTheme.Label(string.Empty, 10, color: UiTheme.Muted);
    private readonly ListBox _broken = new() { Dock = DockStyle.Fill };

    public OfflinePreviewForm(string directory)
    {
        _directory = Path.GetFullPath(directory);
        _server = new OfflinePreviewServer(_directory, requireAuthentication: true);
        Text = "پیش‌نمایش آفلاین و بررسی لینک‌ها";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(760, 520);
        MinimumSize = new Size(620, 420);
        Font = UiTheme.NormalFont;
        RightToLeft = RightToLeft.Yes;
        BackColor = UiTheme.Background;
        BuildUi();
        UiTheme.StyleDialog(this);
        Shown += (_, _) => StartServer();
        FormClosed += (_, _) => _server.Dispose();
    }

    private void BuildUi()
    {
        var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18), BackColor = UiTheme.Background };
        var title = UiTheme.Label("پیش‌نمایش آفلاین داخلی", 17, FontStyle.Bold); title.Dock = DockStyle.Top; title.Height = 38;
        _url.Dock = DockStyle.Top; _url.Height = 30; _url.TextAlign = ContentAlignment.MiddleLeft; _url.RightToLeft = RightToLeft.No;
        var credentials = UiTheme.Label("ورود محلی نسخه آفلاین: admin / admin", 9, FontStyle.Bold, UiTheme.Muted);
        credentials.Dock = DockStyle.Top; credentials.Height = 26;
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var open = UiTheme.Button("باز کردن در مرورگر", UiTheme.Primary); open.Width = 160; open.Click += (_, _) => OpenBrowser();
        var check = UiTheme.Button("بررسی لینک‌های خراب", Color.FromArgb(210, 222, 238)); check.Tag = "secondary-button"; check.ForeColor = UiTheme.Text; check.Width = 170; check.Click += (_, _) => CheckLinks();
        var logout = UiTheme.Button("خروج از حساب محلی", Color.FromArgb(210, 222, 238)); logout.Tag = "secondary-button"; logout.ForeColor = UiTheme.Text; logout.Width = 150; logout.Click += (_, _) => OpenLogout();
        var close = UiTheme.Button("بستن", Color.White); close.Tag = "secondary-button"; close.Width = 90; close.Click += (_, _) => Close();
        actions.Controls.AddRange([close, logout, check, open]);
        var label = UiTheme.Label("لینک‌های خراب محلی (۰ مورد)", 11, FontStyle.Bold); label.Name = "brokenTitle"; label.Dock = DockStyle.Top; label.Height = 32;
        _broken.BackColor = UiTheme.Surface; _broken.BorderStyle = BorderStyle.FixedSingle; _broken.RightToLeft = RightToLeft.No;
        root.Controls.Add(_broken); root.Controls.Add(label); root.Controls.Add(actions); root.Controls.Add(credentials); root.Controls.Add(_url); root.Controls.Add(title); Controls.Add(root);
    }

    private void StartServer()
    {
        try { _server.Start(); _url.Text = _server.BaseUri.AbsoluteUri; CheckLinks(); } catch (Exception ex) { _url.Text = ex.Message; }
    }

    private void OpenBrowser()
    {
        try { Process.Start(new ProcessStartInfo(_server.BaseUri.AbsoluteUri) { UseShellExecute = true }); } catch { }
    }

    private void OpenLogout()
    {
        try { Process.Start(new ProcessStartInfo(new Uri(_server.BaseUri, "__copyweb/logout").AbsoluteUri) { UseShellExecute = true }); } catch { }
    }

    private void CheckLinks()
    {
        var title = Controls.Find("brokenTitle", true).FirstOrDefault();
        var broken = OfflineLinkChecker.Scan(_directory);
        _broken.Items.Clear();
        foreach (var item in broken) _broken.Items.Add($"{item.SourceFile}  ←  {item.Reference}");
        if (title is not null) title.Text = $"لینک‌های خراب محلی ({broken.Count:N0} مورد)";
    }
}
