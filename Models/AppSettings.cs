namespace CopyWeb.Models;

public sealed class AppSettings
{
    public string ThemePreset { get; set; } = "آبی";
    public int PrimaryColorArgb { get; set; } = Color.FromArgb(39, 91, 219).ToArgb();
    public int BackgroundColorArgb { get; set; } = Color.FromArgb(244, 247, 251).ToArgb();
    public int SurfaceColorArgb { get; set; } = Color.White.ToArgb();
    public bool SaveDetailedLogs { get; set; } = true;
}
