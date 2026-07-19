using CopyWeb.Models;
using CopyWeb.Services;

namespace CopyWeb;

public sealed class ProxyProfilesForm : Form
{
    private readonly ListBox _list = new();
    private readonly TextBox _name = new();
    private readonly ComboBox _kind = new();
    private readonly TextBox _address = new();
    private readonly NumericUpDown _port = new();
    private readonly TextBox _user = new();
    private readonly TextBox _password = new();
    private readonly List<ProxyProfile> _profiles;

    public ProxyProfile? SelectedProfile { get; private set; }

    public ProxyProfilesForm(IEnumerable<ProxyProfile> profiles)
    {
        _profiles = profiles.Select(Clone).ToList();
        Text = "پروفایل‌های پروکسی";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(760, 470);
        MinimumSize = new Size(680, 420);
        Font = UiTheme.NormalFont;
        RightToLeft = RightToLeft.Yes;
        BackColor = UiTheme.Background;
        BuildUi();
        RefreshList();
    }

    private void BuildUi()
    {
        var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18), BackColor = UiTheme.Background };
        var title = UiTheme.Label("پروفایل‌های پروکسی", 18, FontStyle.Bold); title.Dock = DockStyle.Top; title.Height = 38;
        _list.Dock = DockStyle.Left; _list.Width = 220; _list.BorderStyle = BorderStyle.FixedSingle;
        _list.SelectedIndexChanged += (_, _) => LoadSelected();
        var card = UiTheme.Card(); card.Dock = DockStyle.Fill; card.Padding = new Padding(18);
        Configure(_name, "نام پروفایل", new Point(18, 18), 360);
        _kind.Items.AddRange(["HTTP", "HTTPS", "SOCKS5"]); _kind.DropDownStyle = ComboBoxStyle.DropDownList; _kind.SelectedIndex = 0; _kind.Location = new Point(18, 70); _kind.Width = 120;
        Configure(_address, "آدرس پروکسی", new Point(150, 70), 180);
        _port.Minimum = 1; _port.Maximum = 65535; _port.Value = 8080; _port.Location = new Point(340, 70); _port.Width = 80;
        Configure(_user, "نام کاربری", new Point(18, 122), 190);
        Configure(_password, "رمز عبور", new Point(218, 122), 190); _password.UseSystemPasswordChar = true;
        var add = UiTheme.Button("افزودن / به‌روزرسانی", UiTheme.Primary); add.Location = new Point(18, 180); add.Width = 180; add.Click += (_, _) => AddOrUpdate();
        var remove = UiTheme.Button("حذف پروفایل", UiTheme.Danger); remove.Location = new Point(208, 180); remove.Width = 130; remove.Click += (_, _) => RemoveSelected();
        var use = UiTheme.Button("استفاده", UiTheme.Accent); use.Location = new Point(348, 180); use.Width = 100; use.Click += (_, _) => UseSelected();
        var health = UiTheme.Button("بررسی سلامت همه", Color.FromArgb(232, 237, 245)); health.Tag = "secondary-button"; health.ForeColor = UiTheme.Text; health.Location = new Point(18, 230); health.Width = 180; health.Click += async (_, _) => await CheckHealthAsync();
        card.Controls.AddRange([_name, _kind, _address, _port, _user, _password, add, remove, use, health]);
        var close = UiTheme.Button("بستن", Color.White); close.Tag = "secondary-button"; close.Dock = DockStyle.Bottom; close.Height = 40; close.Click += (_, _) => Close();
        root.Controls.Add(card); root.Controls.Add(_list); root.Controls.Add(close); root.Controls.Add(title); Controls.Add(root);
    }

    private static void Configure(TextBox box, string placeholder, Point location, int width)
    {
        box.PlaceholderText = placeholder; box.Location = location; box.Width = width; box.Height = 28; box.BorderStyle = BorderStyle.FixedSingle; box.RightToLeft = RightToLeft.No;
    }

    private void RefreshList()
    {
        _list.Items.Clear();
        foreach (var profile in _profiles) _list.Items.Add(profile.Name);
        if (_list.Items.Count > 0) _list.SelectedIndex = 0;
    }

    private void LoadSelected()
    {
        if (_list.SelectedIndex < 0 || _list.SelectedIndex >= _profiles.Count) return;
        var p = _profiles[_list.SelectedIndex];
        _name.Text = p.Name; _kind.SelectedIndex = p.Kind switch { ProxyKind.Https => 1, ProxyKind.Socks5 => 2, _ => 0 }; _address.Text = p.Address; _port.Value = Math.Clamp(p.Port, 1, 65535); _user.Text = SecureStorage.Unprotect(p.EncryptedUsername) ?? string.Empty; _password.Text = SecureStorage.Unprotect(p.EncryptedPassword) ?? string.Empty;
    }

    private void AddOrUpdate()
    {
        if (string.IsNullOrWhiteSpace(_name.Text) || string.IsNullOrWhiteSpace(_address.Text)) { MessageBox.Show(this, "نام و آدرس پروفایل را وارد کنید.", "پروفایل", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        var item = new ProxyProfile { Name = _name.Text.Trim(), Kind = _kind.SelectedIndex switch { 1 => ProxyKind.Https, 2 => ProxyKind.Socks5, _ => ProxyKind.Http }, Enabled = true, Address = _address.Text.Trim(), Port = (int)_port.Value, EncryptedUsername = SecureStorage.Protect(_user.Text.Trim()), EncryptedPassword = SecureStorage.Protect(_password.Text) };
        var index = _profiles.FindIndex(x => x.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase));
        if (index >= 0) _profiles[index] = item; else _profiles.Add(item);
        SaveProfiles(); RefreshList(); _list.SelectedIndex = Math.Max(0, _profiles.FindIndex(x => x.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase)));
    }

    private void RemoveSelected()
    {
        if (_list.SelectedIndex < 0) return;
        _profiles.RemoveAt(_list.SelectedIndex); SaveProfiles(); RefreshList();
    }

    private void UseSelected()
    {
        if (_list.SelectedIndex < 0) return;
        SelectedProfile = Clone(_profiles[_list.SelectedIndex]);
        DialogResult = DialogResult.OK; Close();
    }

    private void SaveProfiles()
    {
        var settings = AppSettingsStore.Load(); settings.ProxyProfiles = _profiles.Select(Clone).ToList(); AppSettingsStore.Save(settings);
    }

    private async Task CheckHealthAsync()
    {
        if (_profiles.Count == 0) return;
        var results = await ProxyPoolService.CheckAsync(_profiles);
        var text = string.Join(Environment.NewLine, results.Select(x => $"{x.Name}: {(x.IsHealthy ? "سالم" : "ناموفق")} | {x.Message} | {x.Elapsed.TotalMilliseconds:0} ms"));
        MessageBox.Show(this, text, "سلامت پروکسی‌ها", MessageBoxButtons.OK, results.All(x => x.IsHealthy) ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    private static ProxyProfile Clone(ProxyProfile source) => new() { Name = source.Name, Kind = source.Kind, Enabled = source.Enabled, Address = source.Address, Port = source.Port, EncryptedUsername = source.EncryptedUsername, EncryptedPassword = source.EncryptedPassword };
}
