using CopyWeb.Models;
using CopyWeb.Services;
using System.ComponentModel;

namespace CopyWeb;

public sealed class LinksForm : Form
{
    private readonly Uri _root;
    private BindingList<DownloadItem> _items;
    private readonly DataGridView _grid = new();
    private readonly TextBox _search = new();
    private readonly Label _count = UiTheme.Label(string.Empty, 9, color: UiTheme.Muted);

    public IReadOnlyList<DownloadItem> Items => _items.ToList();

    public LinksForm(Uri root, IEnumerable<DownloadItem> links)
    {
        _root = root;
        _items = new BindingList<DownloadItem>(links.ToList());
        Text = "مدیریت لینک‌های سایت";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1120, 720);
        MinimumSize = new Size(850, 550);
        Font = UiTheme.NormalFont;
        RightToLeft = RightToLeft.Yes;
        BackColor = UiTheme.Background;

        BuildUi();
        BindGrid(_items);
    }

    private void BuildUi()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 112, BackColor = Color.White, Padding = new Padding(24, 16, 24, 12) };
        var title = UiTheme.Label("لینک‌های مرتبط پیدا شده", 16, FontStyle.Bold);
        title.Location = new Point(24, 14);
        _count.Location = new Point(26, 48);
        _search.PlaceholderText = "جست‌وجو در آدرس یا عنوان...";
        _search.Width = 380;
        _search.Height = 36;
        _search.Location = new Point(24, 70);
        _search.TextChanged += (_, _) => ApplyFilter();
        header.Controls.AddRange([title, _count, _search]);

        _grid.Dock = DockStyle.Fill;
        _grid.BackgroundColor = Color.White;
        _grid.BorderStyle = BorderStyle.None;
        _grid.RowHeadersVisible = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AutoGenerateColumns = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = true;
        _grid.RowTemplate.Height = 34;
        _grid.ColumnHeadersHeight = 42;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = UiTheme.Text;
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
        _grid.DefaultCellStyle.SelectionForeColor = UiTheme.Text;
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(DownloadItem.IsSelected), HeaderText = "انتخاب", Width = 65 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DownloadItem.Title), HeaderText = "عنوان", Width = 210 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DownloadItem.Url), HeaderText = "آدرس", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DownloadItem.Depth), HeaderText = "عمق", Width = 55 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DownloadItem.State), HeaderText = "وضعیت", Width = 95 });

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 66, BackColor = Color.White, Padding = new Padding(18, 12, 18, 8),
            FlowDirection = FlowDirection.RightToLeft, WrapContents = false
        };
        var download = UiTheme.Button("دانلود موارد انتخاب‌شده"); download.Width = 220;
        download.Click += (_, _) => { _grid.EndEdit(); DialogResult = DialogResult.OK; Close(); };
        var all = UiTheme.Button("انتخاب همه", Color.FromArgb(71, 85, 105)); all.Width = 115; all.Click += (_, _) => SetAll(true);
        var none = UiTheme.Button("لغو انتخاب", Color.FromArgb(100, 116, 139)); none.Width = 115; none.Click += (_, _) => SetAll(false);
        var remove = UiTheme.Button("حذف ردیف", UiTheme.Danger); remove.Width = 110; remove.Click += RemoveRows;
        var save = UiTheme.Button("ذخیره لیست", Color.FromArgb(5, 150, 105)); save.Width = 115; save.Click += SaveClick;
        var load = UiTheme.Button("بارگذاری لیست", Color.FromArgb(14, 116, 144)); load.Width = 130; load.Click += LoadClick;
        bottom.Controls.AddRange([download, all, none, remove, save, load]);

        Controls.Add(_grid);
        Controls.Add(bottom);
        Controls.Add(header);
    }

    private void BindGrid(BindingList<DownloadItem> data)
    {
        _grid.DataSource = data;
        _count.Text = $"{_items.Count} لینک — {_items.Count(x => x.IsSelected)} مورد انتخاب شده";
    }

    private void ApplyFilter()
    {
        _grid.EndEdit();
        var term = _search.Text.Trim();
        if (term.Length == 0) BindGrid(_items);
        else BindGrid(new BindingList<DownloadItem>(_items.Where(x =>
            x.Url.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            x.Title.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList()));
    }

    private void SetAll(bool value)
    {
        _grid.EndEdit();
        foreach (var item in _items) item.IsSelected = value;
        _grid.Refresh();
        BindGrid((BindingList<DownloadItem>)_grid.DataSource!);
    }

    private void RemoveRows(object? sender, EventArgs e)
    {
        var selected = _grid.SelectedRows.Cast<DataGridViewRow>().Select(x => x.DataBoundItem).OfType<DownloadItem>().ToList();
        foreach (var item in selected) _items.Remove(item);
        ApplyFilter();
    }

    private async void SaveClick(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog { Filter = "CopyWeb link list|*.json", FileName = "links.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { await ProjectStorage.SaveAsync(dialog.FileName, _root, _items); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "خطای ذخیره", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async void LoadClick(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog { Filter = "CopyWeb link list|*.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var project = await ProjectStorage.LoadAsync(dialog.FileName);
            _items = new BindingList<DownloadItem>(project.Links);
            _search.Clear();
            BindGrid(_items);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "خطای بارگذاری", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}
