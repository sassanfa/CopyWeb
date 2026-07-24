using System.Diagnostics;
using CopyWeb.Services;

namespace CopyWeb;

public sealed class ProjectsForm : Form
{
    private readonly DataGridView _grid = new();
    private readonly Label _empty = UiTheme.Label("هنوز پروژه‌ای ذخیره نشده است.", 10, color: UiTheme.Muted);
    private readonly List<ProjectEntry> _entries = [];

    public string? SelectedProjectFile { get; private set; }
    public string? LoadedProjectFile { get; private set; }

    public ProjectsForm()
    {
        Text = "پروژه‌های ذخیره‌شده";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(980, 620);
        MinimumSize = new Size(780, 480);
        Font = UiTheme.NormalFont;
        RightToLeft = RightToLeft.Yes;
        BackColor = UiTheme.Background;
        BuildUi();
        UiTheme.StyleDialog(this);
        Localization.Apply(this, AppSettingsStore.Load().Language);
        Shown += async (_, _) => await LoadProjectsAsync();
    }

    private void BuildUi()
    {
        var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18), BackColor = UiTheme.Background };
        var title = UiTheme.Label("پروژه‌های ذخیره‌شده", 18, FontStyle.Bold);
        title.Dock = DockStyle.Top;
        title.Height = 42;

        var card = UiTheme.Card();
        card.Dock = DockStyle.Fill;
        _grid.Dock = DockStyle.Fill;
        _grid.AutoGenerateColumns = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.ReadOnly = true;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.BackgroundColor = UiTheme.Surface;
        _grid.BorderStyle = BorderStyle.None;
        _grid.RowHeadersVisible = false;
        _grid.RightToLeft = RightToLeft.Yes;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.ScrollBars = ScrollBars.Vertical;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "site", HeaderText = "سایت", DataPropertyName = nameof(ProjectEntry.Site), FillWeight = 25, MinimumWidth = 150 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "type", HeaderText = "نوع", DataPropertyName = nameof(ProjectEntry.Type), FillWeight = 9, MinimumWidth = 72 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "date", HeaderText = "آخرین ذخیره", DataPropertyName = nameof(ProjectEntry.Date), FillWeight = 15, MinimumWidth = 120 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "links", HeaderText = "تعداد لینک", DataPropertyName = nameof(ProjectEntry.LinkCount), FillWeight = 9, MinimumWidth = 75 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "storage", HeaderText = "پوشه ذخیره‌سازی", DataPropertyName = nameof(ProjectEntry.StoragePath), FillWeight = 36, MinimumWidth = 210 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "size", HeaderText = "حجم", DataPropertyName = nameof(ProjectEntry.SizeText), FillWeight = 10, MinimumWidth = 75 });
        _grid.Columns.Add(new DataGridViewButtonColumn { Name = "delete", HeaderText = "", Text = "X", UseColumnTextForButtonValue = true, Width = 48, MinimumWidth = 48, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, FlatStyle = FlatStyle.Flat });
        _grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex >= 0 && _grid.Columns[e.ColumnIndex].Name == "type" &&
                string.Equals(e.Value?.ToString(), "● زنده", StringComparison.Ordinal))
            {
                e.CellStyle.ForeColor = Color.FromArgb(52, 211, 153);
                e.CellStyle.Font = new Font(UiTheme.NormalFont, FontStyle.Bold);
            }
        };
        _grid.CellContentClick += (_, e) => { if (e.RowIndex >= 0 && _grid.Columns[e.ColumnIndex].Name == "delete") DeleteProject(_grid.Rows[e.RowIndex].DataBoundItem as ProjectEntry); };
        _grid.CellDoubleClick += (_, _) => ViewLinks();
        card.Controls.Add(_grid);
        _empty.Dock = DockStyle.Fill;
        _empty.TextAlign = ContentAlignment.MiddleCenter;
        card.Controls.Add(_empty);

        var buttons = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 58, ColumnCount = 7, RowCount = 1, BackColor = Color.Transparent, RightToLeft = RightToLeft.Yes, Padding = new Padding(0, 6, 0, 0) };
        for (var column = 0; column < buttons.ColumnCount; column++) buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / buttons.ColumnCount));
        buttons.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var refresh = UiTheme.Button("به‌روزرسانی", Color.White);
        refresh.Tag = "secondary-button";
        refresh.Click += async (_, _) => await LoadProjectsAsync();
        var view = UiTheme.Button("مشاهده لینک‌ها", Color.White);
        view.Tag = "secondary-button";
        view.Click += (_, _) => ViewLinks();
        var folder = UiTheme.Button("بازکردن پوشه", Color.White);
        folder.Tag = "secondary-button";
        folder.Click += (_, _) => OpenFolder();
        var resume = UiTheme.Button("ادامه دانلود", UiTheme.Accent);
        resume.Tag = "accent-button";
        resume.Click += (_, _) => ResumeProject();
        var loadProject = UiTheme.Button("بارگذاری آدرس", Color.FromArgb(118, 137, 157));
        loadProject.Click += (_, _) => LoadProjectForEditing();
        var more = UiTheme.Button("ابزارها ⋯", Color.FromArgb(118, 137, 157));
        var toolsMenu = new ContextMenuStrip { RightToLeft = RightToLeft.Yes, BackColor = UiTheme.Surface, ForeColor = UiTheme.Text, ShowImageMargin = false, Font = UiTheme.NormalFont };
        void AddTool(string text, EventHandler action) { var item = toolsMenu.Items.Add(text); item.Click += action; }
        AddTool("اعتبارسنجی آرشیو", (_, _) => ValidateProject());
        AddTool("جست‌وجوی متن", (_, _) => SearchProject());
        AddTool("چت با آرشیو", (_, _) => ChatProject());
        AddTool("نقشه سایت", (_, _) => GraphProject());
        AddTool("پیش‌نمایش روی localhost", (_, _) => PreviewProject());
        AddTool("Snapshot / Diff", (_, _) => OpenSnapshots());
        AddTool("داشبورد پروژه", (_, _) => OpenDashboard());
        AddTool("حالت Watch", (_, _) => OpenWatch());
        AddTool("انتشار", (_, _) => OpenPublish());
        AddTool("زمان‌بندی", (_, _) => ScheduleProject());
        AddTool("تغییر نام", async (_, _) => await RenameProjectAsync());
        AddTool("کپی پروژه", async (_, _) => await CopyProjectAsync());
        AddTool("پشتیبان‌گیری", async (_, _) => await BackupProjectAsync());
        AddTool("بازیابی پروژه", async (_, _) => await RestoreProjectAsync());
        AddTool("اسکرین‌شات قبل/بعد", async (_, _) => await CaptureScreenshotsAsync());
        more.Click += (_, _) => toolsMenu.Show(more, new Point(0, more.Height));
        var close = UiTheme.Button("بستن", UiTheme.Primary);
        close.Click += (_, _) => Close();
        var visibleActions = new[] { close, resume, folder, view, loadProject, refresh, more };
        for (var column = 0; column < visibleActions.Length; column++)
        {
            visibleActions[column].Dock = DockStyle.Fill;
            visibleActions[column].Margin = new Padding(4, 0, 4, 0);
            buttons.Controls.Add(visibleActions[column], column, 0);
        }

        root.Controls.Add(card);
        root.Controls.Add(buttons);
        root.Controls.Add(title);
        Controls.Add(root);
    }

    private async Task LoadProjectsAsync()
    {
        _grid.Enabled = false;
        _empty.Text = "در حال بارگذاری پروژه‌ها...";
        _empty.Visible = true;
        List<ProjectEntry> entries;
        try
        {
            entries = await Task.Run(DiscoverProjects);
        }
        catch (Exception ex)
        {
            entries = [];
            _empty.Text = $"خطا در خواندن پروژه‌ها: {ex.Message}";
        }

        _entries.Clear();
        _entries.AddRange(entries);
        _grid.DataSource = null;
        _grid.DataSource = _entries.ToList();
        _empty.Text = _entries.Count == 0 ? "هنوز پروژه‌ای ذخیره نشده است." : string.Empty;
        _empty.Visible = _entries.Count == 0;
        _grid.Enabled = true;
    }

    private static List<ProjectEntry> DiscoverProjects()
    {
        var entries = new List<ProjectEntry>();
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CopyWeb");
        var files = new HashSet<string>(ProjectStorage.GetKnownProjectFiles(), StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(root))
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
            };
            foreach (var file in Directory.EnumerateFiles(root, "links.json", options)
                         .Where(x => !x.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase)))
                files.Add(file);
        }

        foreach (var file in files.Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc))
        {
            try
            {
                var project = ProjectStorage.LoadAsync(file).GetAwaiter().GetResult();
                var live = File.Exists(Path.Combine(Path.GetDirectoryName(file) ?? string.Empty, "live-capture-manifest.json"));
                entries.Add(new ProjectEntry(
                    project.RootUrl,
                    live ? "● زنده" : "معمولی",
                    File.GetLastWriteTime(file).ToString("yyyy-MM-dd HH:mm"),
                    project.Links.Count,
                    Path.GetDirectoryName(file) ?? string.Empty,
                    file,
                    FormatBytes(DirectorySize(Path.GetDirectoryName(file) ?? string.Empty))));
            }
            catch
            {
                // Ignore incomplete checkpoint files while listing projects.
            }
        }
        return entries;
    }

    private ProjectEntry? SelectedEntry => _grid.CurrentRow?.DataBoundItem as ProjectEntry;

    private void OpenFolder()
    {
        var entry = SelectedEntry;
        if (entry is null) return;
        var folder = Path.GetDirectoryName(entry.FileName);
        if (folder is null) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
    }

    private async void ViewLinks()
    {
        var entry = SelectedEntry;
        if (entry is null) return;
        try
        {
            var project = await ProjectStorage.LoadAsync(entry.FileName);
            if (!Uri.TryCreate(project.RootUrl, UriKind.Absolute, out var root)) return;
            using var links = new LinksForm(root, project.Links);
            links.ShowDialog(this);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "خطای پروژه", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ResumeProject()
    {
        var entry = SelectedEntry;
        if (entry is null) return;
        SelectedProjectFile = entry.FileName;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void LoadProjectForEditing()
    {
        var entry = SelectedEntry;
        if (entry is null) return;
        LoadedProjectFile = entry.FileName;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void ValidateProject()
    {
        var entry = SelectedEntry;
        if (entry is null) return;
        using var form = new ArchiveValidationForm(entry.FileName);
        form.ShowDialog(this);
    }

    private void SearchProject()
    {
        var entry = SelectedEntry;
        if (entry is null) return;
        using var form = new ArchiveSearchForm(entry.StoragePath);
        form.ShowDialog(this);
    }

    private void ChatProject()
    {
        var entry = SelectedEntry;
        if (entry is null) return;
        using var form = new ArchiveChatForm(entry.StoragePath);
        form.ShowDialog(this);
    }

    private void GraphProject()
    {
        var entry = SelectedEntry;
        if (entry is null) return;
        using var form = new SiteGraphForm(entry.FileName);
        form.ShowDialog(this);
    }

    private void OpenSnapshots()
    {
        var entry = SelectedEntry;
        if (entry is null) return;
        using var form = new SnapshotDiffForm(entry.StoragePath);
        form.ShowDialog(this);
    }

    private async Task BackupProjectAsync()
    {
        var entry = SelectedEntry;
        if (entry is null) return;
        using var dialog = new SaveFileDialog { Filter = "CopyWeb backup|*.copyweb.zip", FileName = $"{UrlTools.CleanName(entry.Site, "copyweb-project")}.copyweb.zip" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            await ProjectArchiveService.CreateBackupAsync(entry.FileName, dialog.FileName);
            MessageBox.Show(this, "پشتیبان پروژه با موفقیت ساخته شد.", "پشتیبان‌گیری", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "خطای پشتیبان‌گیری", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task RestoreProjectAsync()
    {
        using var open = new OpenFileDialog { Filter = "CopyWeb backup|*.copyweb.zip;*.zip" };
        if (open.ShowDialog(this) != DialogResult.OK) return;
        using var folder = new FolderBrowserDialog { Description = "محل بازیابی پروژه را انتخاب کنید" };
        if (folder.ShowDialog(this) != DialogResult.OK) return;
        var destination = Path.Combine(folder.SelectedPath, Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(open.FileName)));
        try
        {
            await Task.Run(() => ProjectArchiveService.RestoreBackup(open.FileName, destination));
            await LoadProjectsAsync();
            MessageBox.Show(this, $"پروژه در مسیر زیر بازیابی شد:\n{destination}", "بازیابی پروژه", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "خطای بازیابی", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task CopyProjectAsync()
    {
        var entry = SelectedEntry;
        if (entry is null) return;
        using var dialog = new FolderBrowserDialog { Description = "محل کپی پروژه را انتخاب کنید" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var source = Path.GetDirectoryName(entry.FileName);
        if (string.IsNullOrWhiteSpace(source) || !Directory.Exists(source)) return;
        var target = Path.Combine(dialog.SelectedPath, Path.GetFileName(source) + "-copy");
        try
        {
            await Task.Run(() => CopyDirectory(source, target));
            await LoadProjectsAsync();
            MessageBox.Show(this, $"پروژه در مسیر زیر کپی شد:\n{target}", "کپی پروژه", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "خطای کپی پروژه", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task RenameProjectAsync()
    {
        var entry = SelectedEntry;
        if (entry is null) return;
        var oldFolder = Path.GetDirectoryName(entry.FileName);
        if (string.IsNullOrWhiteSpace(oldFolder) || !Directory.Exists(oldFolder)) return;
        using var dialog = new InputDialog("نام جدید پروژه", Path.GetFileName(oldFolder));
        if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.Value)) return;
        var parent = Directory.GetParent(oldFolder)?.FullName;
        if (string.IsNullOrWhiteSpace(parent)) return;
        var newFolder = Path.Combine(parent, UrlTools.CleanName(dialog.Value.Trim(), "copyweb-project"));
        if (Directory.Exists(newFolder)) { MessageBox.Show(this, "این نام قبلاً وجود دارد.", "تغییر نام", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        try
        {
            var project = await ProjectStorage.LoadAsync(entry.FileName);
            Directory.Move(oldFolder, newFolder);
            await ProjectStorage.SaveAsync(Path.Combine(newFolder, "links.json"), new Uri(project.RootUrl), project.Links, project.Proxy);
            ProjectStorage.Forget(entry.FileName);
            await LoadProjectsAsync();
            MessageBox.Show(this, "نام پروژه تغییر کرد.", "تغییر نام", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "خطای تغییر نام", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void ScheduleProject()
    {
        var entry = SelectedEntry;
        if (entry is null || !Uri.TryCreate(entry.Site, UriKind.Absolute, out var root)) return;
        using var form = new ScheduleForm(root, entry.StoragePath);
        form.ShowDialog(this);
    }

    private void PreviewProject()
    {
        var entry = SelectedEntry;
        if (entry is null) return;
        using var form = new OfflinePreviewForm(entry.StoragePath);
        form.ShowDialog(this);
    }

    private void OpenDashboard()
    {
        var entry = SelectedEntry;
        if (entry is null) return;
        using var form = new DashboardForm(entry.FileName);
        form.ShowDialog(this);
    }

    private void OpenWatch()
    {
        var entry = SelectedEntry;
        if (entry is null) return;
        using var form = new WatchForm(entry.FileName);
        form.ShowDialog(this);
    }

    private void OpenPublish()
    {
        var entry = SelectedEntry;
        if (entry is null) return;
        using var form = new PublishForm(entry.StoragePath);
        form.ShowDialog(this);
    }

    private async Task CaptureScreenshotsAsync()
    {
        var entry = SelectedEntry;
        if (entry is null || !Uri.TryCreate(entry.Site, UriKind.Absolute, out var remote)) return;
        var folder = Path.Combine(entry.StoragePath, "screenshots");
        Directory.CreateDirectory(folder);
        try
        {
            _empty.Text = "در حال گرفتن اسکرین‌شات قبل و بعد..."; _empty.Visible = true;
            using (var browser = new BrowserSnapshotForm(remote)) await browser.CaptureScreenshotAsync(Path.Combine(folder, "before.png"), CancellationToken.None);
            using (var server = new OfflinePreviewServer(entry.StoragePath))
            {
                server.Start();
                using var local = new BrowserSnapshotForm(new Uri(server.BaseUri, "index.html"));
                await local.CaptureScreenshotAsync(Path.Combine(folder, "after.png"), CancellationToken.None);
            }
            MessageBox.Show(this, $"اسکرین‌شات‌ها در پوشه زیر ذخیره شدند:\n{folder}", "اسکرین‌شات", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, $"گرفتن اسکرین‌شات انجام نشد:\n{ex.Message}", "اسکرین‌شات", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        finally { _empty.Visible = _entries.Count == 0; }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        foreach (var directory in Directory.EnumerateDirectories(source)) CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }

    private static long DirectorySize(string path)
    {
        try { return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length); } catch { return 0; }
    }

    private static string FormatBytes(long bytes) => bytes switch { < 1024 => $"{bytes} B", < 1024 * 1024 => $"{bytes / 1024d:0.0} KB", < 1024 * 1024 * 1024 => $"{bytes / 1024d / 1024d:0.0} MB", _ => $"{bytes / 1024d / 1024d / 1024d:0.0} GB" };

    private void DeleteProject(ProjectEntry? entry)
    {
        if (entry is null) return;
        var folder = Path.GetDirectoryName(entry.FileName);
        if (string.IsNullOrWhiteSpace(folder) || !File.Exists(entry.FileName)) return;
        var answer = MessageBox.Show(this,
            $"پروژه و تمام فایل‌های پوشه زیر حذف شود؟\n{folder}\n\nاین کار قابل بازگشت نیست.",
            "حذف پروژه", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (answer != DialogResult.Yes) return;
        try
        {
            var defaultProjectsRoot = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CopyWeb"));
            var normalizedFolder = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (Directory.Exists(folder) && !normalizedFolder.Equals(defaultProjectsRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                Directory.Delete(folder, true);
            else if (File.Exists(entry.FileName))
                File.Delete(entry.FileName);
            ProjectStorage.Forget(entry.FileName);
            _entries.Remove(entry);
            _grid.DataSource = null;
            _grid.DataSource = _entries.ToList();
            _empty.Visible = _entries.Count == 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"حذف پروژه انجام نشد:\n{ex.Message}", "خطای حذف", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private sealed record ProjectEntry(string Site, string Type, string Date, int LinkCount, string StoragePath, string FileName, string SizeText);

    private sealed class InputDialog : Form
    {
        private readonly TextBox _input = new();
        public string Value => _input.Text;
        public InputDialog(string title, string value)
        {
            Text = title; StartPosition = FormStartPosition.CenterParent; Size = new Size(420, 150); Font = UiTheme.NormalFont; RightToLeft = RightToLeft.Yes; BackColor = UiTheme.Background;
            _input.Text = value; _input.Location = new Point(18, 18); _input.Width = 360; _input.RightToLeft = RightToLeft.No;
            var ok = UiTheme.Button("تأیید", UiTheme.Primary); ok.Location = new Point(220, 62); ok.Width = 85; ok.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
            var cancel = UiTheme.Button("انصراف", Color.White); cancel.Tag = "secondary-button"; cancel.Location = new Point(310, 62); cancel.Width = 85; cancel.Click += (_, _) => Close();
            Controls.AddRange([_input, ok, cancel]);
        }
    }
}
