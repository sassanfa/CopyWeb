using System.Diagnostics;
using CopyWeb.Services;

namespace CopyWeb;

public sealed class WatchForm : Form
{
    private readonly string _projectFile;
    private readonly NumericUpDown _minutes = new() { Minimum = 1, Maximum = 1440, Value = 15, Width = 90 };
    private readonly Label _status = UiTheme.Label("آماده", 10, color: UiTheme.Muted);
    private readonly ListBox _changes = new() { Dock = DockStyle.Fill };
    private System.Windows.Forms.Timer? _timer;
    private bool _busy;

    public WatchForm(string projectFile)
    {
        _projectFile = projectFile;
        Text = "حالت Watch — همگام‌سازی تغییرات";
        StartPosition = FormStartPosition.CenterParent; Size = new Size(700, 480); MinimumSize = new Size(560, 380);
        Font = UiTheme.NormalFont; RightToLeft = RightToLeft.Yes; BackColor = UiTheme.Background;
        BuildUi();
        UiTheme.StyleDialog(this);
    }

    private void BuildUi()
    {
        var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18), BackColor = UiTheme.Background };
        var title = UiTheme.Label("بررسی دوره‌ای سایت و دانلود فقط تغییرات", 16, FontStyle.Bold); title.Dock = DockStyle.Top; title.Height = 36;
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 50, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        bar.Controls.Add(UiTheme.Label("فاصله بررسی (دقیقه):", 10)); bar.Controls.Add(_minutes);
        var start = UiTheme.Button("شروع Watch", UiTheme.Accent); start.Width = 130; start.Click += (_, _) => StartWatch(start);
        var once = UiTheme.Button("بررسی اکنون", UiTheme.Primary); once.Width = 130; once.Click += async (_, _) => await CheckOnceAsync(true);
        var close = UiTheme.Button("بستن", Color.White); close.Tag = "secondary-button"; close.Width = 90; close.Click += (_, _) => Close();
        bar.Controls.AddRange([close, start, once]);
        _status.Dock = DockStyle.Top; _status.Height = 30;
        _changes.RightToLeft = RightToLeft.No;
        root.Controls.Add(_changes); root.Controls.Add(_status); root.Controls.Add(bar); root.Controls.Add(title); Controls.Add(root);
    }

    private void StartWatch(Button button)
    {
        if (_timer is not null) { _timer.Stop(); _timer.Dispose(); _timer = null; button.Text = "شروع Watch"; _status.Text = "Watch متوقف شد"; return; }
        _timer = new System.Windows.Forms.Timer { Interval = (int)_minutes.Value * 60_000 };
        _timer.Tick += async (_, _) => await CheckOnceAsync(true);
        _timer.Start(); button.Text = "توقف Watch"; _status.Text = "Watch فعال است";
        _ = CheckOnceAsync(false);
    }

    private async Task CheckOnceAsync(bool downloadChanges)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            _status.Text = "در حال بررسی تغییرات...";
            var result = await WatchService.CheckAsync(_projectFile);
            _changes.Items.Clear();
            foreach (var url in result.Changed) _changes.Items.Add(url);
            _status.Text = $"{result.Checked:N0} صفحه بررسی شد؛ {result.Changed.Count:N0} تغییر جدید — {result.Timestamp:HH:mm:ss}";
            if (downloadChanges && result.Changed.Count > 0) await StartCliResumeAsync();
        }
        catch (Exception ex) { _status.Text = $"خطا: {ex.Message}"; }
        finally { _busy = false; }
    }

    private async Task StartCliResumeAsync()
    {
        var project = await ProjectStorage.LoadAsync(_projectFile);
        var output = Path.GetDirectoryName(_projectFile)!;
        var exe = Path.Combine(AppContext.BaseDirectory, "CopyWeb.exe");
        if (!File.Exists(exe)) exe = Environment.ProcessPath ?? string.Empty;
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe)) return;
        var args = $"--cli --resume --url \"{project.RootUrl}\" --output \"{output}\"";
        Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = true, WorkingDirectory = output });
    }
}
