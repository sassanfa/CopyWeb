using System.Drawing.Drawing2D;

namespace CopyWeb;

internal sealed class GradientPanel : Panel
{
    public Color StartColor = Color.FromArgb(7, 11, 35);
    public Color EndColor = Color.FromArgb(15, 22, 55);

    public GradientPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (ClientRectangle.Width <= 0 || ClientRectangle.Height <= 0) return;
        using var brush = new LinearGradientBrush(ClientRectangle, StartColor, EndColor, 25F);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }
}

internal sealed class DashboardCard : Panel
{
    public int CornerRadius = 14;
    public Color BorderColor = Color.FromArgb(42, 52, 96);
    public Color AccentColor = Color.Transparent;

    public DashboardCard()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.FromArgb(19, 26, 58);
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        if (Width < 2 || Height < 2) return;
        using var path = RoundedPath(new Rectangle(0, 0, Width, Height), CornerRadius);
        var previous = Region;
        Region = new Region(path);
        previous?.Dispose();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var borderPath = RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), CornerRadius);
        using var borderPen = new Pen(BorderColor);
        e.Graphics.DrawPath(borderPen, borderPath);
        if (AccentColor != Color.Transparent)
        {
            using var accentBrush = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(1, Width), 3), AccentColor, Color.Transparent, 0F);
            e.Graphics.FillRectangle(accentBrush, 14, 0, Math.Max(1, Width - 28), 2);
        }
    }

    private static GraphicsPath RoundedPath(Rectangle rectangle, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Max(2, Math.Min(radius * 2, Math.Min(rectangle.Width, rectangle.Height)));
        path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class LogoMarkControl : Control
{
    public LogoMarkControl()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        DoubleBuffered = true;
        BackColor = Color.Transparent;
        Size = new Size(62, 52);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(3, 8, Math.Max(1, Width - 7), Math.Max(1, Height - 15));
        using var glow = new SolidBrush(Color.FromArgb(45, 124, 92, 255));
        e.Graphics.FillEllipse(glow, bounds.Left - 2, bounds.Top - 2, bounds.Width + 4, bounds.Height + 4);
        using var brush = new LinearGradientBrush(bounds, Color.FromArgb(81, 191, 255), Color.FromArgb(135, 61, 240), 15F);
        e.Graphics.FillEllipse(brush, bounds.Left + 2, bounds.Top + 9, bounds.Width - 4, bounds.Height - 10);
        e.Graphics.FillEllipse(brush, bounds.Left + 12, bounds.Top + 1, bounds.Width / 2, bounds.Height - 2);
        e.Graphics.FillEllipse(brush, bounds.Right - 25, bounds.Top + 9, 24, bounds.Height - 11);
    }
}

internal sealed class DashboardTrendChart : Control
{
    private readonly List<int> _values = [0, 8, 18, 31, 45, 59, 72, 82];

    public DashboardTrendChart()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.Transparent;
        MinimumSize = new Size(180, 80);
    }

    public void SetProgress(int value)
    {
        value = Math.Clamp(value, 0, 100);
        if (_values.Count >= 18) _values.RemoveAt(0);
        if (_values.Count == 0 || _values[^1] != value) _values.Add(value);
        Invalidate();
    }

    public void ResetProgress()
    {
        _values.Clear();
        _values.Add(0);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var area = new Rectangle(10, 8, Math.Max(20, Width - 20), Math.Max(20, Height - 20));
        using var gridPen = new Pen(Color.FromArgb(38, 55, 98));
        for (var i = 0; i <= 4; i++)
        {
            var y = area.Top + area.Height * i / 4;
            e.Graphics.DrawLine(gridPen, area.Left, y, area.Right, y);
        }

        if (_values.Count < 2) return;
        var points = _values.Select((value, index) => new PointF(
            area.Left + area.Width * index / (float)Math.Max(1, _values.Count - 1),
            area.Bottom - area.Height * value / 100F)).ToArray();
        using var fillPath = new GraphicsPath();
        fillPath.AddLines(points);
        fillPath.AddLine(points[^1], new PointF(points[^1].X, area.Bottom));
        fillPath.AddLine(new PointF(points[^1].X, area.Bottom), new PointF(points[0].X, area.Bottom));
        fillPath.CloseFigure();
        using var fill = new LinearGradientBrush(area, Color.FromArgb(115, 48, 145, 255), Color.FromArgb(8, 48, 145, 255), 90F);
        e.Graphics.FillPath(fill, fillPath);
        using var line = new Pen(Color.FromArgb(97, 118, 255), 2.5F) { LineJoin = LineJoin.Round };
        e.Graphics.DrawLines(line, points);
        using var pointBrush = new SolidBrush(Color.FromArgb(98, 231, 255));
        foreach (var point in points) e.Graphics.FillEllipse(pointBrush, point.X - 3, point.Y - 3, 6, 6);
    }
}

internal sealed class DashboardDonut : Control
{
    private int _success;
    private int _active;
    private int _queued;
    private int _failed;

    public DashboardDonut()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.Transparent;
        MinimumSize = new Size(112, 112);
    }

    public void SetCounts(int success, int active, int queued, int failed)
    {
        _success = Math.Max(0, success);
        _active = Math.Max(0, active);
        _queued = Math.Max(0, queued);
        _failed = Math.Max(0, failed);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var size = Math.Max(20, Math.Min(Width, Height) - 20);
        var ring = new Rectangle((Width - size) / 2, (Height - size) / 2, size, size);
        var total = Math.Max(1, _success + _active + _queued + _failed);
        using var basePen = new Pen(Color.FromArgb(42, 51, 91), 12) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        e.Graphics.DrawArc(basePen, ring, -90, 359.5F);
        var start = -90F;
        foreach (var part in new[]
                 {
                     (_success, Color.FromArgb(52, 211, 153)),
                     (_active, Color.FromArgb(96, 165, 250)),
                     (_queued, Color.FromArgb(148, 163, 184)),
                     (_failed, Color.FromArgb(248, 113, 113))
                 })
        {
            if (part.Item1 <= 0) continue;
            var sweep = part.Item1 * 360F / total;
            using var pen = new Pen(part.Item2, 12) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            e.Graphics.DrawArc(pen, ring, start, Math.Max(1F, sweep - 2F));
            start += sweep;
        }

        var percent = (_success + _failed) * 100 / total;
        var text = $"{percent}%";
        using var font = new Font("Segoe UI", Math.Max(10F, size / 8F), FontStyle.Bold);
        using var brush = new SolidBrush(Color.FromArgb(241, 245, 249));
        var measured = e.Graphics.MeasureString(text, font);
        e.Graphics.DrawString(text, font, brush, (Width - measured.Width) / 2, (Height - measured.Height) / 2);
    }
}

internal sealed class DashboardProgressBar : Control
{
    private int _value;

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => _value;
        set { _value = Math.Clamp(value, 0, 100); Invalidate(); }
    }

    public Color TrackColor = Color.FromArgb(35, 44, 82);
    public Color FillColor = Color.FromArgb(99, 102, 241);

    public DashboardProgressBar()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        Height = 12;
        BackColor = TrackColor;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
        using var trackPath = RoundedRectangle(bounds, Math.Max(2, Height / 2));
        using var track = new SolidBrush(TrackColor);
        e.Graphics.FillPath(track, trackPath);
        if (_value <= 0) return;
        var fillWidth = Math.Max(Height, (int)Math.Round(bounds.Width * _value / 100D));
        var fillBounds = new Rectangle(bounds.X, bounds.Y, Math.Min(bounds.Width, fillWidth), bounds.Height);
        using var fillPath = RoundedRectangle(fillBounds, Math.Max(2, Height / 2));
        using var fill = new LinearGradientBrush(fillBounds, FillColor, Color.FromArgb(60, 218, 255), 0F);
        e.Graphics.FillPath(fill, fillPath);
    }

    private static GraphicsPath RoundedRectangle(Rectangle rectangle, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Max(2, Math.Min(radius * 2, Math.Min(rectangle.Width, rectangle.Height)));
        path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class DashboardNumericInput : UserControl
{
    private readonly NumericUpDown _source;
    private readonly TextBox _editor;
    private readonly Button _increase;
    private readonly Button _decrease;
    private bool _syncing;
    private Color _borderColor = Color.FromArgb(55, 68, 121);
    private Color _actionColor = Color.FromArgb(111, 82, 255);

    public DashboardNumericInput(NumericUpDown source)
    {
        _source = source;
        DoubleBuffered = true;
        BackColor = Color.FromArgb(28, 37, 76);
        Padding = new Padding(9, 4, 0, 4);
        Height = 34;

        _editor = new TextBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackColor = BackColor,
            ForeColor = Color.FromArgb(238, 242, 255),
            Font = new Font("Segoe UI", 9.5F),
            TextAlign = HorizontalAlignment.Left,
            RightToLeft = RightToLeft.No
        };
        _increase = CreateStepButton("+");
        _decrease = CreateStepButton("−");
        _increase.Click += (_, _) => Step(+1);
        _decrease.Click += (_, _) => Step(-1);
        _editor.KeyDown += EditorKeyDown;
        _editor.Leave += (_, _) => CommitEditor();
        _source.ValueChanged += (_, _) => SyncFromSource();
        _source.Visible = false;
        _source.Dock = DockStyle.None;
        _source.SetBounds(-100, -100, 1, 1);

        Controls.Add(_editor);
        Controls.Add(_decrease);
        Controls.Add(_increase);
        Controls.Add(_source);
        SyncFromSource();
    }

    private static Button CreateStepButton(string text) => new()
    {
        Dock = DockStyle.Right,
        Width = 29,
        Text = text,
        FlatStyle = FlatStyle.Flat,
        FlatAppearance = { BorderSize = 0, MouseOverBackColor = Color.FromArgb(48, 58, 111), MouseDownBackColor = Color.FromArgb(68, 53, 142) },
        BackColor = Color.FromArgb(34, 44, 88),
        ForeColor = Color.FromArgb(203, 213, 242),
        Font = new Font("Segoe UI", 10F, FontStyle.Bold),
        Cursor = Cursors.Hand,
        TabStop = false
    };

    private void Step(int direction)
    {
        CommitEditor();
        var next = _source.Value + direction * _source.Increment;
        _source.Value = Math.Clamp(next, _source.Minimum, _source.Maximum);
    }

    private void EditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter) { CommitEditor(); e.SuppressKeyPress = true; }
        else if (e.KeyCode == Keys.Up) { Step(+1); e.SuppressKeyPress = true; }
        else if (e.KeyCode == Keys.Down) { Step(-1); e.SuppressKeyPress = true; }
    }

    private void CommitEditor()
    {
        if (_syncing) return;
        if (!decimal.TryParse(_editor.Text.Trim(), out var value)) { SyncFromSource(); return; }
        _source.Value = Math.Clamp(value, _source.Minimum, _source.Maximum);
        SyncFromSource();
    }

    private void SyncFromSource()
    {
        _syncing = true;
        _editor.Text = _source.Value.ToString();
        _syncing = false;
    }

    public void ApplyTheme(Color surface, Color border, Color text, Color action)
    {
        _borderColor = border;
        _actionColor = action;
        BackColor = surface;
        _editor.BackColor = surface;
        _editor.ForeColor = text;
        _increase.BackColor = ControlPaint.Light(surface, 0.06F);
        _decrease.BackColor = ControlPaint.Light(surface, 0.06F);
        _increase.FlatAppearance.MouseDownBackColor = _actionColor;
        _decrease.FlatAppearance.MouseDownBackColor = _actionColor;
        _increase.ForeColor = text;
        _decrease.ForeColor = text;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(_borderColor);
        e.Graphics.DrawRectangle(pen, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
    }
}

internal sealed class DashboardCheckBox : Control
{
    private readonly CheckBox _source;
    private Color _actionColor = Color.FromArgb(111, 82, 255);
    private Color _borderColor = Color.FromArgb(92, 105, 160);

    public DashboardCheckBox(CheckBox source)
    {
        _source = source;
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
        ForeColor = Color.FromArgb(222, 228, 245);
        Font = new Font("Segoe UI", 9F);
        Height = 34;
        _source.Visible = false;
        _source.Dock = DockStyle.None;
        _source.SetBounds(-100, -100, 1, 1);
        Controls.Add(_source);
        _source.CheckedChanged += (_, _) => Invalidate();
        _source.TextChanged += (_, _) => Invalidate();
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        if (_source.Enabled) _source.Checked = !_source.Checked;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var boxSize = 17;
        var box = new Rectangle(Math.Max(2, Width - boxSize - 5), (Height - boxSize) / 2, boxSize, boxSize);
        using var background = new SolidBrush(_source.Checked ? _actionColor : Color.FromArgb(20, 28, 64));
        using var outline = new Pen(_source.Checked ? ControlPaint.Light(_actionColor, 0.22F) : _borderColor, 1.5F);
        using var path = RoundedRectangle(box, 4);
        e.Graphics.FillPath(background, path);
        e.Graphics.DrawPath(outline, path);
        if (_source.Checked)
        {
            using var check = new Pen(Color.White, 1.8F) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            e.Graphics.DrawLines(check, [new Point(box.Left + 4, box.Top + 9), new Point(box.Left + 7, box.Bottom - 4), new Point(box.Right - 3, box.Top + 4)]);
        }

        var textBounds = new Rectangle(4, 0, Math.Max(1, box.Left - 10), Height);
        TextRenderer.DrawText(e.Graphics, _source.Text, Font, textBounds, Enabled ? ForeColor : Color.FromArgb(110, 120, 150), TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.RightToLeft);
    }

    public void ApplyTheme(Color text, Color border, Color action)
    {
        ForeColor = text;
        _borderColor = border;
        _actionColor = action;
        Invalidate();
    }

    private static GraphicsPath RoundedRectangle(Rectangle rectangle, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class DashboardCloudStatus : Control
{
    private int _value;

    public DashboardCloudStatus()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        BackColor = Color.Transparent;
    }

    public void SetProgress(int value)
    {
        _value = Math.Clamp(value, 0, 100);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var cx = Width / 2F;
        var cloudTop = Math.Max(8F, Height * .12F);
        var cloudWidth = Math.Min(134F, Width - 20F);
        var cloudHeight = Math.Min(62F, Height * .55F);
        var cloudLeft = cx - cloudWidth / 2F;
        using var glowPath = CloudPath(new RectangleF(cloudLeft - 8, cloudTop - 7, cloudWidth + 16, cloudHeight + 15));
        using var glow = new SolidBrush(Color.FromArgb(28, 89, 72, 255));
        g.FillPath(glow, glowPath);
        using var cloudPath = CloudPath(new RectangleF(cloudLeft, cloudTop, cloudWidth, cloudHeight));
        using var cloudFill = new LinearGradientBrush(new RectangleF(cloudLeft, cloudTop, cloudWidth, cloudHeight), Color.FromArgb(111, 77, 255), Color.FromArgb(59, 124, 246), 20F);
        g.FillPath(cloudFill, cloudPath);
        using var cloudPen = new Pen(Color.FromArgb(125, 139, 255), 1.2F);
        g.DrawPath(cloudPen, cloudPath);

        var ringSize = Math.Min(58F, Math.Max(42F, Height * .42F));
        var ring = new RectangleF(cx - ringSize / 2F, cloudTop + cloudHeight - ringSize * .45F, ringSize, ringSize);
        using var ringBack = new Pen(Color.FromArgb(65, 75, 125), 7F);
        using var ringFill = new Pen(_value >= 100 ? Color.FromArgb(52, 211, 153) : Color.FromArgb(102, 126, 234), 7F);
        ringBack.StartCap = ringBack.EndCap = LineCap.Round;
        ringFill.StartCap = ringFill.EndCap = LineCap.Round;
        g.DrawArc(ringBack, ring, -90, 359.8F);
        if (_value > 0) g.DrawArc(ringFill, ring, -90, Math.Max(2F, 360F * _value / 100F));
        using var font = new Font("Segoe UI", 10F, FontStyle.Bold);
        var valueText = $"{_value}%";
        var valueSize = g.MeasureString(valueText, font);
        using var text = new SolidBrush(Color.White);
        g.DrawString(valueText, font, text, cx - valueSize.Width / 2F, ring.Top + (ring.Height - valueSize.Height) / 2F);
    }

    private static GraphicsPath CloudPath(RectangleF bounds)
    {
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top + bounds.Height * .42F, bounds.Width * .38F, bounds.Height * .52F, 100, 200);
        path.AddArc(bounds.Left + bounds.Width * .18F, bounds.Top + bounds.Height * .12F, bounds.Width * .42F, bounds.Height * .68F, 190, 190);
        path.AddArc(bounds.Left + bounds.Width * .43F, bounds.Top, bounds.Width * .42F, bounds.Height * .78F, 180, 190);
        path.AddArc(bounds.Left + bounds.Width * .67F, bounds.Top + bounds.Height * .32F, bounds.Width * .32F, bounds.Height * .58F, 250, 205);
        path.AddLine(bounds.Right - bounds.Width * .12F, bounds.Bottom, bounds.Left + bounds.Width * .17F, bounds.Bottom);
        path.CloseFigure();
        return path;
    }
}

internal enum DashboardButtonIcon
{
    None,
    Download,
    Settings,
    Play,
    Pause,
    Shield,
    Book,
    Code,
    Live,
    Home,
    Globe,
    Folder,
    Report,
    Info,
    More,
    Plus
}

internal sealed class DashboardButton : Button
{
    private const int BmClick = 0x00F5;
    private bool _hovered;
    private bool _pressed;

    public Color FillStart = Color.FromArgb(24, 32, 70);
    public Color FillEnd = Color.FromArgb(24, 32, 70);
    public Color OutlineColor = Color.FromArgb(48, 59, 108);
    public int CornerRadius = 9;
    public int OutlineWidth = 1;
    public DashboardButtonIcon IconKind;
    public ContentAlignment IconAlignment = ContentAlignment.MiddleRight;

    public DashboardButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        ForeColor = Color.FromArgb(241, 245, 249);
        TextAlign = ContentAlignment.MiddleCenter;
        TabStop = true;
    }

    protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs mevent) { _pressed = true; Invalidate(); base.OnMouseDown(mevent); }
    protected override void OnMouseUp(MouseEventArgs mevent) { _pressed = false; Invalidate(); base.OnMouseUp(mevent); }

    protected override void WndProc(ref Message message)
    {
        // User-painted WinForms buttons do not reliably process BM_CLICK on
        // every Windows/.NET combination.  Supporting it explicitly keeps the
        // dashboard compatible with UI automation and accessibility tools.
        if (message.Msg == BmClick)
        {
            PerformClick();
            return;
        }

        base.WndProc(ref message);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (Width < 2 || Height < 2) return;
        using var path = RoundedButtonPath(new Rectangle(0, 0, Width, Height), CornerRadius);
        var previous = Region;
        Region = new Region(path);
        previous?.Dispose();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
        using var path = RoundedButtonPath(bounds, CornerRadius);
        var start = ButtonShade(FillStart, _pressed, _hovered);
        var end = ButtonShade(FillEnd, _pressed, _hovered);
        using var fill = new LinearGradientBrush(bounds, start, end, 0F);
        e.Graphics.FillPath(fill, path);
        if (OutlineWidth > 0)
        {
            using var pen = new Pen(_hovered ? ControlPaint.Light(OutlineColor, 0.22F) : OutlineColor, OutlineWidth);
            e.Graphics.DrawPath(pen, path);
        }
        var textBounds = bounds;
        if (IconKind != DashboardButtonIcon.None)
        {
            var iconSize = Math.Clamp(Height / 3, 13, 18);
            var iconX = IconAlignment == ContentAlignment.MiddleLeft ? 15 : Width - iconSize - 15;
            var iconBounds = new Rectangle(iconX, (Height - iconSize) / 2, iconSize, iconSize);
            DrawIcon(e.Graphics, iconBounds, Enabled ? ForeColor : Color.FromArgb(108, 121, 153));
            textBounds = IconAlignment == ContentAlignment.MiddleLeft
                ? new Rectangle(iconBounds.Right + 8, 0, Math.Max(1, Width - iconBounds.Right - 12), Height)
                : new Rectangle(8, 0, Math.Max(1, iconBounds.Left - 12), Height);
        }
        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
        if (RightToLeft == RightToLeft.Yes) flags |= TextFormatFlags.RightToLeft;
        TextRenderer.DrawText(e.Graphics, Text, Font, textBounds, Enabled ? ForeColor : Color.FromArgb(108, 121, 153), flags);
        if (Focused && ShowFocusCues)
        {
            var focus = Rectangle.Inflate(bounds, -4, -4);
            ControlPaint.DrawFocusRectangle(e.Graphics, focus, ForeColor, Color.Transparent);
        }
    }

    private static Color ButtonShade(Color color, bool pressed, bool hovered)
    {
        // ControlPaint.Light(Color.Transparent) becomes an opaque white rectangle.
        // Keep icon-only transparent buttons inside the dark dashboard palette.
        if (color.A == 0)
            return pressed ? Color.FromArgb(42, 51, 96) : hovered ? Color.FromArgb(34, 43, 86) : Color.Transparent;
        return pressed ? ControlPaint.Dark(color, 0.08F) : hovered ? ControlPaint.Light(color, 0.08F) : color;
    }

    private void DrawIcon(Graphics graphics, Rectangle bounds, Color color)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(color, 1.65F) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        var cx = bounds.Left + bounds.Width / 2F;
        var cy = bounds.Top + bounds.Height / 2F;
        var left = bounds.Left + 1F;
        var top = bounds.Top + 1F;
        var right = bounds.Right - 1F;
        var bottom = bounds.Bottom - 1F;
        switch (IconKind)
        {
            case DashboardButtonIcon.Download:
                graphics.DrawLine(pen, cx, top, cx, bottom - 4);
                graphics.DrawLine(pen, cx, bottom - 4, cx - 4, bottom - 8);
                graphics.DrawLine(pen, cx, bottom - 4, cx + 4, bottom - 8);
                graphics.DrawLine(pen, left + 2, bottom, right - 2, bottom);
                break;
            case DashboardButtonIcon.Settings:
                graphics.DrawEllipse(pen, cx - 3, cy - 3, 6, 6);
                graphics.DrawEllipse(pen, cx - 6, cy - 6, 12, 12);
                for (var i = 0; i < 8; i++)
                {
                    var angle = i * Math.PI / 4;
                    graphics.DrawLine(pen,
                        cx + (float)Math.Cos(angle) * 7, cy + (float)Math.Sin(angle) * 7,
                        cx + (float)Math.Cos(angle) * 9, cy + (float)Math.Sin(angle) * 9);
                }
                break;
            case DashboardButtonIcon.Play:
                graphics.DrawPolygon(pen, [new PointF(left + 3, top + 1), new PointF(right - 1, cy), new PointF(left + 3, bottom - 1)]);
                break;
            case DashboardButtonIcon.Pause:
                graphics.DrawLine(pen, cx - 3, top + 1, cx - 3, bottom - 1);
                graphics.DrawLine(pen, cx + 3, top + 1, cx + 3, bottom - 1);
                break;
            case DashboardButtonIcon.Shield:
                using (var shield = new GraphicsPath())
                {
                    shield.AddLines([new PointF(cx, top), new PointF(right - 1, top + 3), new PointF(right - 2, cy + 3), new PointF(cx, bottom), new PointF(left + 2, cy + 3), new PointF(left + 1, top + 3), new PointF(cx, top)]);
                    graphics.DrawPath(pen, shield);
                    graphics.DrawLine(pen, cx - 2, cy, cx, cy + 2);
                    graphics.DrawLine(pen, cx, cy + 2, cx + 4, cy - 3);
                }
                break;
            case DashboardButtonIcon.Book:
                graphics.DrawRectangle(pen, left + 1, top + 2, bounds.Width - 3, bounds.Height - 4);
                graphics.DrawLine(pen, cx, top + 2, cx, bottom - 2);
                break;
            case DashboardButtonIcon.Code:
                graphics.DrawLines(pen, [new PointF(cx - 2, top + 2), new PointF(left + 1, cy), new PointF(cx - 2, bottom - 2)]);
                graphics.DrawLines(pen, [new PointF(cx + 2, top + 2), new PointF(right - 1, cy), new PointF(cx + 2, bottom - 2)]);
                break;
            case DashboardButtonIcon.Live:
                using (var dot = new SolidBrush(color)) graphics.FillEllipse(dot, cx - 2, cy - 2, 4, 4);
                graphics.DrawArc(pen, cx - 6, cy - 6, 12, 12, -55, 110);
                graphics.DrawArc(pen, cx - 6, cy - 6, 12, 12, 125, 110);
                break;
            case DashboardButtonIcon.Home:
                graphics.DrawLines(pen, [new PointF(left, cy), new PointF(cx, top), new PointF(right, cy)]);
                graphics.DrawRectangle(pen, left + 3, cy, bounds.Width - 6, bounds.Height / 2F - 1);
                break;
            case DashboardButtonIcon.Globe:
                graphics.DrawEllipse(pen, left, top, bounds.Width - 2, bounds.Height - 2);
                graphics.DrawEllipse(pen, cx - 4, top, 8, bounds.Height - 2);
                graphics.DrawLine(pen, left + 1, cy, right - 1, cy);
                break;
            case DashboardButtonIcon.Folder:
                using (var folder = new GraphicsPath())
                {
                    folder.AddLines([new PointF(left, top + 4), new PointF(cx - 2, top + 4), new PointF(cx, top + 7), new PointF(right, top + 7), new PointF(right, bottom), new PointF(left, bottom), new PointF(left, top + 4)]);
                    graphics.DrawPath(pen, folder);
                }
                break;
            case DashboardButtonIcon.Report:
                graphics.DrawLine(pen, left + 1, top, left + 1, bottom);
                graphics.DrawLine(pen, left + 1, bottom, right, bottom);
                graphics.DrawLine(pen, left + 5, bottom - 2, left + 5, cy + 1);
                graphics.DrawLine(pen, cx, bottom - 2, cx, top + 5);
                graphics.DrawLine(pen, right - 3, bottom - 2, right - 3, cy - 2);
                break;
            case DashboardButtonIcon.Info:
                graphics.DrawEllipse(pen, left, top, bounds.Width - 2, bounds.Height - 2);
                graphics.DrawLine(pen, cx, cy - 1, cx, bottom - 4);
                using (var dot = new SolidBrush(color)) graphics.FillEllipse(dot, cx - 1, top + 3, 2, 2);
                break;
            case DashboardButtonIcon.More:
                using (var dots = new SolidBrush(color))
                {
                    graphics.FillEllipse(dots, cx - 7, cy - 1.5F, 3, 3);
                    graphics.FillEllipse(dots, cx - 1.5F, cy - 1.5F, 3, 3);
                    graphics.FillEllipse(dots, cx + 4, cy - 1.5F, 3, 3);
                }
                break;
            case DashboardButtonIcon.Plus:
                graphics.DrawLine(pen, left + 2, cy, right - 2, cy);
                graphics.DrawLine(pen, cx, top + 2, cx, bottom - 2);
                break;
        }
    }

    private static GraphicsPath RoundedButtonPath(Rectangle rectangle, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Max(2, Math.Min(radius * 2, Math.Min(rectangle.Width, rectangle.Height)));
        path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
