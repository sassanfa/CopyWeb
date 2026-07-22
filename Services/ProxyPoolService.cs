using CopyWeb.Models;

namespace CopyWeb.Services;

public sealed record ProxyHealth(string Name, bool IsHealthy, string Message, TimeSpan Elapsed);

public static class ProxyPoolService
{
    public static async Task<IReadOnlyList<ProxyHealth>> CheckAsync(IEnumerable<ProxyProfile> profiles, CancellationToken token = default)
    {
        var results = new List<ProxyHealth>();
        foreach (var profile in profiles)
        {
            var started = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                using var session = new SiteSession(new ProxyOptions { Enabled = true, Kind = profile.Kind, Address = profile.Address, Port = profile.Port, Username = SecureStorage.Unprotect(profile.EncryptedUsername), Password = SecureStorage.Unprotect(profile.EncryptedPassword), TimeoutSeconds = 15, RetryCount = 0 });
                using var response = await session.GetAsync(new Uri("https://api.ipify.org?format=json"), token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                results.Add(new(profile.Name, true, await response.Content.ReadAsStringAsync(token).ConfigureAwait(false), started.Elapsed));
            }
            catch (Exception ex) { results.Add(new(profile.Name, false, ex.Message, started.Elapsed)); }
        }
        return results;
    }

    public static async Task<ProxyProfile?> SelectFastestAsync(IEnumerable<ProxyProfile> profiles, CancellationToken token = default)
    {
        var source = profiles.Where(x => x.Enabled && !string.IsNullOrWhiteSpace(x.Address)).ToList();
        var results = await CheckAsync(source, token).ConfigureAwait(false);
        var best = results.Where(x => x.IsHealthy).OrderBy(x => x.Elapsed).FirstOrDefault();
        return best is null ? null : source.FirstOrDefault(x => x.Name.Equals(best.Name, StringComparison.OrdinalIgnoreCase));
    }
}
