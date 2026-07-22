using System.Diagnostics;
using System.Media;
using System.Net.Http.Json;

namespace CopyWeb.Services;

public static class NotificationService
{
    public static async Task NotifyAsync(string title, string message, string? webhook = null, string? email = null)
    {
        try { SystemSounds.Asterisk.Play(); } catch { }
        try
        {
            using var icon = new NotifyIcon { Icon = SystemIcons.Information, Visible = true, BalloonTipTitle = title, BalloonTipText = message };
            icon.ShowBalloonTip(3500);
            await Task.Delay(3600).ConfigureAwait(false);
        }
        catch { }
        if (!string.IsNullOrWhiteSpace(webhook))
        {
            try { using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) }; await client.PostAsJsonAsync(webhook, new { title, message }).ConfigureAwait(false); } catch { }
        }
        if (!string.IsNullOrWhiteSpace(email))
        {
            try { Process.Start(new ProcessStartInfo($"mailto:{email}?subject={Uri.EscapeDataString(title)}&body={Uri.EscapeDataString(message)}") { UseShellExecute = true }); } catch { }
        }
    }
}
