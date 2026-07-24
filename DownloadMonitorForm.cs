using CopyWeb.Models;
using CopyWeb.Services;
using LinkState = CopyWeb.Models.LinkState;

namespace CopyWeb;

public sealed class DownloadMonitorForm : Form
{
    private readonly DataGridView _grid = new();
    private readonly Label _summary = UiTheme.Label("در حال آماده‌سازی...", 10, color: UiTheme.Muted);
    private readonly ComboBox _filter = new();
    private readonly TextBox _search = new();
    private readonly ProgressChartPanel _chart = new();
    private readonly Button _retry = UiTheme.Button("تلاش مجدد ناموفق‌ها", UiTheme.Accent);
    private readonly List<DownloadItem> _items = [];
    private readonly Action _cancel;
    private Action<string> _cancelItem;
    private readonly Func<IReadOnlyList<DownloadItem>, Task> _retryCallback;

    public DownloadMonitorForm(IEnumerable<DownloadItem> items, string outputDirectory, Action cancel, Action<string> cancelItem, Func<IReadOnlyList<DownloadItem>, Task> retryCallback)
    {
        _items.AddRange(items);
        _cancel = cancel;
        _cancelItem = cancelItem;
        _retryCallback = retryCallback;
        Text = "وضعیت زنده دانلود";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(980, 560);
        MinimumSize = new Size(760, 420);
        Font = UiTheme.NormalFont;
        RightToLeft = RightToLeft.Yes;
        BackColor = UiTheme.Background;
        BuildUi(outputDirectory);
        UiTheme.StyleDialog(this);
        RefreshRows();
        Localization.Apply(this, AppSettingsStore.Load().Language);
    }

    private void BuildUi(string outputDirectory)
    {
        var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18), BackColor = UiTheme.Background };
        var title = UiTheme.Label("وضعیت زنده دانلود", 18, FontStyle.Bold);
        title.Dock = DockStyle.Top;
        title.Height = 38;
        _summary.Dock = DockStyle.Top;
        _summary.Height = 34;

        var filterBar = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = Color.Transparent };
        _filter.DropDownStyle = ComboBoxStyle.DropDownList; _filter.Items.AddRange(["همه وضعیت‌ها", "در انتظار", "در حال دانلود", "موفق", "ناموفق", "متوقف"]); _filter.SelectedIndex = 0; _filter.Width = 150; _filter.Location = new Point(0, 2); _filter.SelectedIndexChanged += (_, _) => RefreshRows();
        _search.PlaceholderText = "جست‌وجو در URL..."; _search.Width = 300; _search.Location = new Point(160, 2); _search.RightToLeft = RightToLeft.No; _search.TextChanged += (_, _) => RefreshRows();
        filterBar.Controls.AddRange([_filter, _search]);

        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.RowHeadersVisible = false;
        _grid.AutoGenerateColumns = false;
        _grid.BackgroundColor = UiTheme.Surface;
        _grid.BorderStyle = BorderStyle.None;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "آدرس", DataPropertyName = nameof(Row.Url), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "وضعیت", DataPropertyName = nameof(Row.State), Width = 140 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "درصد", DataPropertyName = nameof(Row.Percent), Width = 70 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "خطا", DataPropertyName = nameof(Row.Error), Width = 260 });
        var stopColumn = new DataGridViewButtonColumn { Name = "stop", HeaderText = "توقف", Text = "×", UseColumnTextForButtonValue = true, Width = 52, FlatStyle = FlatStyle.Flat };
        _grid.Columns.Add(stopColumn);
        _grid.CellContentClick += (_, e) =>
        {
            if (e.RowIndex < 0 || _grid.Columns[e.ColumnIndex].Name != "stop") return;
            if (_grid.Rows[e.RowIndex].Cells[0].Value is string url) _cancelItem(url);
        };
        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex < 0 || _grid.Rows[e.RowIndex].Cells[0].Value is not string url || !Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); } catch { }
        };

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = Color.Transparent };
        _retry.Width = 165;
        _retry.Enabled = false;
        _retry.Click += async (_, _) => await RetryFailedAsync();
        var cancel = UiTheme.Button("توقف پروژه", UiTheme.Danger); cancel.Width = 120; cancel.Click += (_, _) => _cancel();
        var open = UiTheme.Button("بازکردن پوشه", Color.White); open.Tag = "secondary-button"; open.Width = 120; open.Click += (_, _) => OpenFolder(outputDirectory);
        var close = UiTheme.Button("بستن", Color.White); close.Tag = "secondary-button"; close.Width = 90; close.Click += (_, _) => Close();
        buttons.Controls.AddRange([close, open, cancel, _retry]);

        root.Controls.Add(_grid);
        _chart.Dock = DockStyle.Bottom; _chart.Height = 82;
        root.Controls.Add(_chart);
        root.Controls.Add(buttons);
        root.Controls.Add(filterBar);
        root.Controls.Add(_summary);
        root.Controls.Add(title);
        Controls.Add(root);
    }

    public void UpdateProgress(DownloadProgress progress)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(() => UpdateProgress(progress)); return; }
        var current = progress.CurrentUrl;
        if (!string.IsNullOrWhiteSpace(current))
        {
            var item = _items.FirstOrDefault(x => x.Url.Equals(current, StringComparison.OrdinalIgnoreCase));
            if (item is not null)
            {
                item.State = progress.CurrentPercent >= 100 ? item.State : LinkState.Downloading;
                if (progress.CurrentPercent < 100) item.Error = null;
            }
        }
        _summary.Text = $"صفحات: {progress.Completed} از {progress.Total} | فعال: {progress.ActiveDownloads} | صف: {progress.Queued} | سرعت: {FormatBytes(progress.TotalBytesDownloaded)}";
        _chart.Add(progress.CurrentPercent);
        RefreshRows(progress.CurrentPercent, current);
    }

    public void SetCancelItem(Action<string> cancelItem) => _cancelItem = cancelItem;

    public void MarkCompleted()
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(MarkCompleted); return; }
        _retry.Enabled = _items.Any(x => x.State == LinkState.Failed);
        RefreshRows();
    }

    private async Task RetryFailedAsync()
    {
        var failed = _items.Where(x => x.State == LinkState.Failed).ToList();
        if (failed.Count == 0) return;
        foreach (var item in failed) { item.State = LinkState.Pending; item.Error = null; }
        _retry.Enabled = false;
        RefreshRows();
        await _retryCallback(failed);
    }

    private void RefreshRows(int currentPercent = 0, string? currentUrl = null)
    {
        var search = _search.Text.Trim();
        var rows = _items.Where(item => (string.IsNullOrWhiteSpace(search) || item.Url.Contains(search, StringComparison.OrdinalIgnoreCase)) && MatchesFilter(item.State))
            .Select(item => new Row(item.Url, StateText(item.State), item.Url.Equals(currentUrl, StringComparison.OrdinalIgnoreCase) ? currentPercent : item.State == LinkState.Downloaded ? 100 : 0, item.Error ?? string.Empty)).ToList();
        _grid.DataSource = rows;
    }

    private bool MatchesFilter(LinkState state) => _filter.SelectedIndex switch
    {
        1 => state is LinkState.Pending or LinkState.Crawled or LinkState.Selected,
        2 => state == LinkState.Downloading,
        3 => state == LinkState.Downloaded,
        4 => state == LinkState.Failed,
        5 => state == LinkState.Skipped,
        _ => true
    };

    private static string StateText(LinkState state) => state switch
    {
        LinkState.Downloaded => "موفق",
        LinkState.Downloading => "در حال دانلود",
        LinkState.Failed => "ناموفق",
        LinkState.Skipped => "رد شده",
        _ => "در انتظار"
    };

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B/s",
        < 1024 * 1024 => $"{bytes / 1024d:0.0} KB/s",
        _ => $"{bytes / (1024d * 1024d):0.0} MB/s"
    };

    private static void OpenFolder(string path)
    {
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    private sealed record Row(string Url, string State, int Percent, string Error);

    private sealed class ProgressChartPanel : Panel
    {
        private readonly List<int> _values = [];
        public ProgressChartPanel() { DoubleBuffered = true; BackColor = UiTheme.Surface; }
        public void Add(int value) { _values.Add(Math.Clamp(value, 0, 100)); if (_values.Count > 80) _values.RemoveAt(0); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); if (_values.Count < 2) return;
            using var pen = new Pen(UiTheme.Primary, 2f); var points = new PointF[_values.Count];
            for (var i = 0; i < _values.Count; i++) points[i] = new PointF(8 + i * Math.Max(1, (Width - 16f) / Math.Max(1, _values.Count - 1)), Height - 8 - (_values[i] / 100f) * (Height - 16));
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; e.Graphics.DrawLines(pen, points);
        }
    }
}
