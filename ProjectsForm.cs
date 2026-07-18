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
        var close = UiTheme.Button("بستن", UiTheme.Primary);
        close.Width = 95;
        close.Click += (_, _) => Close();
        buttons.Controls.AddRange([close, resume, folder, view, refresh]);

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
                    file));
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

    private sealed record ProjectEntry(string Site, string Date, int LinkCount, string StoragePath, string FileName);
}
