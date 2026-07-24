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
            // Migrate the old built-in light themes to the new midnight-purple UI.
            // Custom themes are preserved; these exact values are the presets shipped
            // by versions before 1.3.4 (blue, slate and green).
            var oldBuiltInTheme = settings.PrimaryColorArgb == Color.FromArgb(39, 91, 219).ToArgb()
                || settings.ThemePreset == "شب بنفش" && settings.PrimaryColorArgb == Color.FromArgb(20, 25, 58).ToArgb()
                || settings.PrimaryColorArgb == Color.FromArgb(92, 112, 146).ToArgb()
                || settings.PrimaryColorArgb == Color.FromArgb(91, 130, 111).ToArgb()
                || settings.BackgroundColorArgb == Color.FromArgb(242, 250, 247).ToArgb()
                || settings.BackgroundColorArgb == Color.FromArgb(248, 246, 255).ToArgb()
                || settings.BackgroundColorArgb == Color.FromArgb(244, 247, 251).ToArgb()
                || settings.ThemePreset is "آبی" or "سبز" or "خاکستری" or "بنفش";
            if (oldBuiltInTheme)
            {
                settings.ThemePreset = "شب بنفش";
                settings.PrimaryColorArgb = Color.FromArgb(111, 82, 255).ToArgb();
                settings.BackgroundColorArgb = Color.FromArgb(8, 12, 37).ToArgb();
                settings.SurfaceColorArgb = Color.FromArgb(20, 26, 57).ToArgb();
                Save(settings);
            }
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
