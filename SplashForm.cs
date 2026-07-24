namespace CopyWeb;

internal sealed class SplashForm : Form
{
    private readonly Label _status;
    private readonly System.Windows.Forms.Timer _animation = new() { Interval = 350 };
    private readonly Image? _loadingImage;
    private int _frame;

    public SplashForm()
    {
        Text = "CopyWeb";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(480, 330);
        BackColor = Color.FromArgb(7, 12, 37);
        ForeColor = Color.White;
        Font = UiTheme.NormalFont;
        ShowInTaskbar = true;
        TopMost = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        DoubleBuffered = true;

        var card = new DashboardCard
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(34, 26, 34, 24),
            BackColor = Color.FromArgb(18, 26, 65),
            BorderColor = Color.FromArgb(55, 65, 126),
            AccentColor = Color.FromArgb(124, 77, 255),
            CornerRadius = 20
        };
        var loadingPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Loading.Transparent.gif");
        _loadingImage = File.Exists(loadingPath) ? Image.FromFile(loadingPath) : MainForm.LoadApplicationIcon()?.ToBitmap();
        var loaderFrame = new DashboardCard
        {
            Size = new Size(319, 173),
            Location = new Point(80, 70),
            Padding = new Padding(16),
            BackColor = Color.FromArgb(18, 26, 65),
            BorderColor = Color.FromArgb(18, 26, 65),
            CornerRadius = 16
        };
        var loader = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(18, 26, 65),
            Image = _loadingImage
        };
        loaderFrame.Controls.Add(loader);
        var title = UiTheme.Label("CopyWeb", 23, FontStyle.Bold, Color.White);
        title.AutoSize = false;
        title.Location = new Point(35, 20);
        title.Size = new Size(410, 42);
        title.TextAlign = ContentAlignment.MiddleCenter;
        title.RightToLeft = RightToLeft.No;
        var version = UiTheme.Label("نسخه 1.3.6", 9, color: Color.FromArgb(172, 184, 220));
        version.AutoSize = false;
        version.Location = new Point(35, 247);
        version.Size = new Size(410, 24);
        version.TextAlign = ContentAlignment.MiddleCenter;
        _status = UiTheme.Label("در حال آماده‌سازی برنامه", 9.5F, color: Color.FromArgb(199, 207, 232));
        _status.AutoSize = false;
        _status.Location = new Point(35, 278);
        _status.Size = new Size(410, 26);
        _status.TextAlign = ContentAlignment.MiddleCenter;
        card.Controls.Add(_status);
        card.Controls.Add(version);
        card.Controls.Add(loaderFrame);
        card.Controls.Add(title);
        Controls.Add(card);

        _animation.Tick += (_, _) =>
        {
            _frame = (_frame + 1) % 4;
            _status.Text = "در حال آماده‌سازی برنامه" + new string('.', _frame);
        };
        Shown += (_, _) => _animation.Start();
        FormClosed += (_, _) =>
        {
            _animation.Dispose();
            _loadingImage?.Dispose();
        };
    }
}
