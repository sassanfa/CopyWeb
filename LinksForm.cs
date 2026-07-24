using CopyWeb.Models;
using CopyWeb.Services;
using System.ComponentModel;
using LinkState = CopyWeb.Models.LinkState;

namespace CopyWeb;

public sealed class LinksForm : Form
{
    private readonly Uri _root;
    private BindingList<DownloadItem> _items;
    private BindingList<DisplayRow> _displayRows = [];
    private readonly HashSet<string> _expanded = new(StringComparer.OrdinalIgnoreCase);
    private readonly DataGridView _grid = new();
    private readonly TextBox _search = new();
    private readonly TextBox _domainFilter = new();
    private readonly TextBox _minSize = new();
    private readonly TextBox _maxSize = new();
    private readonly ComboBox _stateFilter = new();
    private readonly ComboBox _kindFilter = new();
    private readonly Label _count = UiTheme.Label(string.Empty, 9, color: UiTheme.Muted);

    public IReadOnlyList<DownloadItem> Items => _items.ToList();

    public LinksForm(Uri root, IEnumerable<DownloadItem> links)
    {
        _root = root;
        _items = new BindingList<DownloadItem>(links.ToList());
        Text = "مدیریت لینک‌های سایت";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1180, 740);
        MinimumSize = new Size(900, 560);
        Font = UiTheme.NormalFont;
        RightToLeft = RightToLeft.Yes;
        BackColor = UiTheme.Background;

        BuildUi();
        UiTheme.StyleDialog(this);
        RebuildRows();
        Localization.Apply(this, AppSettingsStore.Load().Language);
    }

    private void BuildUi()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 154, BackColor = UiTheme.Surface, Padding = new Padding(24, 16, 24, 12) };
        var title = UiTheme.Label("لینک‌ها و منابع مرتبط پیدا شده", 16, FontStyle.Bold);
        title.Location = new Point(24, 14);
        _count.Location = new Point(26, 48);
        _search.PlaceholderText = "جست‌وجو در آدرس، عنوان یا منبع...";
        _search.Width = 380;
        _search.Height = 36;
        _search.Location = new Point(24, 70);
        _search.TextChanged += (_, _) => RebuildRows();
        _stateFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        _stateFilter.Items.AddRange(["همه وضعیت‌ها", "در انتظار", "موفق", "خطا", "رد شده"]);
        _stateFilter.SelectedIndex = 0;
        _stateFilter.Width = 145;
        _stateFilter.Location = new Point(420, 70);
        _stateFilter.SelectedIndexChanged += (_, _) => RebuildRows();
        _kindFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        _kindFilter.Items.AddRange(["همه انواع", "تصویر", "CSS", "JavaScript", "فونت", "رسانه", "فایل"]);
        _kindFilter.SelectedIndex = 0; _kindFilter.Width = 120; _kindFilter.Location = new Point(575, 70); _kindFilter.SelectedIndexChanged += (_, _) => RebuildRows();
        _domainFilter.PlaceholderText = "دامنه..."; _domainFilter.Width = 170; _domainFilter.Height = 30; _domainFilter.Location = new Point(24, 112); _domainFilter.TextChanged += (_, _) => RebuildRows();
        _minSize.PlaceholderText = "حداقل KB"; _minSize.Width = 100; _minSize.Height = 30; _minSize.Location = new Point(205, 112); _minSize.TextChanged += (_, _) => RebuildRows();
        _maxSize.PlaceholderText = "حداکثر KB"; _maxSize.Width = 100; _maxSize.Height = 30; _maxSize.Location = new Point(312, 112); _maxSize.TextChanged += (_, _) => RebuildRows();
        header.Controls.AddRange([title, _count, _search, _stateFilter, _kindFilter, _domainFilter, _minSize, _maxSize]);

        _grid.Dock = DockStyle.Fill;
        _grid.BackgroundColor = UiTheme.Surface;
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
        _grid.ColumnHeadersDefaultCellStyle.BackColor = ControlPaint.Light(UiTheme.Surface, 0.08F);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = UiTheme.Text;
        _grid.DefaultCellStyle.BackColor = UiTheme.Surface;
        _grid.DefaultCellStyle.ForeColor = UiTheme.Text;
        _grid.DefaultCellStyle.SelectionBackColor = UiTheme.Action;
        _grid.DefaultCellStyle.SelectionForeColor = Color.White;
        var expand = new DataGridViewButtonColumn
        {
            DataPropertyName = nameof(DisplayRow.Toggle), HeaderText = string.Empty, Width = 42,
            FlatStyle = FlatStyle.Flat, UseColumnTextForButtonValue = false
        };
        _grid.Columns.Add(expand);
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(DisplayRow.Selected), HeaderText = "انتخاب", Width = 65 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DisplayRow.Title), HeaderText = "عنوان / نوع منبع", Width = 230 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DisplayRow.Url), HeaderText = "آدرس", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DisplayRow.Depth), HeaderText = "عمق", Width = 55 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DisplayRow.State), HeaderText = "وضعیت", Width = 95 });
        _grid.CellContentClick += GridCellContentClick;
        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;
            if (_grid.Rows[e.RowIndex].DataBoundItem is DisplayRow row && row.IsResource)
                e.CellStyle.BackColor = ControlPaint.Light(UiTheme.Surface, 0.035F);
        };

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 66, BackColor = UiTheme.Surface, Padding = new Padding(18, 12, 18, 8),
            FlowDirection = FlowDirection.RightToLeft, WrapContents = false
        };
        var download = UiTheme.Button("دانلود موارد انتخاب‌شده"); download.Width = 220;
        download.Click += (_, _) => { _grid.EndEdit(); DialogResult = DialogResult.OK; Close(); };
        var all = UiTheme.Button("انتخاب همه", ControlPaint.Light(UiTheme.Surface, 0.08F)); all.Width = 115; all.Click += (_, _) => SetAll(true);
        var none = UiTheme.Button("لغو انتخاب", ControlPaint.Light(UiTheme.Surface, 0.08F)); none.Width = 115; none.Click += (_, _) => SetAll(false);
        var remove = UiTheme.Button("حذف ردیف", UiTheme.Danger); remove.Width = 110; remove.Click += RemoveRows;
        var removeFailed = UiTheme.Button("حذف ناموفق‌ها", UiTheme.Danger); removeFailed.Width = 130; removeFailed.Click += (_, _) => RemoveFailed();
        var save = UiTheme.Button("ذخیره لیست", UiTheme.Accent); save.Width = 115; save.Click += SaveClick;
        var load = UiTheme.Button("بارگذاری لیست", ControlPaint.Light(UiTheme.Surface, 0.08F)); load.Width = 130; load.Click += LoadClick;
        bottom.Controls.AddRange([download, all, none, removeFailed, remove, save, load]);

        Controls.Add(_grid);
        Controls.Add(bottom);
        Controls.Add(header);
    }

    private void RebuildRows()
    {
        _grid.EndEdit();
        var rows = new List<DisplayRow>();
        foreach (var page in _items.Where(IsPageItem))
        {
            var matchingResources = page.Resources.Where(MatchesResourceFilter).ToList();
            var pageMatches = MatchesPageFilter(page);
            if (!pageMatches && matchingResources.Count == 0) continue;
            rows.Add(new DisplayRow(page, null, _expanded.Contains(page.Url)));
            if (_expanded.Contains(page.Url))
            {
                var resources = pageMatches ? page.Resources.Where(MatchesResourceFilter).ToList() : matchingResources;
                rows.AddRange(resources.Select(resource => new DisplayRow(page, resource, false)));
            }
        }
        _displayRows = new BindingList<DisplayRow>(rows);
        _grid.DataSource = _displayRows;
        var pages = _items.Where(IsPageItem).ToList();
        var selectedPages = pages.Count(x => x.IsSelected);
        var selectedResources = pages.Sum(x => x.Resources.Count(resource => resource.IsSelected));
        _count.Text = $"{pages.Count} صفحه — {selectedPages} صفحه انتخاب | {selectedResources} منبع انتخاب";
    }

    private void GridCellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != 0) return;
        if (_grid.Rows[e.RowIndex].DataBoundItem is not DisplayRow row || row.IsResource) return;
        if (!_expanded.Add(row.Page.Url)) _expanded.Remove(row.Page.Url);
        RebuildRows();
    }

    private void SetAll(bool value)
    {
        _grid.EndEdit();
        foreach (var page in _items.Where(IsPageItem))
        {
            page.IsSelected = value;
            foreach (var resource in page.Resources) resource.IsSelected = value;
        }
        RebuildRows();
    }

    private void RemoveRows(object? sender, EventArgs e)
    {
        _grid.EndEdit();
        var selectedRows = _grid.SelectedRows.Cast<DataGridViewRow>().Select(row => row.DataBoundItem).OfType<DisplayRow>().ToList();
        var pagesToRemove = selectedRows.Where(row => !row.IsResource).Select(row => row.Page).Distinct().ToList();
        foreach (var page in pagesToRemove) _items.Remove(page);
        foreach (var group in selectedRows.Where(row => row.IsResource && !pagesToRemove.Contains(row.Page)).GroupBy(row => row.Page))
        {
            foreach (var row in group) group.Key.Resources.Remove(row.Resource!);
        }
        RebuildRows();
    }

    private bool MatchesPageFilter(DownloadItem item)
    {
        var term = _search.Text.Trim();
        var stateMatches = MatchesState(item.State);
        return stateMatches && MatchesDomain(item.Url) && (term.Length == 0 || item.Url.Contains(term, StringComparison.OrdinalIgnoreCase) || item.Title.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private bool MatchesResourceFilter(ResourceItem resource)
    {
        var term = _search.Text.Trim();
        var kindMatches = _kindFilter.SelectedIndex <= 0 || KindLabel(resource.Kind).Equals(_kindFilter.SelectedItem?.ToString(), StringComparison.OrdinalIgnoreCase);
        var minKb = ParseSize(_minSize.Text); var maxKb = ParseSize(_maxSize.Text); var sizeMatches = (!minKb.HasValue || resource.SizeBytes >= minKb.Value * 1024) && (!maxKb.HasValue || resource.SizeBytes <= maxKb.Value * 1024);
        return MatchesState(resource.State) && kindMatches && sizeMatches && MatchesDomain(resource.Url) && (term.Length == 0 || resource.Url.Contains(term, StringComparison.OrdinalIgnoreCase) || KindLabel(resource.Kind).Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private bool MatchesDomain(string url)
    {
        var domain = _domainFilter.Text.Trim();
        return domain.Length == 0 || (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Host.Contains(domain, StringComparison.OrdinalIgnoreCase));
    }

    private static long? ParseSize(string value) => long.TryParse(value.Trim(), out var size) && size >= 0 ? size : null;

    private bool MatchesState(LinkState state) => _stateFilter.SelectedIndex switch
    {
        1 => state is LinkState.Pending or LinkState.Selected or LinkState.Downloading,
        2 => state == LinkState.Downloaded,
        3 => state == LinkState.Failed,
        4 => state == LinkState.Skipped,
        _ => true
    };

    private static bool IsPageItem(DownloadItem item) =>
        item.State != LinkState.Skipped && UrlTools.IsLikelyPageUrl(item.Uri);

    private static string KindLabel(ResourceKind kind) => kind switch
    {
        ResourceKind.Image => "تصویر",
        ResourceKind.Stylesheet => "CSS",
        ResourceKind.Script => "JavaScript",
        ResourceKind.Font => "فونت",
        ResourceKind.Media => "رسانه",
        _ => "فایل"
    };

    private void RemoveFailed()
    {
        foreach (var item in _items.Where(x => x.State == LinkState.Failed).ToList()) _items.Remove(item);
        foreach (var item in _items) item.Resources.RemoveAll(resource => resource.State == LinkState.Failed);
        RebuildRows();
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
            _expanded.Clear();
            _search.Clear();
            RebuildRows();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "خطای بارگذاری", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private sealed class DisplayRow
    {
        private readonly ResourceItem? _resource;
        public DownloadItem Page { get; }
        public ResourceItem? Resource => _resource;
        public bool IsResource => _resource is not null;
        public string Toggle { get; }
        public bool Selected
        {
            get => _resource?.IsSelected ?? Page.IsSelected;
            set
            {
                if (_resource is not null) _resource.IsSelected = value;
                else Page.IsSelected = value;
            }
        }
        public string Title => _resource is not null ? $"↳ {KindLabel(_resource.Kind)}" : (string.IsNullOrWhiteSpace(Page.Title) ? "صفحه بدون عنوان" : Page.Title);
        public string Url => _resource?.Url ?? Page.Url;
        public string Depth => _resource is null ? Page.Depth.ToString() : string.Empty;
        public string State => StateLabel(_resource?.State ?? Page.State);

        public DisplayRow(DownloadItem page, ResourceItem? resource, bool expanded)
        {
            Page = page;
            _resource = resource;
            Toggle = resource is not null || page.Resources.Count == 0 ? string.Empty : (expanded ? "−" : "+");
        }

        private static string KindLabel(ResourceKind kind) => kind switch
        {
            ResourceKind.Image => "تصویر",
            ResourceKind.Stylesheet => "CSS",
            ResourceKind.Script => "JavaScript",
            ResourceKind.Font => "فونت",
            ResourceKind.Media => "رسانه",
            _ => "فایل"
        };

        private static string StateLabel(LinkState state) => state switch
        {
            LinkState.Downloaded => "موفق",
            LinkState.Failed => "خطا",
            LinkState.Skipped => "رد شده",
            LinkState.Downloading => "در حال دانلود",
            _ => "در انتظار"
        };
    }
}
