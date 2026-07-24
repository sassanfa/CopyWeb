using System.Diagnostics;

namespace CopyWeb;

public sealed class ArchiveSearchForm : Form
{
    private readonly string _root; private readonly TextBox _query = new() { Width = 430, RightToLeft = RightToLeft.No, PlaceholderText = "عبارت مورد جست‌وجو..." }; private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, AutoGenerateColumns = false, BackgroundColor = UiTheme.Surface, BorderStyle = BorderStyle.None };
    public ArchiveSearchForm(string root)
    {
        _root = root; Text = "جست‌وجو در صفحات ذخیره‌شده"; StartPosition = FormStartPosition.CenterParent; Size = new Size(900, 620); Font = UiTheme.NormalFont; RightToLeft = RightToLeft.Yes; BackColor = UiTheme.Background;
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18), BackColor = UiTheme.Background }; var title = UiTheme.Label("جست‌وجوی متن داخل صفحات ذخیره‌شده", 17, FontStyle.Bold); title.Dock = DockStyle.Top; title.Height = 38;
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, FlowDirection = FlowDirection.RightToLeft, WrapContents = false }; var find = UiTheme.Button("جست‌وجو", UiTheme.Primary); find.Width = 110; find.Click += async (_, _) => await SearchAsync(); var close = UiTheme.Button("بستن", Color.White); close.Tag = "secondary-button"; close.Width = 90; close.Click += (_, _) => Close(); bar.Controls.AddRange([close, find, _query]);
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "فایل", Width = 300 }); _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "خط", Width = 70 }); _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "متن پیدا شده", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill }); _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0 && _grid.Rows[e.RowIndex].Cells[0].Value is string file && File.Exists(file)) Process.Start(new ProcessStartInfo(file) { UseShellExecute = true }); };
        panel.Controls.Add(_grid); panel.Controls.Add(bar); panel.Controls.Add(title); Controls.Add(panel);
        UiTheme.StyleDialog(this);
    }
    private async Task SearchAsync()
    {
        var query = _query.Text.Trim(); _grid.Rows.Clear(); if (query.Length == 0) return;
        var rows = await Task.Run(() => Directory.EnumerateFiles(_root, "*.html", SearchOption.AllDirectories).SelectMany(file => File.ReadLines(file).Select((line, index) => (file, line, index))).Where(x => x.line.Contains(query, StringComparison.OrdinalIgnoreCase)).Take(500).ToList());
        foreach (var row in rows) _grid.Rows.Add(row.file, row.index + 1, row.line.Trim());
    }
}
