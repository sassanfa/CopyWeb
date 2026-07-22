using CopyWeb.Services;

namespace CopyWeb;

public sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text = "درباره CopyWeb";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(520, 360);
        MinimumSize = new Size(480, 320);
        Font = UiTheme.NormalFont;
        RightToLeft = RightToLeft.Yes;
        BackColor = UiTheme.Background;

        var card = UiTheme.Card();
        card.Dock = DockStyle.Fill;
        card.Padding = new Padding(30);

        var title = UiTheme.Label("CopyWeb", 24, FontStyle.Bold, UiTheme.Primary);
        title.AutoSize = false;
        title.Dock = DockStyle.Top;
        title.Height = 48;
        title.RightToLeft = RightToLeft.No;
        title.TextAlign = ContentAlignment.MiddleCenter;

        var details = UiTheme.Label(
            $"CopyWeb Created by SassanFa\nDate : 1405-04-31\nVersion 1.3.2\nEmail : Sassanfa@gmail.com",
            12,
            color: UiTheme.Text);
        details.AutoSize = false;
        details.Dock = DockStyle.Fill;
        details.RightToLeft = RightToLeft.No;
        details.TextAlign = ContentAlignment.MiddleCenter;

        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = Color.Transparent };
        var close = UiTheme.Button("بستن", Color.White); close.Tag = "secondary-button"; close.Width = 90; close.Height = 38; close.Click += (_, _) => Close();
        var github = UiTheme.Button("صفحه GitHub", Color.White); github.Tag = "secondary-button"; github.Width = 135; github.Height = 38; github.ForeColor = UiTheme.Text; github.RightToLeft = RightToLeft.No; github.TextAlign = ContentAlignment.MiddleCenter; github.Click += (_, _) => UpdateChecker.OpenRepository();
        var check = UiTheme.Button("بررسی نسخه", UiTheme.Primary); check.Width = 125; check.Height = 38; check.RightToLeft = RightToLeft.Yes; check.TextAlign = ContentAlignment.MiddleCenter; check.Click += async (_, _) => await CheckForUpdatesAsync(check);
        actions.Controls.AddRange([close, github, check]);

        card.Controls.Add(details);
        card.Controls.Add(title);
        card.Controls.Add(actions);
        Controls.Add(card);
        Services.Localization.Apply(this, Services.AppSettingsStore.Load().Language);
    }

    private async Task CheckForUpdatesAsync(Button button)
    {
        button.Enabled = false;
        try
        {
            var result = await UpdateChecker.CheckAsync();
            if (result.IsNewer)
            {
                if (MessageBox.Show(this, $"نسخه جدید {result.LatestVersion} منتشر شده است. صفحه GitHub باز شود؟", "به‌روزرسانی", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    UpdateChecker.OpenRepository(result.ReleaseUrl);
            }
            else if (result.Error is not null)
                MessageBox.Show(this, "بررسی نسخه انجام نشد. اتصال اینترنت یا آدرس مخزن را بررسی کنید.", "بررسی نسخه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else if (MessageBox.Show(this, $"نسخه فعلی {result.CurrentVersion} است و به‌روز است. صفحه GitHub باز شود؟", "بررسی نسخه", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                UpdateChecker.OpenRepository(result.ReleaseUrl);
        }
        finally { button.Enabled = true; }
    }
}
