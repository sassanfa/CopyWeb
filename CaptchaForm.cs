using CopyWeb.Models;
using Microsoft.Web.WebView2.WinForms;

namespace CopyWeb;

public sealed class CaptchaForm : Form
{
    private readonly Uri _uri;
    private readonly WebView2 _browser = new() { Dock = DockStyle.Fill };
    private readonly Label _status = UiTheme.Label("صفحه در حال بارگذاری است...", 9, color: UiTheme.Muted);
    public IReadOnlyList<BrowserCookie> Cookies { get; private set; } = [];

    public CaptchaForm(Uri uri)
    {
        _uri = uri;
        Text = "تأیید امنیتی سایت";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1050, 760);
        MinimumSize = new Size(760, 560);
        Font = UiTheme.NormalFont;
        RightToLeft = RightToLeft.Yes;
        BackColor = UiTheme.Background;

        var title = UiTheme.Label("تأیید را داخل مرورگر انجام دهید", 14, FontStyle.Bold);
        var help = UiTheme.Label("پس از حل کپچا و مشاهده صفحه سایت، روی «ادامه دانلود» کلیک کنید.", 9, color: UiTheme.Muted);
        var continueButton = UiTheme.Button("ادامه دانلود");
        continueButton.Width = 145;
        continueButton.Click += ContinueClick;
        var cancelButton = UiTheme.Button("لغو", UiTheme.Muted);
        cancelButton.Width = 90;
        cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Left, FlowDirection = FlowDirection.RightToLeft };
        actions.Controls.Add(continueButton);
        actions.Controls.Add(cancelButton);
        var top = new Panel { Dock = DockStyle.Top, Height = 96, BackColor = Color.White, Padding = new Padding(20, 14, 20, 10) };
        title.Location = new Point(20, 13);
        help.Location = new Point(20, 43);
        _status.Location = new Point(20, 68);
        actions.Location = new Point(20, 25);
        top.Controls.AddRange([title, help, _status, actions]);

        Controls.Add(_browser);
        Controls.Add(top);
        Services.Localization.Apply(this, Services.AppSettingsStore.Load().Language);
        Shown += InitializeBrowserAsync;
    }

    private async void InitializeBrowserAsync(object? sender, EventArgs e)
    {
        try
        {
            await _browser.EnsureCoreWebView2Async();
            _browser.CoreWebView2.NavigationCompleted += (_, args) =>
                _status.Text = args.IsSuccess ? "صفحه آماده است." : $"خطا در بارگذاری: {args.WebErrorStatus}";
            _browser.CoreWebView2.Navigate(_uri.AbsoluteUri);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "WebView2 Runtime در دسترس نیست.\n" + ex.Message, "خطای مرورگر", MessageBoxButtons.OK, MessageBoxIcon.Error);
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }

    private async void ContinueClick(object? sender, EventArgs e)
    {
        if (_browser.CoreWebView2 is null) return;
        try
        {
            var cookies = await _browser.CoreWebView2.CookieManager.GetCookiesAsync(_uri.GetLeftPart(UriPartial.Authority));
            Cookies = cookies.Select(c => new BrowserCookie(
                c.Name, c.Value, c.Domain, c.Path,
                c.IsSession ? null : c.Expires)).ToList();
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "خطای دریافت نشست", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
