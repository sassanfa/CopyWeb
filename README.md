# CopyWeb

CopyWeb یک برنامه‌ی Windows Forms برای تهیه‌ی نسخه‌ی آفلاین از سایت‌ها و صفحات داخلی مرتبط آن‌هاست.

![Version](https://img.shields.io/badge/version-1.0.18-2563EB)
![Platform](https://img.shields.io/badge/platform-Windows-0F766E)

**Created by:** SassanFa — [Sassanfa@gmail.com](mailto:Sassanfa@gmail.com)  
**Version:** 1.0.18  
**Release date:** 2026-07-18

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
- پشتیبانی از تصاویر Lazy و `srcset`
- نمایش URL فعلی، مرحله‌ی عملیات، درصد فایل، درصد کل پروژه و تعداد موفق/ناموفق
- نمایش سرعت دانلود، حجم دریافت‌شده و زمان تقریبی پایان
- توقف، ادامه و Resume از checkpointهای اتمیک
- فیلتر و جست‌وجو بین صفحات و منابع و عملیات گروهی
- پروکسی HTTP، HTTPS و SOCKS5 با تست اتصال، Timeout، Retry و تأخیر درخواست
- رمزنگاری نام کاربری و رمز پروکسی با Windows DPAPI
- پشتیبانی از User-Agent، Header و Cookie سفارشی
- مدیریت CAPTCHA با نمایش مرورگر و گزینه‌ی «تأیید همه صفحات»
- گزارش فعالیت با فیلتر موفقیت، اطلاعات، هشدار و خطا
- خروجی گزارش در TXT، CSV و JSON و ثبت Crash Log
- حالت تاریک، تم‌های رنگی و تغییر زبان فارسی/انگلیسی بدون Restart
- ذخیره و مدیریت پروژه‌ها از بخش Projects

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

ساختار منابع در پروژه‌هایی که قبل از نسخه‌ی 1.0.18 ساخته شده‌اند ذخیره نشده است. برای دیدن درخت منابع، یک‌بار بررسی سایت را با نسخه‌ی جدید انجام دهید.

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

## ساختار Repository

```text
CopyWeb/
├── Models/             # مدل صفحات، منابع و تنظیمات
├── Services/           # crawler، downloader، proxy و storage
├── MainForm.cs         # پنجره‌ی اصلی و داشبورد
├── LinksForm.cs        # صفحات و منابع با نمایش درختی
├── ProjectsForm.cs     # پروژه‌های ذخیره‌شده
├── ReportsForm.cs      # گزارش فعالیت
├── SettingsForm.cs     # تم، زبان و تنظیمات برنامه
└── AboutForm.cs        # اطلاعات برنامه
```

## نکات استفاده

- فقط سایت‌هایی را دانلود کنید که مالک آن هستید یا اجازه‌ی آرشیو آن‌ها را دارید.
- محدودیت‌های سایت، احراز هویت، Rate Limit و CAPTCHA ممکن است روی نتیجه اثر بگذارد.
- منابعی که در پنجره‌ی لینک‌ها لغو انتخاب شوند، در صفحه‌ی آفلاین محلی‌سازی نمی‌شوند.
- پروکسی برنامه در تمام مراحل بررسی و دانلود مشترک است.

## English summary

CopyWeb is a Windows Forms website archiver. It crawls internal pages, filters external and non-page URLs, shows each page’s images/CSS/JavaScript/media in an expandable `+ / −` resource tree, lets the user select resources independently, rewrites links for offline browsing, and supports resume checkpoints, HTTP/HTTPS/SOCKS5 proxies, DPAPI credential protection, CAPTCHA handoff, detailed reports, themes, and Persian/English localization.

**CopyWeb Created by SassanFa**  
**Email:** [Sassanfa@gmail.com](mailto:Sassanfa@gmail.com)  
**Version:** 1.0.18

## مجوز و مسئولیت استفاده

کاربر مسئول رعایت قوانین، مجوز دسترسی و شرایط استفاده‌ی سایت مقصد است. این پروژه برای آرشیو مجاز و استفاده‌ی قانونی ارائه شده است.
