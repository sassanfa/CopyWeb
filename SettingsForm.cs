using CopyWeb.Models;
using CopyWeb.Services;

namespace CopyWeb;

public sealed class SettingsForm : Form
{
    private readonly ComboBox _preset = new();
    private readonly Button _primaryColor = new();
    private readonly Button _backgroundColor = new();
    private readonly CheckBox _saveLogs = new();
    private Color _primary;
    private Color _background;
    private Color _surface;

    public AppSettings? Result { get; private set; }

    public SettingsForm(AppSettings current)
    {
        _primary = Color.FromArgb(current.PrimaryColorArgb);
        _background = Color.FromArgb(current.BackgroundColorArgb);
        _surface = Color.FromArgb(current.SurfaceColorArgb);

        Text = "تنظیمات برنامه";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(620, 470);
        MinimumSize = new Size(560, 420);
        Font = UiTheme.NormalFont;
        RightToLeft = RightToLeft.Yes;
        BackColor = UiTheme.Background;
        BuildUi(current);
    }

    private void BuildUi(AppSettings current)
    {
        var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(22), BackColor = UiTheme.Background };
        var title = UiTheme.Label("تنظیمات ظاهر و گزارش‌ها", 18, FontStyle.Bold);
        title.Dock = DockStyle.Top;
        title.Height = 42;

        var card = UiTheme.Card();
        card.Dock = DockStyle.Fill;

        var presetLabel = UiTheme.Label("قالب رنگی", 11, FontStyle.Bold);
        presetLabel.Location = new Point(22, 24);
        _preset.DropDownStyle = ComboBoxStyle.DropDownList;
        _preset.Items.AddRange(["آبی", "سبز", "بنفش", "تیره", "سفارشی"]);
        _preset.SelectedItem = _preset.Items.Contains(current.ThemePreset) ? current.ThemePreset : "سفارشی";
        _preset.Location = new Point(22, 54);
        _preset.Width = 220;
        _preset.SelectedIndexChanged += (_, _) => ApplyPreset();

        var primaryLabel = UiTheme.Label("رنگ اصلی", 10, color: UiTheme.Muted);
        primaryLabel.Location = new Point(270, 24);
        ConfigureColorButton(_primaryColor, "انتخاب رنگ اصلی", _primary, 270, 54);
        _primaryColor.Click += (_, _) => PickColor(_primaryColor, true);

        var backgroundLabel = UiTheme.Label("رنگ پس‌زمینه", 10, color: UiTheme.Muted);
        backgroundLabel.Location = new Point(22, 116);
        ConfigureColorButton(_backgroundColor, "انتخاب پس‌زمینه", _background, 22, 146);
        _backgroundColor.Click += (_, _) => PickColor(_backgroundColor, false);

        _saveLogs.Text = "ذخیره گزارش کامل فعالیت‌ها در فایل activity.log";
        _saveLogs.AutoSize = true;
        _saveLogs.Checked = current.SaveDetailedLogs;
        _saveLogs.Location = new Point(270, 150);

        var hint = UiTheme.Label("پیشنهاد: قالب تیره برای کار طولانی و قالب آبی برای استفاده روزمره مناسب است.", 9, color: UiTheme.Muted);
        hint.AutoSize = false;
        hint.Location = new Point(22, 210);
        hint.Size = new Size(500, 44);

        card.Controls.AddRange([presetLabel, _preset, primaryLabel, _primaryColor, backgroundLabel, _backgroundColor, _saveLogs, hint]);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        var apply = UiTheme.Button("اعمال و ذخیره", UiTheme.Primary);
        apply.Width = 140;
        apply.Click += (_, _) => SaveAndClose();
        var cancel = UiTheme.Button("انصراف", Color.White);
        cancel.Tag = "secondary-button";
        cancel.Width = 110;
        cancel.Click += (_, _) => Close();
        buttons.Controls.AddRange([apply, cancel]);

        root.Controls.Add(card);
        root.Controls.Add(buttons);
        root.Controls.Add(title);
        Controls.Add(root);
    }

    private void ConfigureColorButton(Button button, string text, Color color, int x, int y)
    {
        button.Text = text;
        button.Location = new Point(x, y);
        button.Size = new Size(220, 38);
        button.BackColor = color;
        button.ForeColor = IsDark(color) ? Color.White : UiTheme.Text;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = UiTheme.Border;
        button.Tag = "secondary-button";
    }

    private void ApplyPreset()
    {
        switch (_preset.SelectedItem?.ToString())
        {
            case "سبز":
                _primary = Color.FromArgb(5, 150, 105);
                _background = Color.FromArgb(242, 250, 247);
                _surface = Color.White;
                break;
            case "بنفش":
                _primary = Color.FromArgb(124, 58, 237);
                _background = Color.FromArgb(248, 246, 255);
                _surface = Color.White;
                break;
            case "تیره":
                _primary = Color.FromArgb(96, 165, 250);
                _background = Color.FromArgb(24, 31, 42);
                _surface = Color.FromArgb(35, 45, 60);
                break;
            case "آبی":
                _primary = Color.FromArgb(39, 91, 219);
                _background = Color.FromArgb(244, 247, 251);
                _surface = Color.White;
                break;
            default:
                return;
        }
        UpdateColorButtons();
    }

    private void UpdateColorButtons()
    {
        _primaryColor.BackColor = _primary;
        _primaryColor.ForeColor = IsDark(_primary) ? Color.White : UiTheme.Text;
        _backgroundColor.BackColor = _background;
        _backgroundColor.ForeColor = IsDark(_background) ? Color.White : UiTheme.Text;
    }

    private void PickColor(Button button, bool primary)
    {
        using var dialog = new ColorDialog { Color = primary ? _primary : _background, FullOpen = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (primary) _primary = dialog.Color;
        else _background = dialog.Color;
        _preset.SelectedItem = "سفارشی";
        UpdateColorButtons();
    }

    private void SaveAndClose()
    {
        var result = new AppSettings
        {
            ThemePreset = _preset.SelectedItem?.ToString() ?? "سفارشی",
            PrimaryColorArgb = _primary.ToArgb(),
            BackgroundColorArgb = _background.ToArgb(),
            SurfaceColorArgb = _surface.ToArgb(),
            SaveDetailedLogs = _saveLogs.Checked
        };
        AppSettingsStore.Save(result);
        UiTheme.Apply(result);
        Result = result;
        DialogResult = DialogResult.OK;
        Close();
    }

    private static bool IsDark(Color color) => (color.R * 299 + color.G * 587 + color.B * 114) < 145000;
}
