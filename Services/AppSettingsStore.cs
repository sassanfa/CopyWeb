using CopyWeb.Models;
using System.Text.Json;

namespace CopyWeb.Services;

public static class AppSettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CopyWeb");

    public static string FilePath => Path.Combine(DirectoryPath, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new AppSettings();
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options) ?? new AppSettings();
            // Migrate the original saturated blue default to the calmer slate palette.
            if (settings.PrimaryColorArgb == Color.FromArgb(39, 91, 219).ToArgb())
                settings.PrimaryColorArgb = Color.FromArgb(92, 112, 146).ToArgb();
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(DirectoryPath);
        var temporary = Path.Combine(DirectoryPath, $".settings.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 16 * 1024, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(JsonSerializer.Serialize(settings, Options));
                writer.Flush();
                stream.Flush(true);
            }
            if (File.Exists(FilePath))
            {
                try { File.Replace(temporary, FilePath, null, true); }
                catch (IOException) { File.Move(temporary, FilePath, true); }
            }
            else File.Move(temporary, FilePath);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
        }
    }
}
