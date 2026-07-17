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
            $"CopyWeb Created by SassanFa\nDate : Today {DateTime.Now:yyyy-MM-dd}\nVersion 1.0.13\nEmail : Sassanfa@gmail.com",
            12,
            color: UiTheme.Text);
        details.AutoSize = false;
        details.Dock = DockStyle.Fill;
        details.RightToLeft = RightToLeft.No;
        details.TextAlign = ContentAlignment.MiddleCenter;

        var close = UiTheme.Button("بستن", Color.White);
        close.Tag = "secondary-button";
        close.Dock = DockStyle.Bottom;
        close.Height = 42;
        close.Click += (_, _) => Close();

        card.Controls.Add(details);
        card.Controls.Add(title);
        card.Controls.Add(close);
        Controls.Add(card);
    }
}
