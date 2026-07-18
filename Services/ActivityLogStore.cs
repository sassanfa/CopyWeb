using CopyWeb.Models;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CopyWeb.Services;

public static partial class ActivityLogStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public static string StructuredPath(string textLogPath) =>
        Path.Combine(Path.GetDirectoryName(textLogPath) ?? string.Empty, "activity.jsonl");

    public static void Append(string textLogPath, ActivityLogEntry entry)
    {
        try
        {
            var directory = Path.GetDirectoryName(textLogPath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            var human = $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{entry.Severity}]" +
                        (string.IsNullOrWhiteSpace(entry.Url) ? string.Empty : $" {entry.Url}") +
                        $" {entry.Message}" + (string.IsNullOrWhiteSpace(entry.Details) ? string.Empty : $" | {entry.Details}");
            File.AppendAllText(textLogPath, human + Environment.NewLine, Encoding.UTF8);
            File.AppendAllText(StructuredPath(textLogPath), JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine, Encoding.UTF8);
        }
        catch
        {
            // Logging must not stop a download.
        }
    }

    public static IReadOnlyList<ActivityLogEntry> Read(string textLogPath)
    {
        var structured = StructuredPath(textLogPath);
        if (File.Exists(structured))
        {
            try
            {
                return File.ReadLines(structured)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => JsonSerializer.Deserialize<ActivityLogEntry>(x, JsonOptions))
                    .Where(x => x is not null)
                    .Cast<ActivityLogEntry>()
                    .ToList();
            }
            catch { }
        }

        if (!File.Exists(textLogPath)) return [];
        return File.ReadLines(textLogPath).Select(ParseLegacy).ToList();
    }

    public static void ExportText(IEnumerable<ActivityLogEntry> entries, string fileName)
    {
        var lines = entries.Select(x => $"[{x.Timestamp:yyyy-MM-dd HH:mm:ss}] [{x.Severity}] {x.Url} {x.Message}".TrimEnd());
        File.WriteAllLines(fileName, lines, Encoding.UTF8);
    }

    public static void ExportCsv(IEnumerable<ActivityLogEntry> entries, string fileName)
    {
        static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
        var lines = new List<string> { "Timestamp,Severity,Url,Message,Details" };
        lines.AddRange(entries.Select(x => string.Join(",", Csv(x.Timestamp.ToString("O")), Csv(x.Severity.ToString()), Csv(x.Url), Csv(x.Message), Csv(x.Details ?? string.Empty))));
        File.WriteAllLines(fileName, lines, Encoding.UTF8);
    }

    public static void ExportJson(IEnumerable<ActivityLogEntry> entries, string fileName) =>
        File.WriteAllText(fileName, JsonSerializer.Serialize(entries.ToList(), new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);

    private static ActivityLogEntry ParseLegacy(string line)
    {
        var match = LegacyLineRegex().Match(line);
        var timestamp = match.Success && DateTimeOffset.TryParse(match.Groups[1].Value, out var parsed) ? parsed : DateTimeOffset.Now;
        var message = match.Success ? match.Groups[2].Value : line;
        var severity = message.Contains("خطا", StringComparison.OrdinalIgnoreCase) || message.Contains("exception", StringComparison.OrdinalIgnoreCase)
            ? ActivitySeverity.Error
            : message.Contains("هشدار", StringComparison.OrdinalIgnoreCase) || message.Contains("warning", StringComparison.OrdinalIgnoreCase)
                ? ActivitySeverity.Warning
                : message.Contains("موفق", StringComparison.OrdinalIgnoreCase) || message.Contains("success", StringComparison.OrdinalIgnoreCase)
                    ? ActivitySeverity.Success : ActivitySeverity.Info;
        return new ActivityLogEntry { Timestamp = timestamp, Severity = severity, Message = message };
    }

    [GeneratedRegex("^\\[(.+?)\\]\\s*(.*)$")]
    private static partial Regex LegacyLineRegex();
}
