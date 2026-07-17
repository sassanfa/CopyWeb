using System.Diagnostics;

namespace CopyWeb;

public sealed class ReportsForm : Form
{
    private readonly ListBox _reports = new();
    private readonly RichTextBox _viewer = new();
    private readonly Label _empty = UiTheme.Label("هنوز گزارشی ذخیره نشده است.", 10, color: UiTheme.Muted);
    private readonly List<string> _files = [];

    public ReportsForm()
    {
        Text = "گزارش‌های فعالیت";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(950, 620);
        MinimumSize = new Size(760, 480);
        Font = UiTheme.NormalFont;
        RightToLeft = RightToLeft.Yes;
        BackColor = UiTheme.Background;
        BuildUi();
        LoadReports();
    }

    private void BuildUi()
    {
        var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18), BackColor = UiTheme.Background };
        var header = UiTheme.Label("گزارش‌های ذخیره‌شده", 18, FontStyle.Bold);
        header.Dock = DockStyle.Top;
        header.Height = 42;

        var left = UiTheme.Card();
        left.Dock = DockStyle.Left;
        left.Width = 270;
        _reports.Dock = DockStyle.Fill;
        _reports.BorderStyle = BorderStyle.None;
        _reports.BackColor = UiTheme.Surface;
        _reports.Font = UiTheme.NormalFont;
        _reports.SelectedIndexChanged += (_, _) => ShowSelectedReport();
        left.Controls.Add(_reports);

        var right = UiTheme.Card();
        right.Dock = DockStyle.Fill;
        _viewer.Dock = DockStyle.Fill;
        _viewer.ReadOnly = true;
        _viewer.BackColor = UiTheme.Surface;
        _viewer.BorderStyle = BorderStyle.None;
        _viewer.Font = new Font("Consolas", 10);
        right.Controls.Add(_viewer);
        _empty.Dock = DockStyle.Fill;
        _empty.TextAlign = ContentAlignment.MiddleCenter;
        right.Controls.Add(_empty);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        var refresh = UiTheme.Button("به‌روزرسانی", Color.White);
        refresh.Tag = "secondary-button";
        refresh.Width = 120;
        refresh.Click += (_, _) => LoadReports();
        var openFolder = UiTheme.Button("بازکردن پوشه", Color.White);
        openFolder.Tag = "secondary-button";
        openFolder.Width = 130;
        openFolder.Click += (_, _) => OpenSelectedFolder();
        var close = UiTheme.Button("بستن", UiTheme.Primary);
        close.Width = 100;
        close.Click += (_, _) => Close();
        buttons.Controls.AddRange([close, openFolder, refresh]);

        root.Controls.Add(right);
        root.Controls.Add(left);
        root.Controls.Add(buttons);
        root.Controls.Add(header);
        Controls.Add(root);
    }

    private void LoadReports()
    {
        _reports.Items.Clear();
        _files.Clear();
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CopyWeb");
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(root))
        {
            foreach (var file in Directory.EnumerateFiles(root, "activity.log", SearchOption.AllDirectories))
                folders.Add(file);
        }
        foreach (var projectFile in Services.ProjectStorage.GetKnownProjectFiles())
        {
            var folder = Path.GetDirectoryName(projectFile);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                var log = Path.Combine(folder, "activity.log");
                if (File.Exists(log)) folders.Add(log);
            }
        }
        foreach (var file in folders.Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc))
        {
            _files.Add(file);
            _reports.Items.Add(Path.GetFileName(Path.GetDirectoryName(file) ?? file));
        }

        _empty.Visible = _files.Count == 0;
        if (_files.Count > 0) _reports.SelectedIndex = 0;
        else _viewer.Clear();
    }

    private void ShowSelectedReport()
    {
        var index = _reports.SelectedIndex;
        if (index < 0 || index >= _files.Count) return;
        try
        {
            _viewer.Text = File.ReadAllText(_files[index]);
            _empty.Visible = false;
        }
        catch (Exception ex)
        {
            _viewer.Text = ex.ToString();
        }
    }

    private void OpenSelectedFolder()
    {
        var index = _reports.SelectedIndex;
        if (index < 0 || index >= _files.Count) return;
        var folder = Path.GetDirectoryName(_files[index]);
        if (folder is null) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
    }
}
