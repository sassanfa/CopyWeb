using CopyWeb.Services;
using System.Drawing;

namespace CopyWeb;

public sealed class SnapshotDiffForm : Form
{
    private readonly string _root; private readonly ComboBox _before = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 }; private readonly ComboBox _after = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 }; private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, AutoGenerateColumns = false, BackgroundColor = UiTheme.Surface, BorderStyle = BorderStyle.None };
    private readonly RichTextBox _left = new() { Dock = DockStyle.Fill, ReadOnly = true, Font = new Font("Consolas", 9), BackColor = Color.White, RightToLeft = RightToLeft.No }; private readonly RichTextBox _right = new() { Dock = DockStyle.Fill, ReadOnly = true, Font = new Font("Consolas", 9), BackColor = Color.White, RightToLeft = RightToLeft.No }; private IReadOnlyList<SnapshotDiffEntry> _diff = [];
    public SnapshotDiffForm(string root)
    {
        _root = root; Text = "Snapshot Versioning و Visual Diff"; StartPosition = FormStartPosition.CenterParent; Size = new Size(1100, 720); Font = UiTheme.NormalFont; RightToLeft = RightToLeft.Yes; BackColor = UiTheme.Background;
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18), BackColor = UiTheme.Background }; var title = UiTheme.Label("مقایسه‌ی بصری نسخه‌های سایت و تشخیص تغییرات دقیق", 17, FontStyle.Bold); title.Dock = DockStyle.Top; title.Height = 38;
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, FlowDirection = FlowDirection.RightToLeft, WrapContents = false }; var create = UiTheme.Button("ایجاد Snapshot", UiTheme.Accent); create.Width = 130; create.Click += async (_, _) => await CreateSnapshotAsync(); var compare = UiTheme.Button("مقایسه", UiTheme.Primary); compare.Width = 100; compare.Click += async (_, _) => await CompareAsync(); var close = UiTheme.Button("بستن", Color.White); close.Tag = "secondary-button"; close.Width = 90; close.Click += (_, _) => Close(); bar.Controls.AddRange([close, compare, create, _after, UiTheme.Label("نسخه جدید:"), _before, UiTheme.Label("نسخه قدیم:")]);
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "وضعیت", Width = 100 }); _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "فایل", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill }); _grid.CellClick += (_, e) => { if (e.RowIndex >= 0 && e.RowIndex < _diff.Count) ShowDiff(_diff[e.RowIndex]); };
        var split = new SplitContainer { Dock = DockStyle.Bottom, Height = 230, Orientation = Orientation.Vertical, SplitterDistance = 530 }; split.Panel1.Controls.Add(_left); split.Panel2.Controls.Add(_right);
        panel.Controls.Add(_grid); panel.Controls.Add(split); panel.Controls.Add(bar); panel.Controls.Add(title); Controls.Add(panel); Shown += (_, _) => RefreshSnapshots();
    }
    private void RefreshSnapshots()
    {
        var items = SnapshotVersionService.List(_root); _before.DataSource = items.ToList(); _after.DataSource = items.ToList(); _before.DisplayMember = nameof(SnapshotInfo.Id); _after.DisplayMember = nameof(SnapshotInfo.Id); if (items.Count > 1) { _before.SelectedIndex = 1; _after.SelectedIndex = 0; }
    }
    private async Task CreateSnapshotAsync() { try { await SnapshotVersionService.CreateAsync(_root); RefreshSnapshots(); MessageBox.Show(this, "Snapshot با موفقیت ساخته شد.", "Snapshot", MessageBoxButtons.OK, MessageBoxIcon.Information); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "خطای Snapshot", MessageBoxButtons.OK, MessageBoxIcon.Error); } }
    private async Task CompareAsync()
    {
        if (_before.SelectedItem is not SnapshotInfo before || _after.SelectedItem is not SnapshotInfo after) return; _diff = await SnapshotVersionService.CompareAsync(before.Directory, after.Directory); _grid.Rows.Clear(); foreach (var item in _diff) _grid.Rows.Add(item.Status, item.Path);
    }
    private void ShowDiff(SnapshotDiffEntry item)
    {
        if (_before.SelectedItem is not SnapshotInfo before || _after.SelectedItem is not SnapshotInfo after) return; var oldFile = SnapshotVersionService.ResolveFile(before.Directory, item.Path); var newFile = SnapshotVersionService.ResolveFile(after.Directory, item.Path); _left.Text = File.Exists(oldFile) && IsText(item.Path) ? File.ReadAllText(oldFile) : item.BeforeHash ?? "(وجود ندارد)"; _right.Text = File.Exists(newFile) && IsText(item.Path) ? File.ReadAllText(newFile) : item.AfterHash ?? "(وجود ندارد)"; _right.SelectAll(); _right.SelectionBackColor = item.Status == "Changed" ? Color.FromArgb(255, 245, 200) : Color.FromArgb(220, 252, 231); _right.DeselectAll();
    }
    private static bool IsText(string path) => Path.GetExtension(path).ToLowerInvariant() is ".html" or ".htm" or ".css" or ".js" or ".json" or ".txt" or ".xml";
}
