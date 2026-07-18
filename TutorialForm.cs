using CopyWeb.Services;
using System.Diagnostics;

namespace CopyWeb;

public sealed class TutorialForm : Form
{
    public TutorialForm()
    {
        var english = AppSettingsStore.Load().Language.Equals("en", StringComparison.OrdinalIgnoreCase);
        Text = english ? "CopyWeb tutorial" : "آموزش CopyWeb";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(780, 680);
        MinimumSize = new Size(620, 520);
        Font = UiTheme.NormalFont;
        RightToLeft = english ? RightToLeft.No : RightToLeft.Yes;
        BackColor = UiTheme.Background;

        var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(22), BackColor = UiTheme.Background };
        var card = UiTheme.Card();
        card.Dock = DockStyle.Fill;
        card.Padding = new Padding(24);

        var title = UiTheme.Label(english ? "How to use CopyWeb" : "راهنمای استفاده از CopyWeb", 19, FontStyle.Bold, UiTheme.Primary);
        title.Dock = DockStyle.Top;
        title.Height = 42;
        title.AutoSize = false;
        title.TextAlign = english ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;

        var description = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = UiTheme.Surface,
            ForeColor = UiTheme.Text,
            Font = new Font(UiTheme.NormalFont.FontFamily, 10.5F),
            RightToLeft = english ? RightToLeft.No : RightToLeft.Yes,
            DetectUrls = true,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            Text = english ? EnglishText : PersianText
        };

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        var close = UiTheme.Button(english ? "Close" : "بستن", Color.White);
        close.Tag = "secondary-button";
        close.Width = 100;
        close.Height = 38;
        close.Click += (_, _) => Close();
        var email = UiTheme.Button(english ? "Email support" : "ایمیل پشتیبانی", UiTheme.Primary);
        email.Width = 145;
        email.Height = 38;
        email.Click += (_, _) => OpenEmail();
        actions.Controls.AddRange([close, email]);

        card.Controls.Add(description);
        card.Controls.Add(title);
        card.Controls.Add(actions);
        root.Controls.Add(card);
        Controls.Add(root);
    }

    private void OpenEmail()
    {
        try
        {
            Process.Start(new ProcessStartInfo("mailto:Sassanfa@gmail.com?subject=CopyWeb%20question") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"امکان باز کردن برنامه ایمیل وجود ندارد: {ex.Message}", "CopyWeb", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private const string PersianText = """
راهنمای سریع

CopyWeb ابتدا صفحات داخلی سایت را پیدا می‌کند، سپس صفحه‌ها و منابع انتخاب‌شده را برای استفاده آفلاین ذخیره می‌کند.

گزینه‌های بررسی سایت
• آدرس سایت: آدرس کامل سایت را با http یا https وارد کنید.
• حداکثر صفحه: بیشترین تعداد صفحه‌ای که در مرحله بررسی پیدا و ثبت می‌شود. برای سایت‌های بزرگ مقدار کمتری انتخاب کنید.
• عمق لینک: تعداد مرحله‌های دنبال‌کردن لینک از صفحه اصلی. مقدار ۰ فقط صفحه اصلی، مقدار ۱ لینک‌های مستقیم صفحه اصلی و مقدارهای بیشتر لایه‌های بعدی را بررسی می‌کنند.
• شامل زیردامنه‌ها: در صورت فعال بودن، زیردامنه‌های همان دامنه نیز بررسی می‌شوند.
• رعایت robots.txt: محدودیت‌های اعلام‌شده توسط سایت را رعایت می‌کند.
• خواندن Sitemap: لینک‌های موجود در sitemap.xml را هم به فهرست اضافه می‌کند.
• پیروی از Canonical: نسخه اصلی معرفی‌شده توسط سایت را در اولویت قرار می‌دهد.

گزینه‌های دانلود
• دانلود هم‌زمان: تعداد فایل‌هایی که هم‌زمان دانلود می‌شوند. مقدار بیشتر سرعت بالاتری دارد اما فشار بیشتری به شبکه و سایت وارد می‌کند.
• حداقل فضای آزاد: اگر فضای دیسک از این مقدار کمتر شود، شروع دانلودهای جدید متوقف می‌شود تا از پر شدن دیسک جلوگیری شود.
• تأخیر درخواست: فاصله بین درخواست‌ها بر حسب میلی‌ثانیه.
• تلاش مجدد: تعداد تلاش دوباره برای درخواست‌های ناموفق.
• Timeout: حداکثر زمان انتظار برای هر درخواست.

پروکسی
با فعال‌کردن احراز هویت پروکسی، نوع HTTP، HTTPS یا SOCKS5، آدرس، پورت و در صورت نیاز نام کاربری و رمز را وارد کنید. دکمه «تست پروکسی» اتصال را قبل از شروع دانلود بررسی می‌کند. اطلاعات ورود با Windows DPAPI ذخیره می‌شود.

مراحل کار
۱. روی «شروع بررسی سایت» بزنید تا لینک‌های مرتبط پیدا شوند.
۲. در پنجره لینک‌ها با دکمه + منابع هر صفحه را ببینید و موارد دلخواه را انتخاب یا حذف کنید.
۳. فهرست را ذخیره کنید یا روی «ادامه پروژه» بزنید تا دانلود آغاز شود.
۴. «توقف و ذخیره» یک checkpoint امن می‌سازد؛ بعداً از بخش «پروژه‌ها» می‌توانید دانلود را Resume کنید.

اگر سایت CAPTCHA نشان دهد، پنجره مرورگر باز می‌شود. CAPTCHA را خودتان حل کنید و سپس گزینه تأیید همه صفحه‌ها را بزنید تا عملیات ادامه پیدا کند.

خروجی
صفحه‌ها در پوشه pages و منابع در پوشه‌های Img، CSS، JS، Fonts و Files ذخیره می‌شوند. لینک‌های داخلی صفحه‌ها برای مرور آفلاین بازنویسی می‌شوند.

اگر سؤال یا پیشنهادی دارید، از دکمه «ایمیل پشتیبانی» استفاده کنید یا مستقیماً به Sassanfa@gmail.com ایمیل بزنید.
""";

    private const string EnglishText = """
Quick guide

CopyWeb first discovers internal pages, then saves the selected pages and resources for offline use.

Website scan options
• Website URL: enter the complete address including http or https.
• Page limit: the maximum number of pages collected during scanning. Use a lower value for large sites.
• Link depth: how many link levels are followed from the home page. 0 scans only the home page, 1 scans its direct links, and larger values scan deeper levels.
• Include subdomains: also scans subdomains that belong to the same site.
• Respect robots.txt: follows the site's published crawling rules.
• Read sitemap: adds links listed in sitemap.xml.
• Follow canonical: prioritizes the canonical page selected by the site.

Download options
• Concurrent downloads: how many files download at the same time. Higher values can be faster but create more network and server load.
• Minimum free disk: pauses new downloads when free disk space falls below this value.
• Request delay: wait time between requests in milliseconds.
• Retry count: how many times a failed request is retried.
• Timeout: maximum wait time for each request.

Proxy
Enable proxy authentication and choose HTTP, HTTPS or SOCKS5, then enter the address, port and optional credentials. The “Test proxy” button checks the connection before downloading. Credentials are protected with Windows DPAPI.

Workflow
1. Click “Scan website” to discover related links.
2. In the links window, use + to expand a page and select or remove its resources.
3. Save the list or click “Continue project” to start downloading.
4. “Stop and save” creates a safe checkpoint; use “Projects” later to resume the download.

If a site shows a CAPTCHA, a browser window opens. Solve it yourself, then choose the option to approve all pages so the operation can continue.

Output
Pages are saved under pages and resources under Img, CSS, JS, Fonts and Files. Internal links are rewritten for offline browsing.

For questions or suggestions, use “Email support” or write directly to Sassanfa@gmail.com.
""";
}
