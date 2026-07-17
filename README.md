# CopyWeb

CopyWeb is a Windows Forms application for creating an offline copy of a website.
CopyWeb is a modern Windows Forms application for creating an offline copy of a website and its related internal pages.

> نسخه فارسی در ادامه آمده است.

## What it does

CopyWeb starts from a URL, discovers links that belong to the selected site, removes unrelated external links, and downloads the selected pages and their resources into a structured local project.

- Crawls pages with configurable maximum depth and page limit
- Keeps links within the selected domain and optionally its subdomains
- Respects `robots.txt` when enabled
- Downloads pages and rewrites local links for offline browsing
- Stores images in `Img`, stylesheets in `CSS`, JavaScript in `JS`, fonts in `Fonts`, and other resources in `Files`
- Shows the current URL and per-file progress percentage
- Supports pause, stop, and resume from the last saved checkpoint
- Uses the same proxy session for link discovery, page downloads, and resources
- Includes a proxy connection test, optional authentication, and CAPTCHA handoff to the user
- Saves projects, activity logs, reports, and application settings
- Includes theme presets and custom colors

## Quick start

1. Open `CopyWeb.slnx` in Visual Studio.
2. Restore NuGet packages and build the `CopyWeb` project.
3. Run the application.
4. Enter the starting URL, choose the depth, page limit, and output folder.
5. Configure the proxy if required and use **Test Proxy** to verify it.
6. Start the site scan, review the discovered links, then continue with the download.

If a CAPTCHA is detected, CopyWeb pauses the operation and displays the browser view. Solve the CAPTCHA manually and return to the application to continue.

## Requirements

- Windows 10 or later
- Visual Studio with the .NET Windows Desktop workload
- .NET 10 SDK for building from source
- Microsoft WebView2 Runtime for the CAPTCHA browser view

The project targets `net10.0-windows` and uses AngleSharp and Microsoft WebView2 through NuGet.

## Project output

Each downloaded site is saved in the output directory selected by the user:

```text
ProjectFolder/
├── index.html
├── pages/
├── Img/
├── CSS/
├── JS/
├── Fonts/
├── Files/
├── links.json          # resume checkpoint and discovered links
├── activity.log        # detailed application activity
└── download-log.txt    # download summary
```

Saved project locations are also indexed under `%AppData%\CopyWeb`, so they can be opened later from the **Projects** section.

## Repository layout

```text
CopyWeb/
├── Models/             # data models and settings
├── Services/           # crawler, downloader, proxy session, and storage
├── MainForm.cs         # main application window
├── ProjectsForm.cs     # saved projects
├── ReportsForm.cs      # activity reports
├── SettingsForm.cs     # theme and application settings
└── AboutForm.cs        # application information
```

## Notes

- Use the crawler only for websites that you own or are authorized to archive.
- Website restrictions, authentication, rate limits, and CAPTCHA challenges may affect the result.
- A proxy configured in the application is shared by crawling and downloading; it is not a separate download-only option.

## Features
## About

- Same proxy session for crawling, page downloads, and assets
- Crawl depth, page limit, subdomain and robots.txt options
- CAPTCHA handoff to the user through an embedded browser
- Pause/resume checkpoints using `links.json`
- Download progress for the current file and total project
- Assets organized into `Img`, `CSS`, `JS`, `Fonts`, and `Files`
- Saved projects, activity reports, theme presets, and custom colors
**CopyWeb Created by SassanFa**  
**Version:** 1.0.13  
**Email:** [Sassanfa@gmail.com](mailto:Sassanfa@gmail.com)

---

## Build
## معرفی فارسی

Open `CopyWeb.slnx` in Visual Studio with the .NET 10 Windows Desktop workload, then build the `CopyWeb` project.
CopyWeb یک برنامه‌ی Windows Forms برای تهیه‌ی نسخه‌ی آفلاین از سایت‌هاست. برنامه از آدرس شروع، لینک‌های داخلی مربوط به همان سایت را پیدا می‌کند، لینک‌های خارجی را کنار می‌گذارد و صفحات و منابع سایت را در پوشه‌های مرتب ذخیره می‌کند.

The project targets `net10.0-windows` and uses the AngleSharp and WebView2 NuGet packages.
امکانات اصلی شامل عمق و سقف تعداد صفحات، رعایت اختیاری `robots.txt`، فیلتر دامنه و زیر‌دامنه، نمایش دقیق URL و درصد پیشرفت، توقف و ادامه‌ی دانلود، ذخیره‌ی checkpoint، تنظیم و تست پروکسی، ورود دستی CAPTCHA، مدیریت پروژه‌ها، گزارش فعالیت و تغییر رنگ محیط برنامه است.

