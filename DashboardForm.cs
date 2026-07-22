using CopyWeb.Models;
using CopyWeb.Services;
using LinkState = CopyWeb.Models.LinkState;

namespace CopyWeb;

public sealed class DashboardForm : Form
{
    public DashboardForm(string projectFile)
    {
        Text = "داشبورد پروژه"; StartPosition = FormStartPosition.CenterParent; Size = new Size(760, 520); Font = UiTheme.NormalFont; RightToLeft = RightToLeft.Yes; BackColor = UiTheme.Background;
        var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18), BackColor = UiTheme.Background };
        var title = UiTheme.Label("داشبورد حرفه‌ای پروژه", 17, FontStyle.Bold); title.Dock = DockStyle.Top; title.Height = 38;
        var grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, BackgroundColor = UiTheme.Surface, BorderStyle = BorderStyle.None, RightToLeft = RightToLeft.Yes };
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "نوع فایل", Width = 180 }); grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "تعداد", Width = 110 }); grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "حجم", Width = 150 });
        var close = UiTheme.Button("بستن", Color.White); close.Tag = "secondary-button"; close.Dock = DockStyle.Bottom; close.Height = 42; close.Click += (_, _) => Close();
        root.Controls.Add(grid); root.Controls.Add(close); root.Controls.Add(title); Controls.Add(root);
        Shown += async (_, _) => await LoadAsync(projectFile, grid, title);
    }

    private static async Task LoadAsync(string file, DataGridView grid, Label title)
    {
        try
        {
            var project = await ProjectStorage.LoadAsync(file); var root = Path.GetDirectoryName(file)!;
            var groups = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Where(x => !x.EndsWith("links.json", StringComparison.OrdinalIgnoreCase) && !x.EndsWith("activity.jsonl", StringComparison.OrdinalIgnoreCase)).GroupBy(x => Path.GetExtension(x).ToLowerInvariant() switch { ".html" or ".htm" => "صفحات", ".css" => "CSS", ".js" or ".mjs" => "JavaScript", ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".svg" => "تصاویر", ".woff" or ".woff2" or ".ttf" or ".otf" => "فونت", _ => "سایر" }).OrderBy(x => x.Key);
            foreach (var group in groups) grid.Rows.Add(group.Key, group.Count(), FormatBytes(group.Sum(x => new FileInfo(x).Length)));
            var failed = project.Links.Count(x => x.State == LinkState.Failed); var downloaded = project.Links.Count(x => x.State == LinkState.Downloaded);
            title.Text = $"داشبورد پروژه — صفحات: {project.Links.Count:N0} | دانلودشده: {downloaded:N0} | ناموفق: {failed:N0}";
        }
        catch (Exception ex) { title.Text = ex.Message; }
    }

    private static string FormatBytes(long bytes) => bytes switch { < 1024 => $"{bytes} B", < 1024 * 1024 => $"{bytes / 1024d:0.0} KB", < 1024 * 1024 * 1024 => $"{bytes / 1024d / 1024d:0.0} MB", _ => $"{bytes / 1024d / 1024d / 1024d:0.0} GB" };
}
