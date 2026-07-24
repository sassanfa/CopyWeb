using CopyWeb.Services;

namespace CopyWeb;

public sealed class ScheduleForm : Form
{
    private readonly Uri _url;
    private readonly string _output;
    private readonly TextBox _name = new();
    private readonly DateTimePicker _when = new();
    public ScheduleForm(Uri url, string output)
    {
        _url = url; _output = output;
        Text = "زمان‌بندی دانلود"; StartPosition = FormStartPosition.CenterParent; Size = new Size(520, 240); Font = UiTheme.NormalFont; RightToLeft = RightToLeft.Yes; BackColor = UiTheme.Background;
        var title = UiTheme.Label("زمان‌بندی دانلود پروژه", 16, FontStyle.Bold); title.Location = new Point(20, 18); title.AutoSize = true;
        _name.PlaceholderText = "نام زمان‌بندی"; _name.Text = "CopyWeb-" + url.Host; _name.Location = new Point(20, 60); _name.Width = 450; _name.RightToLeft = RightToLeft.No;
        _when.Format = DateTimePickerFormat.Custom; _when.CustomFormat = "yyyy-MM-dd HH:mm"; _when.ShowUpDown = true; _when.Value = DateTime.Now.AddHours(1); _when.Location = new Point(20, 104); _when.Width = 220;
        var info = UiTheme.Label($"سایت: {url.Host}\nخروجی: {_output}", 9, color: UiTheme.Muted); info.Location = new Point(250, 102); info.Size = new Size(220, 46); info.AutoEllipsis = true;
        var save = UiTheme.Button("ثبت زمان‌بندی", UiTheme.Primary); save.Location = new Point(260, 165); save.Width = 130; save.Click += async (_, _) => await SaveAsync();
        var cancel = UiTheme.Button("انصراف", Color.White); cancel.Tag = "secondary-button"; cancel.Location = new Point(400, 165); cancel.Width = 90; cancel.Click += (_, _) => Close();
        Controls.AddRange([title, _name, _when, info, save, cancel]);
        UiTheme.StyleDialog(this);
    }
    private async Task SaveAsync()
    {
        try { await ScheduleService.CreateOneTimeAsync(_name.Text, _url, _output, _when.Value); MessageBox.Show(this, "زمان‌بندی با موفقیت ثبت شد.", "زمان‌بندی", MessageBoxButtons.OK, MessageBoxIcon.Information); DialogResult = DialogResult.OK; Close(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "خطای زمان‌بندی", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}
