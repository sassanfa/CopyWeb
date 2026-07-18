using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace CopyWeb.Services;

/// <summary>
/// Encrypts small secrets for the current Windows user. The encrypted value can
/// be stored in the settings file, but it cannot be decrypted by another user.
/// </summary>
public static class SecureStorage
{
    private const int CryptProtectUiForbidden = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int Length;
        public IntPtr Data;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(
        ref DataBlob input,
        string? description,
        IntPtr entropy,
        IntPtr reserved,
        IntPtr prompt,
        int flags,
        ref DataBlob output);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(
        ref DataBlob input,
        StringBuilder? description,
        IntPtr entropy,
        IntPtr reserved,
        IntPtr prompt,
        int flags,
        ref DataBlob output);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr handle);

    public static string? Protect(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        var bytes = Encoding.UTF8.GetBytes(value);
        var input = CreateBlob(bytes);
        try
        {
            var output = new DataBlob();
            if (!CryptProtectData(ref input, "CopyWeb secret", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, ref output))
                throw new InvalidOperationException("Windows DPAPI نتوانست اطلاعات را رمزنگاری کند.");
            try
            {
                var encrypted = new byte[output.Length];
                Marshal.Copy(output.Data, encrypted, 0, encrypted.Length);
                return Convert.ToBase64String(encrypted);
            }
            finally { LocalFree(output.Data); }
        }
        finally { FreeBlob(input); }
    }

    public static string? Unprotect(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            var bytes = Convert.FromBase64String(value);
            var input = CreateBlob(bytes);
            try
            {
                var output = new DataBlob();
                if (!CryptUnprotectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, ref output))
                    return null;
                try
                {
                    var plain = new byte[output.Length];
                    Marshal.Copy(output.Data, plain, 0, plain.Length);
                    return Encoding.UTF8.GetString(plain);
                }
                finally { LocalFree(output.Data); }
            }
            finally { FreeBlob(input); }
        }
        catch (FormatException) { return null; }
        catch (SecurityException) { return null; }
        catch (InvalidOperationException) { return null; }
    }

    private static DataBlob CreateBlob(byte[] bytes)
    {
        var pointer = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, pointer, bytes.Length);
        return new DataBlob { Length = bytes.Length, Data = pointer };
    }

    private static void FreeBlob(DataBlob blob)
    {
        if (blob.Data != IntPtr.Zero) Marshal.FreeHGlobal(blob.Data);
    }
}
