namespace CopyWeb.Services;

public static class Localization
{
    private static readonly Dictionary<string, string> English = new(StringComparer.Ordinal)
    {
        ["شروع دانلود سایت"] = "Start website download", ["آدرس سایت مورد نظر خود را وارد کنید و تنظیمات را انتخاب نمایید."] = "Enter the website address and choose your settings.",
        ["داشبورد"] = "Dashboard", ["دانلود سریع، مدیریت پروژه‌ها و مشاهده‌ی پیشرفت از یک صفحه"] = "Quick download, project management, and live progress in one place",
        ["افزودن به پروژه‌ها   +"] = "Add project   +", ["پروژه‌های اخیر"] = "Recent projects", ["مشاهده همه پروژه‌ها"] = "View all projects",
        ["عملیات سریع"] = "Quick actions", ["شروع دانلود جدید   ↓"] = "New download   ↓", ["تنظیمات پیشرفته   ⚙"] = "Advanced settings   ⚙",
        ["ادامه پروژه   ▷"] = "Resume project   ▷", ["توقف و ذخیره   □"] = "Stop and save   □", ["تست پروکسی   ◇"] = "Test proxy   ◇",
        ["آموزش   ▤"] = "Tutorial   ▤", ["کپی وب   < >"] = "Copy Web   < >", ["ذخیره زنده   ●"] = "Live archive   ●",
        ["روند دانلود"] = "Download progress", ["خلاصه پروژه"] = "Project summary", ["● تکمیل‌شده"] = "● Completed", ["● در حال دانلود"] = "● Downloading", ["● در صف"] = "● Queued", ["● خطا"] = "● Failed",
        ["عملیات"] = "Actions", ["شروع بررسی سایت"] = "Scan website", ["ادامه پروژه"] = "Resume project", ["توقف و ذخیره"] = "Stop and save", ["تست پروکسی"] = "Test proxy", ["آموزش"] = "Tutorial", ["کپی وبی"] = "CopyWeb mode", ["ذخیره زنده"] = "Live archive",
        ["اطلاعات پروژه"] = "Project information", ["آماده شروع"] = "Ready", ["فایل فعلی: -"] = "Current file: -", ["هنوز عملیاتی انجام نشده است"] = "No operation has started", ["پیشرفت کل پروژه"] = "Overall project progress", ["پیشرفت فایل جاری"] = "Current file progress",
        ["تنظیمات دانلود"] = "Download settings", ["آدرس سایت"] = "Website URL", ["حداکثر صفحه"] = "Page limit", ["عمق لینک"] = "Link depth", ["شامل زیردامنه‌ها"] = "Include subdomains", ["رعایت robots.txt"] = "Respect robots.txt", ["خواندن Sitemap"] = "Read sitemap", ["پیروی از Canonical"] = "Follow canonical",
        ["احراز هویت پروکسی (اختیاری)"] = "Proxy authentication (optional)", ["فعال"] = "Enabled", ["آدرس پروکسی"] = "Proxy address", ["پورت"] = "Port", ["نام کاربری"] = "Username", ["رمز عبور"] = "Password", ["تأخیر درخواست (ms)"] = "Request delay (ms)",
        ["محل ذخیره"] = "Output folder", ["مسیر پوشه خروجی"] = "Output folder path", ["انتخاب مسیر"] = "Choose folder", ["گزارش فعالیت‌ها"] = "Activity log", ["پاک کردن"] = "Clear",
        ["⌂   شروع"] = "⌂   Home", ["🌐   پروژه‌ها"] = "🌐   Projects", ["▣   پروژه‌ها"] = "▣   Projects", ["⚙   تنظیمات"] = "⚙   Settings", ["📊   گزارش‌ها"] = "📊   Reports", ["▤   گزارش‌ها"] = "▤   Reports", ["ⓘ   درباره برنامه"] = "ⓘ   About",
        ["نسخه 1.3.6"] = "Version 1.3.6", ["نسخه 1.3.5"] = "Version 1.3.5", ["نسخه 1.3.4"] = "Version 1.3.4", ["نسخه 1.3.2"] = "Version 1.3.2", ["نسخه 1.3.1"] = "Version 1.3.1", ["نسخه 1.3.0"] = "Version 1.3.0", ["نسخه 1.2.0"] = "Version 1.2.0", ["نسخه 1.1.2"] = "Version 1.1.2", ["نسخه 1.1.0"] = "Version 1.1.0", ["پوشه ذخیره‌سازی"] = "Storage folder", ["حذف پروژه"] = "Delete project", ["چسباندن"] = "Paste", ["چسباندن آدرس"] = "Paste URL", ["متن معتبری در Clipboard وجود ندارد."] = "Clipboard does not contain valid text.", ["نسخه 1.0.18"] = "Version 1.0.18", ["پروژه‌های ذخیره‌شده"] = "Saved projects", ["به‌روزرسانی"] = "Refresh", ["مشاهده لینک‌ها"] = "View links", ["بازکردن پوشه"] = "Open folder", ["بازکردن پوشه گزارش"] = "Open report folder", ["بستن"] = "Close",
        ["گزارش‌های فعالیت"] = "Activity reports", ["گزارش‌های ذخیره‌شده"] = "Saved reports", ["همه رویدادها"] = "All events", ["موفق"] = "Success", ["اطلاعات"] = "Info", ["هشدار"] = "Warning", ["خطا"] = "Error", ["خروجی TXT"] = "Export TXT", ["خروجی CSV"] = "Export CSV", ["خروجی JSON"] = "Export JSON", ["Crash Log"] = "Crash Log برنامه", ["لینک‌ها و منابع مرتبط پیدا شده"] = "Discovered pages and resources", ["جست‌وجو در آدرس، عنوان یا منبع..."] = "Search URL, title, or resource...", ["صفحه بدون عنوان"] = "Untitled page", ["تصویر"] = "Image", ["فونت"] = "Font", ["رسانه"] = "Media", ["فایل"] = "File",
        ["مدیریت لینک‌های سایت"] = "Website links", ["لینک‌های مرتبط پیدا شده"] = "Discovered related links", ["انتخاب همه"] = "Select all", ["لغو انتخاب"] = "Clear selection", ["حذف ردیف"] = "Remove row", ["حذف ناموفق‌ها"] = "Remove failed", ["ذخیره لیست"] = "Save list", ["بارگذاری لیست"] = "Load list",
        ["تنظیمات برنامه"] = "Application settings", ["تنظیمات ظاهر و گزارش‌ها"] = "Appearance and reporting", ["قالب رنگی"] = "Color theme", ["رنگ اصلی"] = "Primary color", ["رنگ پس‌زمینه"] = "Background color", ["اعمال و ذخیره"] = "Apply and save", ["انصراف"] = "Cancel", ["زبان برنامه"] = "Application language", ["فارسی"] = "Persian", ["English"] = "English", ["پیشرفته"] = "Advanced", ["تنظیمات پیشرفته"] = "Advanced settings", ["حالت ساده"] = "Simple mode", ["شروع دانلود خودکار"] = "Start automatic download", ["فعال‌سازی حالت سازگار با صفحات JavaScript / SPA"] = "Enable JavaScript / SPA compatibility", ["User-Agent سفارشی (اختیاری)"] = "Custom User-Agent (optional)", ["Headerهای سفارشی — هر خط: Name: Value"] = "Custom headers — one per line: Name: Value", ["Cookie سفارشی (اختیاری)"] = "Custom Cookie (optional)",
        ["درباره CopyWeb"] = "About CopyWeb", ["تأیید را داخل مرورگر انجام دهید"] = "Complete the verification in the browser", ["ادامه دانلود"] = "Continue download", ["لغو"] = "Cancel"
    };

    private static readonly Dictionary<string, string> Persian = English
        .GroupBy(x => x.Value, StringComparer.Ordinal)
        .ToDictionary(x => x.Key, x => x.First().Key, StringComparer.Ordinal);

    public static void Apply(Control root, string language)
    {
        var isEnglish = language.Equals("en", StringComparison.OrdinalIgnoreCase);
        var translations = isEnglish ? English : Persian;
        root.RightToLeft = isEnglish ? RightToLeft.No : RightToLeft.Yes;
        foreach (Control control in Enumerate(root))
        {
            if (translations.TryGetValue(control.Text, out var translated)) control.Text = translated;
            if (control is TextBox textBox && translations.TryGetValue(textBox.PlaceholderText, out var placeholder)) textBox.PlaceholderText = placeholder;
            if (control is ComboBox combo)
            {
                for (var i = 0; i < combo.Items.Count; i++)
                    if (combo.Items[i] is string item && translations.TryGetValue(item, out var itemTranslation)) combo.Items[i] = itemTranslation;
            }
            control.RightToLeft = isEnglish ? RightToLeft.No : RightToLeft.Yes;
        }
    }

    private static IEnumerable<Control> Enumerate(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in Enumerate(child)) yield return descendant;
        }
    }
}
