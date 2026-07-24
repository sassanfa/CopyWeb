# نصب CopyWeb 1.3.6

## روش پیشنهادی

فایل `CopyWeb-Setup-1.3.6.exe` را اجرا کنید. میانبر برنامه در Start Menu و Desktop ساخته می‌شود.

## نسخه Portable

فایل `CopyWeb-Portable-1.3.6.zip` را Extract و `CopyWeb-Portable.exe` را اجرا کنید. این نسخه self-contained است و به نصب جداگانهٔ .NET نیاز ندارد.

## نسخه معمولی

فایل `CopyWeb-Windows-x64-1.3.6.zip` را Extract و `CopyWeb.exe` را اجرا کنید.

## نسخه CLI

فایل `CopyWeb-CLI-1.3.6.zip` را Extract کنید. نمونه:

```powershell
.\CopyWeb-CLI.exe --cli --url https://example.com --output C:\Sites\example
```

## پیش‌نیاز مرورگر داخلی

برای ورود به سایت، صفحات SPA، ذخیره زنده و کپی وبی، Microsoft Edge WebView2 Runtime باید روی Windows نصب باشد. نسخهٔ 1.3.6 داده‌های WebView2 را در مسیر مجاز LocalAppData می‌سازد تا خطای `E_ACCESSDENIED` رخ ندهد.

## پیش‌نمایش آفلاین

پیش‌نمایش امن روی localhost اجرا می‌شود. اطلاعات ورود محلی:

```text
Username: admin
Password: admin
```

این حساب فقط برای آرشیو آفلاین است و به حساب اصلی وب‌سایت ارتباطی ندارد.
