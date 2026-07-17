using CopyWeb.Models;

namespace CopyWeb;

internal static class UiTheme
{
    public static Color Background { get; private set; } = Color.FromArgb(244, 247, 251);
    public static Color Surface { get; private set; } = Color.White;
    public static Color Primary { get; private set; } = Color.FromArgb(39, 91, 219);
    public static Color PrimaryDark { get; private set; } = Color.FromArgb(29, 66, 158);
    public static Color Text { get; private set; } = Color.FromArgb(30, 41, 59);
    public static Color Muted { get; private set; } = Color.FromArgb(100, 116, 139);
    public static Color Border { get; private set; } = Color.FromArgb(226, 232, 240);
    public static Color Danger { get; } = Color.FromArgb(220, 38, 38);
    public static readonly Font NormalFont = new("Segoe UI", 10F);

    public static void Apply(AppSettings settings)
    {
        Primary = Color.FromArgb(settings.PrimaryColorArgb);
        Background = Color.FromArgb(settings.BackgroundColorArgb);
        Surface = Color.FromArgb(settings.SurfaceColorArgb);
        PrimaryDark = ControlPaint.Dark(Primary, 0.25F);
        Text = IsDark(Background) ? Color.White : Color.FromArgb(30, 41, 59);
        Muted = IsDark(Background) ? Color.FromArgb(190, 200, 215) : Color.FromArgb(100, 116, 139);
        Border = IsDark(Background) ? ControlPaint.Light(Background, 0.25F) : Color.FromArgb(226, 232, 240);
    }

    private static bool IsDark(Color color) => (color.R * 299 + color.G * 587 + color.B * 114) < 145000;

    public static Button Button(string text, Color? backColor = null)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = false,
            Height = 40,
            BackColor = backColor ?? Primary,
            ForeColor = (backColor == Color.White || backColor == Color.Transparent) ? Text : Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font(NormalFont, FontStyle.Bold),
            Padding = new Padding(12, 0, 12, 0),
            UseVisualStyleBackColor = false,
            RightToLeft = RightToLeft.Yes
        };
        button.Tag = backColor is null ? "primary-button" : "secondary-button";
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = backColor ?? PrimaryDark;
        button.FlatAppearance.MouseDownBackColor = backColor ?? PrimaryDark;
        return button;
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
}
