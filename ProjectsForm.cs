using System.Diagnostics;
using CopyWeb.Services;

namespace CopyWeb;

public sealed class ProjectsForm : Form
{
    private readonly DataGridView _grid = new();
    private readonly Label _empty = UiTheme.Label("هنوز پروژه‌ای ذخیره نشده است.", 10, color: UiTheme.Muted);
    private readonly List<ProjectEntry> _entries = [];

    public string? SelectedProjectFile { get; private set; }

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
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "site", HeaderText = "سایت", DataPropertyName = nameof(ProjectEntry.Site), Width = 280 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "date", HeaderText = "آخرین ذخیره", DataPropertyName = nameof(ProjectEntry.Date), Width = 150 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "links", HeaderText = "تعداد لینک", DataPropertyName = nameof(ProjectEntry.LinkCount), Width = 100 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "storage", HeaderText = "پوشه ذخیره‌سازی", DataPropertyName = nameof(ProjectEntry.StoragePath), Width = 300 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "size", HeaderText = "حجم", DataPropertyName = nameof(ProjectEntry.SizeText), Width = 100 });
        _grid.Columns.Add(new DataGridViewButtonColumn { Name = "delete", HeaderText = "", Text = "X", UseColumnTextForButtonValue = true, Width = 48, FlatStyle = FlatStyle.Flat });
        _grid.CellContentClick += (_, e) => { if (e.RowIndex >= 0 && _grid.Columns[e.ColumnIndex].Name == "delete") DeleteProject(_grid.Rows[e.RowIndex].DataBoundItem as ProjectEntry); };
        _grid.CellDoubleClick += (_, _) => ViewLinks();
        card.Controls.Add(_grid);
        _empty.Dock = DockStyle.Fill;
        _empty.TextAlign = ContentAlignment.MiddleCenter;
        card.Controls.Add(_empty);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.Transparent
        };
        var refresh = UiTheme.Button("به‌روزرسانی", Color.White);
        refresh.Tag = "secondary-button";
        refresh.Width = 120;
        refresh.Click += async (_, _) => await LoadProjectsAsync();
        var view = UiTheme.Button("مشاهده لینک‌ها", Color.White);
        view.Tag = "secondary-button";
        view.Width = 135;
        view.Click += (_, _) => ViewLinks();
        var folder = UiTheme.Button("بازکردن پوشه", Color.White);
        folder.Tag = "secondary-button";
        folder.Width = 130;
        folder.Click += (_, _) => OpenFolder();
        var resume = UiTheme.Button("ادامه دانلود", UiTheme.Accent);
        resume.Tag = "accent-button";
        resume.Width = 130;
        resume.Click += (_, _) => ResumeProject();
        var backup = UiTheme.Button("پشتیبان‌گیری", Color.FromArgb(118, 137, 157));
        backup.Width = 125;
        backup.Click += async (_, _) => await BackupProjectAsync();
        var restore = UiTheme.Button("بازیابی پروژه", Color.FromArgb(118, 137, 157));
        restore.Width = 125;
        restore.Click += async (_, _) => await RestoreProjectAsync();
        var copy = UiTheme.Button("کپی پروژه", Color.FromArgb(118, 137, 157));
        copy.Width = 110;
        copy.Click += async (_, _) => await CopyProjectAsync();
        var rename = UiTheme.Button("تغییر نام", Color.FromArgb(118, 137, 157));
        rename.Width = 105;
        rename.Click += async (_, _) => await RenameProjectAsync();
        var schedule = UiTheme.Button("زمان‌بندی", Color.FromArgb(118, 137, 157));
        schedule.Width = 105;
        schedule.Click += (_, _) => ScheduleProject();
        var close = UiTheme.Button("بستن", UiTheme.Primary);
        close.Width = 95;
        close.Click += (_, _) => Close();
        buttons.Controls.AddRange([close, restore, backup, copy, rename, schedule, resume, folder, view, refresh]);

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
                entries.Add(new ProjectEntry(
                    project.RootUrl,
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

    private sealed record ProjectEntry(string Site, string Date, int LinkCount, string StoragePath, string FileName, string SizeText);

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
