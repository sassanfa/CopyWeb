# ساخت فایل نصبی CopyWeb

1. ابتدا خروجی منتشرشده را بسازید:

```powershell
dotnet publish ..\CopyWeb.csproj --configuration Release --runtime win-x64 --self-contained true --output ..\bin\Release\net10.0-windows\publish-v49 /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

2. Inno Setup 6 را نصب کنید.
3. فایل `CopyWeb.iss` را با Inno Setup Compiler باز و روی **Compile** کلیک کنید.

فایل Setup در پوشه‌ی `installer\output` با نام `CopyWeb-Setup-1.3.2.exe` ساخته می‌شود.
