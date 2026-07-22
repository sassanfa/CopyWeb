using System.Net;
using CopyWeb.Services;

namespace CopyWeb;

public sealed class PublishForm : Form
{
    private readonly string _source;
    private readonly TextBox _ftpUrl = new() { PlaceholderText = "ftp:// یا sftp://server/path", Width = 300, RightToLeft = RightToLeft.No };
    private readonly TextBox _ftpUser = new() { PlaceholderText = "نام کاربری", Width = 140, RightToLeft = RightToLeft.No };
    private readonly TextBox _ftpPassword = new() { PlaceholderText = "رمز عبور", Width = 140, UseSystemPasswordChar = true, RightToLeft = RightToLeft.No };
    private readonly Label _status = UiTheme.Label(string.Empty, 9, color: UiTheme.Muted);

    public PublishForm(string source)
    {
        _source = Path.GetFullPath(source);
        Text = "انتشار پروژه"; StartPosition = FormStartPosition.CenterParent; Size = new Size(720, 360); MinimumSize = new Size(620, 320);
        Font = UiTheme.NormalFont; RightToLeft = RightToLeft.Yes; BackColor = UiTheme.Background; BuildUi();
    }

    private void BuildUi()
    {
        var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18), BackColor = UiTheme.Background };
        var title = UiTheme.Label("انتشار مستقیم پروژه", 17, FontStyle.Bold); title.Dock = DockStyle.Top; title.Height = 36;
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 54, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var zip = UiTheme.Button("ساخت ZIP", UiTheme.Primary); zip.Width = 130; zip.Click += async (_, _) => await CreateZipAsync();
        var iis = UiTheme.Button("آماده‌سازی IIS", Color.FromArgb(210, 222, 238)); iis.Tag = "secondary-button"; iis.ForeColor = UiTheme.Text; iis.Width = 150; iis.Click += async (_, _) => await PrepareIisAsync();
        var close = UiTheme.Button("بستن", Color.White); close.Tag = "secondary-button"; close.Width = 90; close.Click += (_, _) => Close();
        actions.Controls.AddRange([close, iis, zip]);
        var ftpTitle = UiTheme.Label("انتشار روی FTP / SFTP (SFTP با کلید SSH ویندوز)", 11, FontStyle.Bold); ftpTitle.Dock = DockStyle.Top; ftpTitle.Height = 30;
        var ftp = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var upload = UiTheme.Button("آپلود FTP/SFTP", UiTheme.Accent); upload.Width = 145; upload.Click += async (_, _) => await UploadFtpAsync();
        ftp.Controls.AddRange([upload, _ftpPassword, _ftpUser, _ftpUrl]);
        _status.Dock = DockStyle.Fill; _status.TextAlign = ContentAlignment.TopRight;
        root.Controls.Add(_status); root.Controls.Add(ftp); root.Controls.Add(ftpTitle); root.Controls.Add(actions); root.Controls.Add(title); Controls.Add(root);
    }

    private async Task CreateZipAsync()
    {
        using var dialog = new SaveFileDialog { Filter = "ZIP archive|*.zip", FileName = Path.GetFileName(_source) + ".zip" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { await PublishService.CreateZipAsync(_source, dialog.FileName); _status.Text = $"ZIP ساخته شد: {dialog.FileName}"; }
        catch (Exception ex) { _status.Text = ex.Message; }
    }

    private async Task PrepareIisAsync()
    {
        using var dialog = new FolderBrowserDialog { Description = "پوشه مقصد IIS را انتخاب کنید" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var target = Path.Combine(dialog.SelectedPath, Path.GetFileName(_source));
        try { await PublishService.PrepareIisAsync(_source, target); _status.Text = $"پوشه آماده IIS ساخته شد: {target}"; }
        catch (Exception ex) { _status.Text = ex.Message; }
    }

    private async Task UploadFtpAsync()
    {
        if (!Uri.TryCreate(_ftpUrl.Text.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("ftp" or "sftp")) { _status.Text = "آدرس FTP/SFTP معتبر نیست."; return; }
        try
        {
            if (uri.Scheme.Equals("sftp", StringComparison.OrdinalIgnoreCase))
                await PublishService.UploadSftpAsync(_source, uri.Host, _ftpUser.Text.Trim(), string.IsNullOrWhiteSpace(uri.AbsolutePath) ? "/" : uri.AbsolutePath, new Progress<string>(x => _status.Text = $"در حال ارسال: {x}"));
            else
                await PublishService.UploadFtpAsync(_source, new UriBuilder(uri) { Scheme = "ftp" }.Uri, new NetworkCredential(_ftpUser.Text, _ftpPassword.Text), new Progress<string>(x => _status.Text = $"در حال ارسال: {x}"));
            _status.Text = "انتشار با موفقیت تمام شد.";
        }
        catch (Exception ex) { _status.Text = $"خطای FTP: {ex.Message}"; }
    }
}
