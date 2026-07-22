using CopyWeb.Services;

namespace CopyWeb;

public sealed class ArchiveValidationForm : Form
{
    private readonly string _projectFile;
    private readonly Label _summary = UiTheme.Label("آماده بررسی", 11, FontStyle.Bold);
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, AutoGenerateColumns = false, BackgroundColor = UiTheme.Surface, BorderStyle = BorderStyle.None };

    public ArchiveValidationForm(string projectFile)
    {
        _projectFile = projectFile; Text = "اعتبارسنجی آرشیو"; StartPosition = FormStartPosition.CenterParent; Size = new Size(820, 560); Font = UiTheme.NormalFont; RightToLeft = RightToLeft.Yes; BackColor = UiTheme.Background;
        var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18), BackColor = UiTheme.Background }; var title = UiTheme.Label("اعتبارسنجی کامل آرشیو و تشخیص فایل خراب", 17, FontStyle.Bold); title.Dock = DockStyle.Top; title.Height = 38;
        _summary.Dock = DockStyle.Top; _summary.Height = 30;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "نوع", Width = 110 }); _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "مسیر", Width = 300 }); _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "توضیحات", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, WrapContents = false }; var run = UiTheme.Button("شروع اعتبارسنجی", UiTheme.Primary); run.Width = 150; run.Click += async (_, _) => await RunAsync(); var close = UiTheme.Button("بستن", Color.White); close.Tag = "secondary-button"; close.Width = 90; close.Click += (_, _) => Close(); actions.Controls.AddRange([close, run]);
        root.Controls.Add(_grid); root.Controls.Add(_summary); root.Controls.Add(actions); root.Controls.Add(title); Controls.Add(root); Shown += async (_, _) => await RunAsync();
    }

    private async Task RunAsync()
    {
        _summary.Text = "در حال بررسی فایل‌ها، HTML و لینک‌های محلی..."; _grid.Rows.Clear();
        var result = await ArchiveValidatorService.ValidateAsync(_projectFile);
        _summary.Text = result.IsValid ? $"آرشیو سالم است — {result.FilesChecked:N0} فایل بررسی شد." : $"آرشیو نیاز به بررسی دارد — {result.Issues.Count:N0} مشکل از {result.FilesChecked:N0} فایل.";
        foreach (var issue in result.Issues) _grid.Rows.Add(issue.Kind, issue.Path, issue.Message);
    }
}
