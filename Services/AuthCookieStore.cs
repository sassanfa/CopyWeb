using CopyWeb.Models;
using System.Text.Json;

namespace CopyWeb.Services;

public static class AuthCookieStore
{
    public static void Save(string fileName, IEnumerable<BrowserCookie> cookies)
    {
        var json = JsonSerializer.Serialize(cookies.ToList());
        var encrypted = SecureStorage.Protect(json) ?? throw new InvalidOperationException("ذخیره‌ی نشست انجام نشد.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(fileName))!);
        File.WriteAllText(fileName, encrypted);
    }

    public static IReadOnlyList<BrowserCookie> Load(string fileName)
    {
        try
        {
            if (!File.Exists(fileName)) return [];
            var json = SecureStorage.Unprotect(File.ReadAllText(fileName));
            return string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<BrowserCookie>>(json) ?? [];
        }
        catch { return []; }
    }
}
