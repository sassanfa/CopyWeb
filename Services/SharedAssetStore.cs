using System.Security.Cryptography;
using System.Runtime.InteropServices;

namespace CopyWeb.Services;

/// <summary>Content-addressed shared image store. Projects receive a hard-link when Windows allows it.</summary>
public static class SharedAssetStore
{
    public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CopyWeb", "SharedAssets");

    public static async Task<string> MaterializeAsync(string hash, string extension, byte[] bytes, string projectPath, CancellationToken token = default)
    {
        Directory.CreateDirectory(Root);
        var safeExt = string.IsNullOrWhiteSpace(extension) ? ".bin" : extension.StartsWith('.') ? extension : "." + extension;
        var shared = Path.Combine(Root, hash + safeExt.ToLowerInvariant());
        if (!File.Exists(shared))
        {
            var temp = shared + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllBytesAsync(temp, bytes, token).ConfigureAwait(false);
            File.Move(temp, shared, true);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        if (!File.Exists(projectPath))
        {
            try { File.CreateSymbolicLink(projectPath, shared); }
            catch
            {
                if (!CreateHardLink(projectPath, shared, IntPtr.Zero))
                {
                    await using var source = File.OpenRead(shared); await using var target = File.Create(projectPath); await source.CopyToAsync(target, token).ConfigureAwait(false);
                }
            }
        }
        return projectPath;
    }

    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLink(string fileName, string existingFileName, IntPtr securityAttributes);
}
