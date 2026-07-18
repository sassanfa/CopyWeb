using System.Text;

namespace CopyWeb.Services;

public static class CrashLogger
{
    public static string DirectoryPath => Path.Combine(AppSettingsStore.DirectoryPath, "crashes");
    public static string LatestFilePath => Path.Combine(DirectoryPath, "crash.log");

    public static void Install()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, args) => Write(args.Exception, "UI thread");
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Write(args.ExceptionObject as Exception ?? new Exception(Convert.ToString(args.ExceptionObject)), "AppDomain");
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Write(args.Exception, "Unobserved task");
            args.SetObserved();
        };
    }

    public static void Write(Exception exception, string source)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var text = new StringBuilder()
                .AppendLine($"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] {source}")
                .AppendLine(exception.ToString())
                .AppendLine(new string('-', 80))
                .ToString();
            File.AppendAllText(LatestFilePath, text, Encoding.UTF8);
        }
        catch
        {
            // A crash logger must never become the cause of another crash.
        }
    }
}
