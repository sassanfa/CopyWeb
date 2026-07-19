using System.Diagnostics;

namespace CopyWeb.Services;

public static class ScheduleService
{
    public static async Task CreateOneTimeAsync(string name, Uri url, string output, DateTime when, CancellationToken token = default)
    {
        if (when <= DateTime.Now.AddMinutes(1)) throw new ArgumentException("زمان‌بندی باید حداقل یک دقیقه بعد باشد.");
        var taskName = "CopyWeb\\" + UrlTools.CleanName(name, "scheduled-download");
        var bundled = Path.Combine(AppContext.BaseDirectory, "CopyWeb.exe");
        var exe = File.Exists(bundled) ? bundled : Environment.ProcessPath ?? throw new InvalidOperationException("مسیر فایل اجرایی پیدا نشد.");
        var psi = new ProcessStartInfo("schtasks.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        psi.ArgumentList.Add("/Create"); psi.ArgumentList.Add("/TN"); psi.ArgumentList.Add(taskName); psi.ArgumentList.Add("/SC"); psi.ArgumentList.Add("ONCE"); psi.ArgumentList.Add("/SD"); psi.ArgumentList.Add(when.ToString("MM/dd/yyyy")); psi.ArgumentList.Add("/ST"); psi.ArgumentList.Add(when.ToString("HH:mm")); psi.ArgumentList.Add("/F"); psi.ArgumentList.Add("/TR");
        psi.ArgumentList.Add($"\"{exe}\" --cli --scheduled --url \"{url.AbsoluteUri}\" --output \"{output}\"");
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("زمان‌بندی ویندوز اجرا نشد.");
        await process.WaitForExitAsync(token).ConfigureAwait(false);
        if (process.ExitCode != 0) throw new InvalidOperationException((await process.StandardError.ReadToEndAsync(token).ConfigureAwait(false)).Trim());
    }

    public static void Delete(string name)
    {
        var psi = new ProcessStartInfo("schtasks.exe", $"/Delete /TN \"CopyWeb\\{UrlTools.CleanName(name, "scheduled-download")}\" /F") { UseShellExecute = false, CreateNoWindow = true };
        using var process = Process.Start(psi); process?.WaitForExit(10_000);
    }
}
