using System.Text.RegularExpressions;

namespace CopyWeb;

/// <summary>Offline, privacy-friendly assistant for common archive questions.</summary>
public sealed class ArchiveChatForm : Form
{
    private readonly string _root;
    private readonly TextBox _question = new() { Dock = DockStyle.Fill, PlaceholderText = "مثلاً: همه ایمیل‌ها را پیدا کن یا صفحه قیمت کجاست؟", RightToLeft = RightToLeft.Yes };
    private readonly RichTextBox _answer = new() { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, ScrollBars = RichTextBoxScrollBars.Vertical };

    public ArchiveChatForm(string root)
    {
        _root = Path.GetFullPath(root);
        Text = "چت با آرشیو ذخیره‌شده";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(900, 620);
        MinimumSize = new Size(680, 450);
        Font = UiTheme.NormalFont;
        RightToLeft = RightToLeft.Yes;
        BackColor = UiTheme.Background;
        BuildUi();
    }

    private void BuildUi()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18), BackColor = UiTheme.Background };
        var title = UiTheme.Label("چت و جست‌وجوی هوشمند داخل آرشیو", 17, FontStyle.Bold); title.Dock = DockStyle.Top; title.Height = 38;
        var hint = UiTheme.Label("پرسش‌ها به‌صورت آفلاین در فایل‌های HTML جست‌وجو می‌شوند و اطلاعاتی به اینترنت ارسال نمی‌شود.", 9, color: UiTheme.Muted); hint.Dock = DockStyle.Top; hint.Height = 28;
        var bar = new TableLayoutPanel { Dock = DockStyle.Top, Height = 42, ColumnCount = 2, RightToLeft = RightToLeft.Yes };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112)); bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var ask = UiTheme.Button("پرسش", UiTheme.Primary); ask.Dock = DockStyle.Fill; ask.Click += async (_, _) => await AskAsync();
        bar.Controls.Add(ask, 0, 0); bar.Controls.Add(_question, 1, 0);
        panel.Controls.Add(_answer); panel.Controls.Add(bar); panel.Controls.Add(hint); panel.Controls.Add(title); Controls.Add(panel);
        _question.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await AskAsync(); } };
    }

    private async Task AskAsync()
    {
        var question = _question.Text.Trim();
        if (question.Length == 0) return;
        _answer.Text = "در حال بررسی صفحات ذخیره‌شده...";
        var result = await Task.Run(() => Answer(question));
        _answer.Text = result;
    }

    private string Answer(string question)
    {
        var files = Directory.Exists(_root) ? Directory.EnumerateFiles(_root, "*.html", SearchOption.AllDirectories).ToList() : [];
        if (files.Count == 0) return "هیچ صفحه‌ی HTML قابل جست‌وجویی در این پروژه پیدا نشد.";
        var lower = question.ToLowerInvariant();
        if (lower.Contains("ایمیل") || lower.Contains("email") || lower.Contains("mail"))
        {
            var emails = files.SelectMany(file => Regex.Matches(File.ReadAllText(file), @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase).Select(x => (file, value: x.Value))).Distinct().Take(100).ToList();
            return emails.Count == 0 ? "ایمیلی پیدا نشد." : "ایمیل‌های پیدا‌شده:\n\n" + string.Join("\n", emails.Select(x => $"• {x.value} — {Path.GetRelativePath(_root, x.file)}"));
        }
        if (lower.Contains("قیمت") || lower.Contains("price") || lower.Contains("هزینه"))
        {
            var rows = FindLines(files, ["قیمت", "price", "تومان", "ریال", "$", "€", "رایگان"]);
            return rows.Count == 0 ? "عبارت یا نشانه‌ای از قیمت پیدا نشد." : "نتیجه‌های مرتبط با قیمت:\n\n" + FormatRows(rows);
        }
        if (lower.Contains("صفحه") || lower.Contains("page") || lower.Contains("کجاست"))
        {
            var words = question.Split([' ', '؟', '?', '،', ','], StringSplitOptions.RemoveEmptyEntries).Where(x => x.Length > 2 && !x.Equals("صفحه", StringComparison.OrdinalIgnoreCase)).Take(8).ToArray();
            var rows = FindLines(files, words);
            return rows.Count == 0 ? "صفحه‌ای با این عبارت پیدا نشد." : "صفحه‌های مرتبط:\n\n" + FormatRows(rows);
        }
        var general = FindLines(files, [question]);
        return general.Count == 0 ? "نتیجه‌ای پیدا نشد. یک کلمه‌ی کلیدی کوتاه‌تر امتحان کنید." : FormatRows(general);
    }

    private static List<(string File, int Line, string Text)> FindLines(IEnumerable<string> files, IEnumerable<string> terms)
    {
        var list = terms.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        return files.SelectMany(file => File.ReadLines(file).Select((text, index) => (File: file, Line: index + 1, Text: Regex.Replace(text, "<[^>]+>", " ").Trim())))
            .Where(x => x.Text.Length > 0 && list.Any(term => x.Text.Contains(term, StringComparison.OrdinalIgnoreCase))).Take(100).ToList();
    }

    private static string FormatRows(IEnumerable<(string File, int Line, string Text)> rows) => string.Join("\n", rows.Select(x => $"• {Path.GetFileName(x.File)}:{x.Line} — {x.Text[..Math.Min(220, x.Text.Length)]}"));
}
