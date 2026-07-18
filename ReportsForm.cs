using CopyWeb.Models;
using CopyWeb.Services;
using System.Diagnostics;

namespace CopyWeb;

public sealed class ReportsForm : Form
{
    private readonly ListBox _reports = new();
    private readonly RichTextBox _viewer = new();
    private readonly ComboBox _severity = new();
    private readonly TextBox _search = new();
    private readonly Label _empty = UiTheme.Label("هنوز گزارشی ذخیره نشده است.", 10, color: UiTheme.Muted);
    private readonly List<string> _files = [];
    private IReadOnlyList<ActivityLogEntry> _selectedEntries = [];

    public ReportsForm()
    {
        Text = "گزارش‌های فعالیت";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1100, 680);
        MinimumSize = new Size(850, 520);
        Font = UiTheme.NormalFont;
        RightToLeft = RightToLeft.Yes;
        BackColor = UiTheme.Background;
        BuildUi();
        Localization.Apply(this, AppSettingsStore.Load().Language);
        LoadReports();
    }

    private void BuildUi()
    {
        var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18), BackColor = UiTheme.Background };
        var header = UiTheme.Label("گزارش‌های ذخیره‌شده", 18, FontStyle.Bold);
        header.Dock = DockStyle.Top;
        header.Height = 42;

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 46, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false, BackColor = Color.Transparent
        };
        _severity.DropDownStyle = ComboBoxStyle.DropDownList;
        _severity.Items.AddRange(["همه رویدادها", "موفق", "اطلاعات", "هشدار", "خطا"]);
        _severity.SelectedIndex = 0;
        _severity.Width = 130;
        _severity.SelectedIndexChanged += (_, _) => ApplyEntryFilter();
        _search.PlaceholderText = "جست‌وجو در گزارش...";
        _search.Width = 250;
        _search.TextChanged += (_, _) => ApplyEntryFilter();
        var exportText = UiTheme.Button("خروجی TXT", Color.White); exportText.Tag = "secondary-button"; exportText.Width = 95; exportText.Click += (_, _) => Export(ActivityExport.Text);
        var exportCsv = UiTheme.Button("خروجی CSV", Color.White); exportCsv.Tag = "secondary-button"; exportCsv.Width = 95; exportCsv.Click += (_, _) => Export(ActivityExport.Csv);
        var exportJson = UiTheme.Button("خروجی JSON", Color.White); exportJson.Tag = "secondary-button"; exportJson.Width = 105; exportJson.Click += (_, _) => Export(ActivityExport.Json);
        toolbar.Controls.AddRange([exportJson, exportCsv, exportText, _search, _severity]);

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
            Dock = DockStyle.Bottom, Height = 48,
            FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = Color.Transparent
        };
        var refresh = UiTheme.Button("به‌روزرسانی", Color.White); refresh.Tag = "secondary-button"; refresh.Width = 120; refresh.Click += (_, _) => LoadReports();
        var openFolder = UiTheme.Button("بازکردن پوشه گزارش", Color.White); openFolder.Tag = "secondary-button"; openFolder.Width = 155; openFolder.Click += (_, _) => OpenSelectedFolder();
        var crash = UiTheme.Button("Crash Log", Color.White); crash.Tag = "secondary-button"; crash.Width = 105; crash.Click += (_, _) => OpenCrashFolder();
        var close = UiTheme.Button("بستن", UiTheme.Primary); close.Width = 100; close.Click += (_, _) => Close();
        buttons.Controls.AddRange([close, crash, openFolder, refresh]);

        root.Controls.Add(right);
        root.Controls.Add(left);
        root.Controls.Add(buttons);
        root.Controls.Add(toolbar);
        root.Controls.Add(header);
        Controls.Add(root);
    }

    private void LoadReports()
    {
        _reports.Items.Clear();
        _files.Clear();
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CopyWeb");
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(root))
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(root, "activity.log", SearchOption.AllDirectories)) files.Add(file);
            }
            catch { }
        }
        foreach (var projectFile in ProjectStorage.GetKnownProjectFiles())
        {
            var folder = Path.GetDirectoryName(projectFile);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                var log = Path.Combine(folder, "activity.log");
                if (File.Exists(log)) files.Add(log);
            }
        }
        if (File.Exists(CrashLogger.LatestFilePath)) files.Add(CrashLogger.LatestFilePath);
        foreach (var file in files.Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc))
        {
            _files.Add(file);
            _reports.Items.Add(file.Equals(CrashLogger.LatestFilePath, StringComparison.OrdinalIgnoreCase)
                ? "Crash Log برنامه"
                : Path.GetFileName(Path.GetDirectoryName(file) ?? file));
        }

        _empty.Visible = _files.Count == 0;
        if (_files.Count > 0) _reports.SelectedIndex = 0;
        else { _viewer.Clear(); _selectedEntries = []; }
    }

    private void ShowSelectedReport()
    {
        var index = _reports.SelectedIndex;
        if (index < 0 || index >= _files.Count) return;
        var file = _files[index];
        try
        {
            _selectedEntries = file.Equals(CrashLogger.LatestFilePath, StringComparison.OrdinalIgnoreCase)
                ? [] : ActivityLogStore.Read(file);
            _search.Clear();
            ApplyEntryFilter();
            if (_selectedEntries.Count == 0)
            {
                _viewer.Text = File.ReadAllText(file);
                _empty.Visible = false;
            }
        }
        catch (Exception ex) { _viewer.Text = ex.ToString(); _empty.Visible = false; }
    }

    private void ApplyEntryFilter()
    {
        if (_selectedEntries.Count == 0) return;
        var selectedSeverity = _severity.SelectedIndex;
        var term = _search.Text.Trim();
        var entries = _selectedEntries.Where(x =>
                selectedSeverity == 0 || (selectedSeverity == 1 && x.Severity == ActivitySeverity.Success) ||
                (selectedSeverity == 2 && x.Severity == ActivitySeverity.Info) ||
                (selectedSeverity == 3 && x.Severity == ActivitySeverity.Warning) ||
                (selectedSeverity == 4 && x.Severity == ActivitySeverity.Error))
            .Where(x => term.Length == 0 || x.Message.Contains(term, StringComparison.OrdinalIgnoreCase) || x.Url.Contains(term, StringComparison.OrdinalIgnoreCase) || (x.Details?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();
        _viewer.Text = string.Join(Environment.NewLine, entries.Select(x => $"[{x.Timestamp:yyyy-MM-dd HH:mm:ss}] [{x.Severity}] {x.Url} {x.Message}".TrimEnd()));
        _empty.Visible = false;
    }

    private void Export(ActivityExport export)
    {
        if (_selectedEntries.Count == 0) return;
        var entries = GetFilteredEntries();
        using var dialog = new SaveFileDialog { Filter = export switch { ActivityExport.Csv => "CSV|*.csv", ActivityExport.Json => "JSON|*.json", _ => "Text|*.txt" }, FileName = $"copyweb-report-{DateTime.Now:yyyyMMdd-HHmmss}" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            switch (export)
            {
                case ActivityExport.Csv: ActivityLogStore.ExportCsv(entries, dialog.FileName); break;
                case ActivityExport.Json: ActivityLogStore.ExportJson(entries, dialog.FileName); break;
                default: ActivityLogStore.ExportText(entries, dialog.FileName); break;
            }
            MessageBox.Show(this, "گزارش با موفقیت ذخیره شد.", "خروجی گزارش", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "خطای خروجی", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private List<ActivityLogEntry> GetFilteredEntries()
    {
        var term = _search.Text.Trim();
        return _selectedEntries.Where(x =>
                _severity.SelectedIndex == 0 || (_severity.SelectedIndex == 1 && x.Severity == ActivitySeverity.Success) ||
                (_severity.SelectedIndex == 2 && x.Severity == ActivitySeverity.Info) || (_severity.SelectedIndex == 3 && x.Severity == ActivitySeverity.Warning) || (_severity.SelectedIndex == 4 && x.Severity == ActivitySeverity.Error))
            .Where(x => term.Length == 0 || x.Message.Contains(term, StringComparison.OrdinalIgnoreCase) || x.Url.Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private void OpenSelectedFolder()
    {
        var index = _reports.SelectedIndex;
        if (index < 0 || index >= _files.Count) return;
        var folder = Path.GetDirectoryName(_files[index]);
        if (folder is null) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
    }

    private static void OpenCrashFolder()
    {
        Directory.CreateDirectory(CrashLogger.DirectoryPath);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{CrashLogger.DirectoryPath}\"") { UseShellExecute = true });
    }

    private enum ActivityExport { Text, Csv, Json }
}
