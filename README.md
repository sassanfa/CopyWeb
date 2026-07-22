# CopyWeb

CopyWeb یک برنامه‌ی Windows Forms برای تهیه‌ی نسخه‌ی آفلاین از سایت‌ها و صفحات داخلی مرتبط آن‌هاست.

![Version](https://img.shields.io/badge/version-1.3.1-2563EB)
![Platform](https://img.shields.io/badge/platform-Windows-0F766E)

**Created by:** SassanFa — [Sassanfa@gmail.com](mailto:Sassanfa@gmail.com)  
**Version:** 1.3.1  
**Release date:** 2026-07-22

## قابلیت‌ها

- دریافت سایت از یک URL و شناسایی صفحات داخلی همان دامنه
- محدودکردن عمق بررسی، تعداد صفحات و زیردامنه‌ها
- پشتیبانی از `robots.txt`، Sitemap و لینک‌های Canonical
- حذف لینک‌های خارجی و فیلترکردن URLهای غیرصفحه مانند تصویر، CSS، JS و ویدئو از فهرست صفحات
- نمایش درختی صفحات و منابع با دکمه‌ی `+ / −`
- نمایش تصاویر، CSS، JavaScript، فونت، رسانه و فایل‌های هر صفحه زیر همان صفحه
- انتخاب یا لغو انتخاب مستقل هر منبع قبل از دانلود
- ذخیره‌ی صفحات در `pages` و منابع در پوشه‌های `Img`، `CSS`، `JS`، `Fonts` و `Files`
- بازنویسی لینک‌های داخلی برای مرور آفلاین
- پشتیبانی گسترده از تصاویر Lazy، `srcset`، `picture/source`، `data-*` و WebP/AVIF
- نمایش URL فعلی، مرحله‌ی عملیات، درصد فایل، درصد کل پروژه و تعداد موفق/ناموفق
- نمایش سرعت دانلود، حجم دریافت‌شده و زمان تقریبی پایان
- توقف، ادامه و Resume از checkpointهای اتمیک
- فیلتر و جست‌وجو بین صفحات و منابع و عملیات گروهی
- پروکسی HTTP، HTTPS و SOCKS5 با تست اتصال، Timeout، Retry و تأخیر درخواست
- رمزنگاری نام کاربری و رمز پروکسی با Windows DPAPI
- پشتیبانی از User-Agent، Header و Cookie سفارشی
- مدیریت CAPTCHA با نمایش مرورگر و گزینه‌ی «تأیید همه صفحات»
- ورود به سایت با WebView2 و استفاده از Cookie/Session کاربر برای صفحات خصوصی
- اعتبارسنجی آرشیو، Snapshot Versioning، Visual Diff و جست‌وجوی متن صفحات ذخیره‌شده
- بارگذاری پروژه برای ویرایش URL بدون Resume خودکار و استفاده از آدرس اصلاح‌شده
- محدودیت اتصال هم‌زمان برای هر دامنه در کنار سقف کلی دانلود
- پروفایل‌های ذخیره‌شده‌ی پروکسی با رمزنگاری DPAPI و انتخاب سریع
- گزارش فعالیت با فیلتر موفقیت، اطلاعات، هشدار و خطا
- خروجی گزارش در TXT، CSV و JSON و ثبت Crash Log
- حالت تاریک، تم‌های رنگی و تغییر زبان فارسی/انگلیسی بدون Restart
- ذخیره و مدیریت پروژه‌ها از بخش Projects
- کپی و تغییر نام پروژه، پشتیبان‌گیری ZIP و بازیابی امن
- زمان‌بندی اجرای یک‌باره با Windows Task Scheduler
- Drag & Drop برای واردکردن URL از متن یا فایل
- فیلتر زنده، جست‌وجو، نمودار پیشرفت و بازکردن URL خطادار از مانیتور دانلود
- API محلی اختیاری روی `127.0.0.1` برای وضعیت، پروژه‌ها و توقف عملیات
- حالت ساده برای واردکردن فقط URL و شروع دانلود خودکار، همراه با دکمه‌ی حالت پیشرفته برای نمایش همه تنظیمات
- دکمه‌ی «پیشرفته» با رنگ قرمز و متن سفید، برای تشخیص سریع و جابه‌جایی بین حالت ساده و حرفه‌ای

## شروع سریع

1. فایل `CopyWeb.slnx` را با Visual Studio باز کنید.
2. بسته‌های NuGet را Restore و پروژه‌ی `CopyWeb` را Build کنید.
3. برنامه را اجرا کنید.
4. URL سایت و پوشه‌ی خروجی را وارد کنید.
5. عمق لینک و حداکثر تعداد صفحات را مشخص کنید.
6. در صورت نیاز پروکسی را فعال و با **Test Proxy** آزمایش کنید.
7. روی **شروع بررسی سایت** بزنید.
8. در پنجره‌ی لینک‌ها، با `+` منابع هر صفحه را باز کنید و موارد دلخواه را انتخاب یا حذف کنید.
9. روی **دانلود موارد انتخاب‌شده** کلیک کنید.

اگر CAPTCHA شناسایی شود، عملیات متوقف می‌شود. CAPTCHA را در مرورگر داخلی حل کنید و روی **تأیید همه صفحات** بزنید تا در همان عملیات دوباره پنجره‌ی CAPTCHA باز نشود.

## Resume و پروژه‌های قدیمی

اطلاعات پروژه و وضعیت دانلود در `links.json` ذخیره می‌شود. برای ادامه‌ی یک عملیات متوقف‌شده، از **Resume Project** یا بخش **Projects** استفاده کنید.

پروژه‌های ساخته‌شده قبل از نسخه‌ی 1.2.0 ممکن است metadata منابع را نداشته باشند؛ برای دیدن درخت منابع، یک‌بار بررسی سایت را با نسخه‌ی جدید انجام دهید.

## ساختار خروجی

```text
ProjectFolder/
├── index.html
├── pages/
├── Img/
├── CSS/
├── JS/
├── Fonts/
├── Files/
├── links.json          # checkpoint و فهرست صفحات و منابع
├── activity.log        # گزارش متنی فعالیت
├── activity.jsonl      # گزارش ساختاریافته برای فیلتر و خروجی
├── assets-manifest.json # نگاشت URL/هش منابع برای جلوگیری از دریافت تکراری
└── download-log.txt    # خلاصه‌ی دانلود
```

## تنظیمات پروکسی

پروکسی انتخاب‌شده برای بررسی لینک‌ها، دانلود صفحات و دریافت منابع استفاده می‌شود و فقط مخصوص یک مرحله نیست.

پروتکل‌های قابل استفاده:

- HTTP
- HTTPS
- SOCKS5

اطلاعات ورود پروکسی با Windows DPAPI برای کاربر فعلی ویندوز رمزنگاری می‌شود.

## نیازمندی‌ها

- Windows 10 یا بالاتر
- Visual Studio با workload مربوط به .NET Desktop
- .NET 10 SDK برای Build از سورس
- Microsoft WebView2 Runtime برای CAPTCHA و حالت JavaScript/SPA

پروژه از `AngleSharp` و `Microsoft.Web.WebView2` از طریق NuGet استفاده می‌کند و روی `net10.0-windows` ساخته می‌شود.

## ساخت فایل اجرایی

```powershell
dotnet publish CopyWeb.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --output bin\Release\net10.0-windows\publish `
  /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true
```

فایل آیکون برنامه `CopyWeb.ico` است و همراه پروژه در repository قرار دارد.

## ساخت فایل نصبی

اسکریپت Inno Setup در `installer\CopyWeb.iss` قرار دارد. ابتدا publish را طبق دستور بالا بسازید، سپس فایل را با Inno Setup 6 باز و Compile کنید. راهنمای کامل در `installer\README.md` است و خروجی با نام `CopyWeb-Setup-1.3.1.exe` ساخته می‌شود.

فایل‌های Release نسخه 1.3.1 در پوشه‌ی `releases/1.3.1` قرار دارند:

- `CopyWeb-Setup-1.3.1.exe` — نصب‌کننده‌ی Windows
- `CopyWeb.exe` — اجرای Portable تک‌فایلی
- `CopyWeb-Portable.exe` — نام جایگزین نسخه‌ی Portable
- `CopyWeb-Portable-1.3.1.zip` — بسته‌ی Portable
- `CLI.md` — راهنمای کامل خط فرمان
- `RELEASE_NOTES-1.3.1.md` — متن Release

## CLI و API محلی

برای اجرای بدون رابط گرافیکی:

```powershell
CopyWeb.exe --cli --url https://example.com --output C:\Sites\example --depth 3 --max-pages 500 --concurrency 4 --speed-kbps 0
CopyWeb.exe --cli --self-test
CopyWeb.exe --cli --url https://example.com --output C:\Sites\example --resume
CopyWeb.exe --cli --url https://example.com --output C:\Sites\example --proxy 127.0.0.1 --proxy-kind socks5 --proxy-port 1080 --proxy-user user --proxy-password pass
```

برای دیدن همه گزینه‌ها `CopyWeb.exe --cli --help` را اجرا کنید. پارامترهای مهم شامل `--per-domain`، `--timeout`، `--retry`، `--delay-ms` و `--resume` هستند.

در تنظیمات برنامه می‌توان API محلی را فعال کرد. این API فقط روی `127.0.0.1` گوش می‌دهد و مسیرهای `GET /api/status`، `GET /api/projects` و `POST /api/stop` را ارائه می‌کند.

## ساختار Repository

```text
CopyWeb/
├── Models/             # مدل صفحات، منابع و تنظیمات
├── Services/           # crawler، downloader، proxy و storage
├── MainForm.cs         # پنجره‌ی اصلی و داشبورد
├── LinksForm.cs        # صفحات و منابع با نمایش درختی
├── ProjectsForm.cs     # پروژه‌های ذخیره‌شده
├── OfflinePreviewForm.cs # پیش‌نمایش localhost و لینک‌های خراب
├── WatchForm.cs         # بررسی دوره‌ای و دانلود افزایشی
├── DashboardForm.cs     # داشبورد وضعیت و حجم فایل‌ها
├── PublishForm.cs       # ZIP، IIS، FTP/SFTP
├── ReportsForm.cs      # گزارش فعالیت
├── SettingsForm.cs     # تم، زبان و تنظیمات برنامه
└── AboutForm.cs        # اطلاعات برنامه
```

تست سریع خودکار از مسیر `Tests\\Run-SelfTest.ps1` اجرا می‌شود و build، نرمال‌سازی URL/منابع و Backup/Restore پروژه را بررسی می‌کند.

## نکات استفاده

- فقط سایت‌هایی را دانلود کنید که مالک آن هستید یا اجازه‌ی آرشیو آن‌ها را دارید.
- محدودیت‌های سایت، احراز هویت، Rate Limit و CAPTCHA ممکن است روی نتیجه اثر بگذارد.
- منابعی که در پنجره‌ی لینک‌ها لغو انتخاب شوند، در صفحه‌ی آفلاین محلی‌سازی نمی‌شوند.
- پروکسی برنامه در تمام مراحل بررسی و دانلود مشترک است.

## Release 1.3.1 highlights

- Advanced mode is clearly marked with a red button and white text; simple mode remains uncluttered for first-time users.
- Archive validation detects empty/corrupt files, broken local links and inconsistent manifests.
- Snapshot versioning stores timestamped SHA-256 manifests; Visual Diff highlights added, removed and changed files.
- Full-text search scans saved HTML pages and opens the matching file on double-click.
- Loading a project for editing no longer starts Resume or overwrites a corrected URL.
- Lazy, responsive and inline image discovery now covers common `data-*` attributes, `srcset`, `picture/source`, WebP and AVIF.
- Simple mode lets a new user enter only a URL and start an automatic download; advanced mode exposes every setting.

## Release 1.3.0 highlights

- Bounded parallel downloads (1-16 workers) with a visible active/queued count.
- Per-page progress, aggregate progress, transfer speed, expected bytes and free disk space.
- Atomic checkpoint after each completed page; stop/resume keeps pending work safe.
- Disk-space guard prevents starting when the selected drive is below the configured minimum.
- Shared asset locking prevents duplicate concurrent downloads of the same image, CSS or script.
- Asset de-duplication prevents repeated image URLs, fragment variants and common cache-busting query variants from being requested more than once in a run.
- Identical image bytes found under different URLs are stored only once and all pages point to the same local file.
- `assets-manifest.json` is written atomically so resume runs reuse already downloaded assets instead of fetching them again.
- Sidebar branding is fully visible at the default window size.
- Version checker in About can compare the latest GitHub release and open the repository only after user confirmation.
- Projects view shows each storage folder and supports confirmed `X` deletion of a project and its downloaded files.
- URL input includes a one-click Clipboard paste button.
- Sidebar navigation now uses clear home, globe, gear, chart and info icons.
- All application buttons use rounded corners and a calm low-saturation slate palette.
- Proxy testing is presented as a visible rounded action button alongside the download controls.
- URL input now uses a rounded bordered field with a dedicated Clipboard paste action.
- Project and current-file progress captions are spaced below the status summary so the text stays fully readable.
- New Tutorial window explains scan, depth, page limit, proxy, resume, CAPTCHA, output folders and resource selection; it also links to support email.
- Project information labels are aligned to the opposite side for clearer RTL reading.
- Proxy credential fields stay disabled until proxy is enabled; the proxy test button turns muted green after a successful connection.
- URL input uses a clear bordered field for reliable visibility in the Windows Forms designer and runtime.
- Proxy profiles, per-domain connection limits, scheduled one-time downloads and project copy/rename are available.
- Download Monitor includes search, state filtering, a progress chart, URL double-click navigation and per-link retry/stop.
- Optional localhost API exposes `/api/status`, `/api/projects` and `/api/stop` without opening a network port externally.
- Drag & Drop accepts a URL or a text file whose first line is a URL.
- Offline preview server runs archived pages on `127.0.0.1` and checks broken local links before delivery.
- Watch mode stores page hashes and launches an incremental `--resume` download only when content changes.
- Images use a content-addressed shared repository under `%LOCALAPPDATA%\CopyWeb\SharedAssets`; projects use links/hard-links when Windows permits.
- Before/after rendered screenshots can be captured into the project `screenshots` folder.
- Completion notification includes Windows balloon notification and system sound; webhook and mailto hooks are available in the notification service.
- Project dashboard summarizes page state, file count and size by type.
- Publish tools create ZIP archives, prepare an IIS-ready folder and upload a project to FTP/SFTP (SFTP uses Windows OpenSSH and an SSH key).
- Saved proxy profiles are health-checked and rotated between requests when more than one enabled profile is available.

## English summary

CopyWeb is a Windows Forms website archiver. It crawls internal pages, filters external and non-page URLs, shows each page’s images/CSS/JavaScript/media in an expandable `+ / −` resource tree, lets the user select resources independently, rewrites links for offline browsing, supports authenticated WebView2 sessions, resume checkpoints, HTTP/HTTPS/SOCKS5 proxies, DPAPI credential protection, CAPTCHA handoff, detailed reports, themes, and Persian/English localization.

**CopyWeb Created by SassanFa**  
**Email:** [Sassanfa@gmail.com](mailto:Sassanfa@gmail.com)  
**Version:** 1.3.1

## مجوز و مسئولیت استفاده

کاربر مسئول رعایت قوانین، مجوز دسترسی و شرایط استفاده‌ی سایت مقصد است. این پروژه برای آرشیو مجاز و استفاده‌ی قانونی ارائه شده است.
