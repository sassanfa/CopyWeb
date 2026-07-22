using CopyWeb.Models;
using Microsoft.Web.WebView2.WinForms;

namespace CopyWeb;

public sealed class LoginForm : Form
{
    private readonly Uri _uri;
    private readonly WebView2 _browser = new() { Dock = DockStyle.Fill };
    private readonly Label _status = UiTheme.Label("صفحه‌ی ورود را باز کنید و سپس ادامه دهید.", 9, color: UiTheme.Muted);
    private readonly CheckBox _remember = new() { Text = "ذخیره‌ی امن نشست برای Resume بعدی", AutoSize = true, Checked = true };

    public IReadOnlyList<BrowserCookie> Cookies { get; private set; } = [];
    public bool RememberSession => _remember.Checked;

    public LoginForm(Uri uri)
    {
        _uri = uri;
        Text = "ورود به حساب کاربری سایت";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1100, 760);
        MinimumSize = new Size(780, 560);
        Font = UiTheme.NormalFont;
        RightToLeft = RightToLeft.Yes;
        BackColor = UiTheme.Background;
        BuildUi();
        Shown += InitializeAsync;
    }

    private void BuildUi()
    {
        // Reserve a dedicated action row so the buttons never overlap the status
        // text or the WebView2 surface below it.
        var top = new Panel { Dock = DockStyle.Top, Height = 154, BackColor = UiTheme.Surface, Padding = new Padding(18, 12, 18, 8) };
        var title = UiTheme.Label("ورود به سایت", 14, FontStyle.Bold); title.Location = new Point(18, 10);
        var help = UiTheme.Label("با حساب خود وارد شوید؛ پس از ورود روی «استفاده از نشست» بزنید. رمز عبور در CopyWeb ذخیره نمی‌شود.", 9, color: UiTheme.Muted); help.Location = new Point(18, 40); help.AutoSize = false; help.Width = 980; help.Height = 24;
        _status.Location = new Point(18, 67); _status.AutoSize = false; _status.Width = 700; _status.Height = 22;
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 38,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 3, 0, 0),
            RightToLeft = RightToLeft.No
        };
        var use = UiTheme.Button("استفاده از نشست", UiTheme.Primary); use.Width = 150; use.Height = 30; use.Margin = new Padding(0, 0, 8, 0); use.Click += async (_, _) => await ReadCookiesAsync();
        var cancel = UiTheme.Button("لغو", Color.White); cancel.Tag = "secondary-button"; cancel.Width = 90; cancel.Height = 30; cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        // FlowDirection=RightToLeft puts the primary action on the right and
        // the cancel action beside it, away from the web content.
        actions.Controls.AddRange([use, cancel]);
        top.Controls.AddRange([title, help, _status, _remember, actions]);
        _remember.Location = new Point(18, 76);
        Controls.Add(_browser); Controls.Add(top); top.BringToFront();
    }

    private async void InitializeAsync(object? sender, EventArgs e)
    {
        try
        {
            await _browser.EnsureCoreWebView2Async();
            _browser.CoreWebView2.NavigationCompleted += (_, args) => _status.Text = args.IsSuccess ? "صفحه آماده است." : $"خطا در بارگذاری: {args.WebErrorStatus}";
            _browser.CoreWebView2.Navigate(_uri.AbsoluteUri);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "WebView2 Runtime در دسترس نیست.\n" + ex.Message, "خطای مرورگر", MessageBoxButtons.OK, MessageBoxIcon.Error);
            DialogResult = DialogResult.Cancel; Close();
        }
    }

    private async Task ReadCookiesAsync()
    {
        if (_browser.CoreWebView2 is null) return;
        try
        {
            var cookies = await _browser.CoreWebView2.CookieManager.GetCookiesAsync(_uri.GetLeftPart(UriPartial.Authority));
            Cookies = cookies.Select(c => new BrowserCookie(c.Name, c.Value, c.Domain, c.Path, c.IsSession ? null : c.Expires)).ToList();
            if (Cookies.Count == 0) { MessageBox.Show(this, "Cookieای پیدا نشد؛ مطمئن شوید ورود کامل شده است.", "ورود", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            DialogResult = DialogResult.OK; Close();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "خطای دریافت نشست", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}
