# CopyWeb CLI 1.3.6

این بسته برای اجرای CopyWeb بدون رابط گرافیکی است.

## نمونه اجرا

```powershell
.\CopyWeb-CLI.exe --cli --url https://example.com --output C:\Sites\example
```

## راهنما و آزمون

```powershell
.\CopyWeb-CLI.exe --cli --help
.\CopyWeb-CLI.exe --cli --self-test
```

## گزینه‌های اصلی

```text
--depth N              حداکثر عمق لینک
--max-pages N          حداکثر تعداد صفحه
--concurrency N        تعداد کار هم‌زمان
--per-domain N         اتصال هم‌زمان برای هر دامنه
--speed-kbps N         سقف سرعت؛ صفر یعنی نامحدود
--delay-ms N           تأخیر بین درخواست‌ها
--resume               ادامه از links.json موجود
--timeout N            Timeout بر حسب ثانیه
--retry N              تعداد تلاش مجدد
```

## پروکسی

```text
--proxy HOST
--proxy-kind http|https|socks5
--proxy-port N
--proxy-user USER
--proxy-password PASS
```

برای جلوگیری از باقی‌ماندن رمز در History ترمینال، رمز پروکسی را مستقیماً در خط فرمان عمومی یا سیستم مشترک وارد نکنید.

## تصاویر مدرن

نسخهٔ 1.3.6 منابع WebP و AVIF را از HTML، `srcset`، CSS، داده‌های lazy-load و آدرس‌های بدون پسوند شناسایی می‌کند. محتوای فایل نیز بررسی می‌شود تا پاسخ HTML خطا به‌اشتباه به‌عنوان تصویر ذخیره نشود.
