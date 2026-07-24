using CopyWeb.Services;
using CopyWeb.Models;
using CopyWebLinkState = CopyWeb.Models.LinkState;

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
#if DEBUG
            if (args.Length >= 3 && args[0].Equals("--qa-theme-preview", StringComparison.OrdinalIgnoreCase))
            {
                ApplicationConfiguration.Initialize();
                RunThemePreviewQa(args[1], args[2]);
                return;
            }
            if (args.Length >= 3 && args[0].Equals("--qa-site-map", StringComparison.OrdinalIgnoreCase))
            {
                ApplicationConfiguration.Initialize();
                RunSiteMapQa(args[1], args[2]);
                return;
            }
            if (args.Length >= 3 && args[0].Equals("--qa-live-webp", StringComparison.OrdinalIgnoreCase))
            {
                ApplicationConfiguration.Initialize();
                RunLiveWebpQa(args[1], args[2]);
                return;
            }
#endif
            if (args.Any(x => x.Equals("--cli", StringComparison.OrdinalIgnoreCase)))
            {
                CrashLogger.Install();
                try
                {
                    Environment.ExitCode = CliRunner.RunAsync(args).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    CrashLogger.Write(ex, "CLI");
                    Console.Error.WriteLine(ex);
                    Environment.ExitCode = 1;
                }
                return;
            }
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            CrashLogger.Install();
            Application.Run(new StartupApplicationContext());
        }

        private sealed class StartupApplicationContext : ApplicationContext
        {
            private readonly SplashForm _splash = new();
            private readonly DateTime _splashStarted = DateTime.UtcNow;

            public StartupApplicationContext()
            {
                MainForm = _splash;
                _splash.Shown += (_, _) => _splash.BeginInvoke((Action)StartMainForm);
                _splash.Show();
            }

            private void StartMainForm()
            {
                try
                {
                    var main = new MainForm();
                    MainForm = main;
                    main.StartupReady += async (_, _) =>
                    {
                        var remaining = TimeSpan.FromMilliseconds(1100) - (DateTime.UtcNow - _splashStarted);
                        if (remaining > TimeSpan.Zero) await Task.Delay(remaining);
                        if (!_splash.IsDisposed) _splash.Close();
                    };
                    main.Show();
                }
                catch (Exception ex)
                {
                    CrashLogger.Write(ex, "Startup");
                    MessageBox.Show(_splash, ex.Message, "خطای شروع CopyWeb", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    ExitThread();
                }
            }
        }

#if DEBUG
        private static void RunThemePreviewQa(string kind, string outputFile)
        {
            var root = new Uri("https://example.com/");
            using Form form = kind.ToLowerInvariant() switch
            {
                "links" => new LinksForm(root,
                [
                    new DownloadItem
                    {
                        Url = root.AbsoluteUri, Title = "صفحه اصلی نمونه", Depth = 0, State = CopyWebLinkState.Selected,
                        Resources =
                        [
                            new ResourceItem { Url = "https://example.com/Img/hero.webp", Kind = ResourceKind.Image, SizeBytes = 284_000, State = CopyWebLinkState.Pending },
                            new ResourceItem { Url = "https://example.com/CSS/site.css", Kind = ResourceKind.Stylesheet, SizeBytes = 42_000, State = CopyWebLinkState.Downloaded }
                        ]
                    },
                    new DownloadItem { Url = "https://example.com/about", Title = "درباره ما", Depth = 1, State = CopyWebLinkState.Downloaded },
                    new DownloadItem { Url = "https://example.com/contact", Title = "تماس با ما", Depth = 1, State = CopyWebLinkState.Failed, Error = "Timeout" }
                ]),
                "live" => new LiveArchiveForm(root, Path.Combine(Path.GetTempPath(), "CopyWeb-theme-preview")),
                "settings" => new SettingsForm(AppSettingsStore.Load()),
                "projects" => new ProjectsForm(),
                "reports" => new ReportsForm(),
                "about" => new AboutForm(),
                "tutorial" => new TutorialForm(),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown QA theme preview.")
            };

            using var timer = new System.Windows.Forms.Timer { Interval = kind.Equals("live", StringComparison.OrdinalIgnoreCase) ? 5000 : 1200 };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                form.Refresh();
                var path = Path.GetFullPath(outputFile);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using var bitmap = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                form.Close();
            };
            form.Shown += (_, _) => timer.Start();
            Application.Run(form);
        }

        private static void RunSiteMapQa(string projectFile, string outputFile)
        {
            using var form = new SiteGraphForm(projectFile);
            using var timer = new System.Windows.Forms.Timer { Interval = 4500 };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                form.Refresh();
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputFile))!);
                using var bitmap = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                bitmap.Save(outputFile, System.Drawing.Imaging.ImageFormat.Png);
                form.Close();
            };
            form.Shown += (_, _) => timer.Start();
            Application.Run(form);
        }

        private static void RunLiveWebpQa(string url, string outputDirectory)
        {
            var output = Path.GetFullPath(outputDirectory);
            Directory.CreateDirectory(output);
            using var form = new LiveArchiveForm(new Uri(url), output, suppressStartupErrorDialog: true);
            using var timer = new System.Windows.Forms.Timer { Interval = 8000 };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                form.Close();
            };
            form.Shown += (_, _) => timer.Start();
            Application.Run(form);

            var images = Directory.Exists(Path.Combine(output, "Img"))
                ? Directory.GetFiles(Path.Combine(output, "Img"), "*.webp", SearchOption.TopDirectoryOnly)
                : [];
            if (images.Length < 3) throw new InvalidDataException($"Live Archive saved only {images.Length} WebP files.");
            foreach (var image in images)
            {
                var bytes = File.ReadAllBytes(image);
                if (bytes.Length < 12 ||
                    !bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) ||
                    !bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8))
                    throw new InvalidDataException($"Live Archive produced an invalid WebP: {image}");
            }
            if (!File.Exists(Path.Combine(output, "live-capture-manifest.json")))
                throw new InvalidDataException("Live Archive manifest was not written.");
            if (!File.Exists(Path.Combine(output, "links.json")))
                throw new InvalidDataException("Live Archive was not registered as a project.");
            Console.WriteLine($"Live Archive WebP QA passed: {images.Length} valid WebP files.");
        }
#endif
    }
}
