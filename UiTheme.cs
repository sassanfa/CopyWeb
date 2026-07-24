using CopyWeb.Models;
using System.Runtime.InteropServices;

namespace CopyWeb;

internal static class UiTheme
{
    public static Color Background { get; private set; } = Color.FromArgb(8, 12, 37);
    public static Color Surface { get; private set; } = Color.FromArgb(20, 26, 57);
    public static Color Primary { get; private set; } = Color.FromArgb(20, 25, 58);
    public static Color PrimaryDark { get; private set; } = Color.FromArgb(12, 16, 40);
    public static Color Action { get; private set; } = Color.FromArgb(99, 79, 154);
    public static Color Accent { get; private set; } = Color.FromArgb(67, 105, 88);
    public static Color Text { get; private set; } = Color.FromArgb(241, 245, 249);
    public static Color Muted { get; private set; } = Color.FromArgb(164, 174, 205);
    public static Color Border { get; private set; } = Color.FromArgb(42, 49, 87);
    public static Color Danger { get; private set; } = Color.FromArgb(137, 78, 86);
    public static bool DarkMode => IsDark(Background);
    public static readonly Font NormalFont = new("Segoe UI", 10F);

    public static void Apply(AppSettings settings)
    {
        Primary = Color.FromArgb(settings.PrimaryColorArgb);
        Background = Color.FromArgb(settings.BackgroundColorArgb);
        Surface = Color.FromArgb(settings.SurfaceColorArgb);
        PrimaryDark = ControlPaint.Dark(Primary, 0.25F);
        if (IsDark(Background))
        {
            Action = settings.ThemePreset switch
            {
                "شب آبی" => Color.FromArgb(70, 98, 230),
                "شب سبز" => Color.FromArgb(58, 140, 110),
                "شب بنفش" => Color.FromArgb(111, 82, 255),
                "تیره" => Color.FromArgb(90, 110, 145),
                _ => IsDark(Primary) ? ControlPaint.Light(Primary, 0.30F) : Primary
            };
            Accent = settings.ThemePreset == "شب سبز" ? Color.FromArgb(74, 180, 139) : Color.FromArgb(67, 145, 112);
            Danger = Color.FromArgb(137, 78, 86);
            Text = Color.FromArgb(241, 245, 249);
            Muted = Color.FromArgb(164, 174, 205);
        }
        else
        {
            Action = Primary;
            Accent = Color.FromArgb(91, 130, 111);
            Danger = Color.FromArgb(145, 90, 90);
            Text = Color.FromArgb(30, 41, 59);
            Muted = Color.FromArgb(100, 116, 139);
        }
        Border = IsDark(Background) ? ControlPaint.Light(Background, 0.25F) : Color.FromArgb(226, 232, 240);
    }

    private static bool IsDark(Color color) => (color.R * 299 + color.G * 587 + color.B * 114) < 145000;

    public static Button Button(string text, Color? backColor = null)
    {
        var effectiveColor = backColor ?? Primary;
        if (IsDark(Background) && backColor.HasValue && backColor.Value != Color.Transparent && !IsDark(backColor.Value))
            effectiveColor = Color.FromArgb(31, 39, 77);
        var button = new Button
        {
            Text = text,
            AutoSize = false,
            Height = 40,
            BackColor = effectiveColor,
            ForeColor = (backColor == Color.White || backColor == Color.Transparent || IsDark(Background) && effectiveColor == Color.FromArgb(31, 39, 77)) ? Text : Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font(NormalFont, FontStyle.Bold),
            Padding = new Padding(12, 0, 12, 0),
            UseVisualStyleBackColor = false,
            RightToLeft = RightToLeft.Yes
        };
        button.Tag = backColor is null ? "primary-button" : "secondary-button";
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = IsDark(Background) ? ControlPaint.Light(effectiveColor, 0.12F) : backColor ?? PrimaryDark;
        button.FlatAppearance.MouseDownBackColor = IsDark(Background) ? ControlPaint.Light(effectiveColor, 0.18F) : backColor ?? PrimaryDark;
        button.Resize += (_, _) => ApplyRoundedRegion(button);
        ApplyRoundedRegion(button);
        return button;
    }

    private static void ApplyRoundedRegion(Button button)
    {
        if (button.Width <= 0 || button.Height <= 0) return;
        var radius = Math.Min(16, Math.Max(8, button.Height / 3));
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(0, 0, diameter, diameter, 180, 90);
        path.AddArc(button.Width - diameter - 1, 0, diameter, diameter, 270, 90);
        path.AddArc(button.Width - diameter - 1, button.Height - diameter - 1, diameter, diameter, 0, 90);
        path.AddArc(0, button.Height - diameter - 1, diameter, diameter, 90, 90);
        path.CloseFigure();
        var previous = button.Region;
        button.Region = new Region(path);
        previous?.Dispose();
    }

    public static Panel RoundedInput(TextBox input, Size size, Point location)
    {
        var container = new Panel
        {
            Size = size,
            Location = location,
            BackColor = Border,
            Padding = new Padding(1),
            Tag = "input-container"
        };
        container.Resize += (_, _) => ApplyRoundedRegion(container, 10);
        ApplyRoundedRegion(container, 10);

        input.BorderStyle = BorderStyle.None;
        input.Dock = DockStyle.Fill;
        input.Margin = Padding.Empty;
        input.BackColor = Surface;
        container.Controls.Add(input);
        return container;
    }

    private static void ApplyRoundedRegion(Control control, int radius)
    {
        if (control.Width <= 0 || control.Height <= 0) return;
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(0, 0, diameter, diameter, 180, 90);
        path.AddArc(control.Width - diameter - 1, 0, diameter, diameter, 270, 90);
        path.AddArc(control.Width - diameter - 1, control.Height - diameter - 1, diameter, diameter, 0, 90);
        path.AddArc(0, control.Height - diameter - 1, diameter, diameter, 90, 90);
        path.CloseFigure();
        var previous = control.Region;
        control.Region = new Region(path);
        previous?.Dispose();
    }

    public static Label Label(string text, float size = 10, FontStyle style = FontStyle.Regular, Color? color = null) =>
        new()
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI", size, style),
            ForeColor = color ?? Text,
            RightToLeft = RightToLeft.Yes,
            Tag = color == Color.White ? "on-primary" : color == Muted ? "muted" : "text"
        };

    public static Panel Card() => new()
    {
        BackColor = Surface,
        Padding = new Padding(22),
        BorderStyle = BorderStyle.None,
        Tag = "surface"
    };

    /// <summary>Normalizes native WinForms controls used by dark secondary dialogs.</summary>
    public static void StyleDialog(Control root)
    {
        if (root is Form form)
        {
            form.BackColor = Background;
            form.ForeColor = Text;
            var icon = MainForm.LoadApplicationIcon();
            if (icon is not null)
            {
                form.Icon = icon;
                form.ShowIcon = true;
            }
            EnableActiveCaption(form);
        }

        foreach (var control in Enumerate(root))
        {
            switch (control)
            {
                case FlowLayoutPanel flow:
                    NormalizeContainer(flow);
                    break;
                case TableLayoutPanel table:
                    NormalizeContainer(table);
                    break;
                case TabPage page:
                    page.BackColor = Surface;
                    page.ForeColor = Text;
                    break;
                case Panel panel:
                    NormalizeContainer(panel);
                    break;
                case SplitContainer split:
                    split.BackColor = Border;
                    NormalizeContainer(split.Panel1);
                    NormalizeContainer(split.Panel2);
                    break;
                case TabControl tabs:
                    tabs.BackColor = Background;
                    tabs.ForeColor = Text;
                    break;
                case TextBoxBase text:
                    text.BackColor = ControlPaint.Light(Surface, 0.045F);
                    text.ForeColor = Text;
                    text.BorderStyle = text.Multiline ? BorderStyle.FixedSingle : text.BorderStyle;
                    break;
                case ComboBox combo:
                    combo.BackColor = ControlPaint.Light(Surface, 0.045F);
                    combo.ForeColor = Text;
                    combo.FlatStyle = FlatStyle.Flat;
                    combo.DrawMode = DrawMode.OwnerDrawFixed;
                    combo.ItemHeight = Math.Max(22, combo.ItemHeight);
                    combo.HandleCreated -= ComboHandleCreated;
                    combo.HandleCreated += ComboHandleCreated;
                    combo.DrawItem -= DrawComboItem;
                    combo.DrawItem += DrawComboItem;
                    break;
                case NumericUpDown numeric:
                    numeric.BackColor = ControlPaint.Light(Surface, 0.045F);
                    numeric.ForeColor = Text;
                    numeric.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case CheckedListBox checkedList:
                    checkedList.BackColor = Surface;
                    checkedList.ForeColor = Text;
                    checkedList.BorderStyle = BorderStyle.None;
                    break;
                case ListBox list:
                    list.BackColor = Surface;
                    list.ForeColor = Text;
                    break;
                case ListView list:
                    list.BackColor = Surface;
                    list.ForeColor = Text;
                    list.BorderStyle = BorderStyle.None;
                    break;
                case DateTimePicker picker:
                    picker.CalendarMonthBackground = Surface;
                    picker.CalendarForeColor = Text;
                    picker.CalendarTitleBackColor = Action;
                    picker.CalendarTitleForeColor = Color.White;
                    break;
                case TreeView tree:
                    tree.BackColor = Surface;
                    tree.ForeColor = Text;
                    tree.BorderStyle = BorderStyle.None;
                    break;
                case DataGridView grid:
                    grid.EnableHeadersVisualStyles = false;
                    grid.BackgroundColor = Surface;
                    grid.GridColor = Border;
                    grid.DefaultCellStyle.BackColor = Surface;
                    grid.DefaultCellStyle.ForeColor = Text;
                    grid.DefaultCellStyle.SelectionBackColor = Action;
                    grid.DefaultCellStyle.SelectionForeColor = Color.White;
                    grid.AlternatingRowsDefaultCellStyle.BackColor = ControlPaint.Light(Surface, 0.025F);
                    grid.AlternatingRowsDefaultCellStyle.ForeColor = Text;
                    grid.ColumnHeadersDefaultCellStyle.BackColor = ControlPaint.Light(Surface, 0.08F);
                    grid.ColumnHeadersDefaultCellStyle.ForeColor = Text;
                    grid.RowHeadersDefaultCellStyle.BackColor = Surface;
                    grid.RowHeadersDefaultCellStyle.ForeColor = Text;
                    foreach (DataGridViewColumn column in grid.Columns)
                    {
                        if (column is not DataGridViewButtonColumn buttonColumn) continue;
                        buttonColumn.FlatStyle = FlatStyle.Flat;
                        buttonColumn.DefaultCellStyle.BackColor = ControlPaint.Light(Surface, 0.075F);
                        buttonColumn.DefaultCellStyle.ForeColor = Text;
                        buttonColumn.DefaultCellStyle.SelectionBackColor = Action;
                        buttonColumn.DefaultCellStyle.SelectionForeColor = Color.White;
                    }
                    break;
                case CheckBox check:
                    check.ForeColor = Text;
                    check.UseVisualStyleBackColor = false;
                    if (check.Parent is not null) check.BackColor = check.Parent.BackColor;
                    break;
                case RadioButton radio:
                    radio.ForeColor = Text;
                    radio.UseVisualStyleBackColor = false;
                    if (radio.Parent is not null) radio.BackColor = radio.Parent.BackColor;
                    break;
                case Label label when label.Parent is not null && IsDark(label.Parent.BackColor) && IsDark(label.ForeColor):
                    label.ForeColor = Text;
                    break;
            }
        }
    }

    public static void EnableActiveCaption(Form form)
    {
        void Active(object? sender, EventArgs args) => ApplyCaption(form, true);
        void Inactive(object? sender, EventArgs args) => ApplyCaption(form, false);
        void HandleCreated(object? sender, EventArgs args) => ApplyCaption(form, form.ContainsFocus || Form.ActiveForm == form);
        form.Activated -= Active;
        form.Deactivate -= Inactive;
        form.HandleCreated -= HandleCreated;
        form.Activated += Active;
        form.Deactivate += Inactive;
        form.HandleCreated += HandleCreated;
        if (form.IsHandleCreated) ApplyCaption(form, form.ContainsFocus || Form.ActiveForm == form);
    }

    private static void ApplyCaption(Form form, bool active)
    {
        if (!OperatingSystem.IsWindows() || !form.IsHandleCreated || form.FormBorderStyle == FormBorderStyle.None) return;
        try
        {
            var caption = active
                ? (DarkMode ? Action : Primary)
                : (DarkMode ? ControlPaint.Dark(PrimaryDark, 0.10F) : ControlPaint.Light(Background, 0.02F));
            var text = active || !DarkMode ? Color.White : Muted;
            var border = active ? ControlPaint.Light(caption, 0.18F) : Border;
            SetDwmColor(form.Handle, DwmwaCaptionColor, caption);
            SetDwmColor(form.Handle, DwmwaTextColor, text);
            SetDwmColor(form.Handle, DwmwaBorderColor, border);
        }
        catch
        {
            // Older Windows versions simply keep the system title-bar colors.
        }
    }

    private static void SetDwmColor(IntPtr window, int attribute, Color color)
    {
        var colorRef = color.R | color.G << 8 | color.B << 16;
        _ = DwmSetWindowAttribute(window, attribute, ref colorRef, sizeof(int));
    }

    private static void NormalizeContainer(Control control)
    {
        if (control.BackColor == Color.Transparent) return;
        if (IsLegacyLight(control.BackColor) || control.BackColor == SystemColors.Control)
            control.BackColor = Surface;
        control.ForeColor = Text;
    }

    private static bool IsLegacyLight(Color color) =>
        color == Color.White ||
        color == SystemColors.Window ||
        color.R >= 238 && color.G >= 238 && color.B >= 238;

    private static void DrawComboItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ComboBox combo) return;
        var selected = (e.State & DrawItemState.Selected) != 0;
        var backColor = selected ? Action : ControlPaint.Light(Surface, 0.045F);
        var foreColor = selected ? Color.White : Text;
        using var background = new SolidBrush(backColor);
        e.Graphics.FillRectangle(background, e.Bounds);
        var value = e.Index >= 0 && e.Index < combo.Items.Count
            ? combo.GetItemText(combo.Items[e.Index])
            : combo.Text;
        TextRenderer.DrawText(
            e.Graphics,
            value,
            combo.Font,
            Rectangle.Inflate(e.Bounds, -6, 0),
            foreColor,
            TextFormatFlags.VerticalCenter |
            (combo.RightToLeft == RightToLeft.Yes ? TextFormatFlags.Right : TextFormatFlags.Left) |
            TextFormatFlags.EndEllipsis);
        e.DrawFocusRectangle();
    }

    private static void ComboHandleCreated(object? sender, EventArgs e)
    {
        if (sender is not ComboBox combo) return;
        SetWindowTheme(combo.Handle, string.Empty, string.Empty);
        combo.BackColor = ControlPaint.Light(Surface, 0.045F);
        combo.ForeColor = Text;
        combo.Invalidate();
    }

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr window, string? subAppName, string? subIdList);

    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int valueSize);

    private static IEnumerable<Control> Enumerate(Control root)
    {
        yield return root;
        foreach (Control child in root.Controls)
            foreach (var descendant in Enumerate(child))
                yield return descendant;
    }
}
