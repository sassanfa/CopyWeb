using CopyWeb.Services;

namespace CopyWeb
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Any(x => x.Equals("--cli", StringComparison.OrdinalIgnoreCase)))
            {
                Environment.ExitCode = CliRunner.RunAsync(args).GetAwaiter().GetResult();
                return;
            }
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            CrashLogger.Install();
            Application.Run(new MainForm());
        }
    }
}
