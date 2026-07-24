using AngleSharp;
using CopyWeb.Models;
using CopyWeb.Services;

namespace CopyWeb;

public sealed class SiteGraphForm : Form
{
    private readonly string _projectFile;
    private readonly GraphCanvas _canvas = new() { Dock = DockStyle.Fill };
    private readonly Label _status = UiTheme.Label("در حال ساخت نقشه...", 9, color: UiTheme.Muted);

    public SiteGraphForm(string projectFile)
    {
        _projectFile = projectFile;
        Text = "نقشه ساختاری سایت";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1100, 760);
        MinimumSize = new Size(760, 520);
        Font = UiTheme.NormalFont;
        RightToLeft = RightToLeft.Yes;
        BackColor = UiTheme.Background;
        _canvas.BackColor = UiTheme.Background;

        var top = new Panel { Dock = DockStyle.Top, Height = 54, Padding = new Padding(18, 10, 18, 7), BackColor = UiTheme.Surface };
        var title = UiTheme.Label("نقشه شاخه‌ای صفحات آرشیو", 14, FontStyle.Bold); title.Dock = DockStyle.Left; title.Width = 330;
        _status.Dock = DockStyle.Fill; _status.TextAlign = ContentAlignment.MiddleRight;
        top.Controls.Add(_status); top.Controls.Add(title);
        Controls.Add(_canvas); Controls.Add(top);
        UiTheme.StyleDialog(this);
        Shown += async (_, _) => await BuildGraphAsync();
    }

    private async Task BuildGraphAsync()
    {
        try
        {
            var graph = await Task.Run(LoadGraph);
            _canvas.SetGraph(graph);
            _status.Text = $"صفحه‌ها: {graph.Nodes.Count:N0} | ارتباط‌های داخلی: {graph.Edges.Count:N0} | برای باز و بسته‌کردن هر شاخه روی عنوان آن کلیک کنید";
        }
        catch (Exception ex) { _status.Text = ex.Message; }
    }

    private GraphData LoadGraph()
    {
        var project = ProjectStorage.LoadAsync(_projectFile).GetAwaiter().GetResult();
        var root = Path.GetDirectoryName(_projectFile) ?? string.Empty;
        var pages = project.Links.Where(x => UrlTools.IsLikelyPageUrl(x.Uri)).Take(500).ToList();
        var urls = pages.Select(x => x.Url).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nodes = pages.Select((x, i) => new GraphNode(x.Url, string.IsNullOrWhiteSpace(x.Title) ? PageName(x.Uri, i) : x.Title, i)).ToList();
        var edges = new HashSet<(string From, string To)>();
        var context = BrowsingContext.New(Configuration.Default);
        if (Directory.Exists(root))
        {
            foreach (var file in Directory.EnumerateFiles(root, "*.html", SearchOption.AllDirectories))
            {
                var source = SourceUrl(file, root, pages, project.RootUrl);
                if (source is null) continue;
                var doc = context.OpenAsync(req => req.Content(File.ReadAllText(file))).GetAwaiter().GetResult();
                foreach (var anchor in doc.QuerySelectorAll("a[href]"))
                {
                    if (!Uri.TryCreate(source, anchor.GetAttribute("href"), out var target)) continue;
                    var normalized = UrlTools.NormalizePageUrl(source, target.AbsoluteUri)?.AbsoluteUri;
                    if (normalized is not null && urls.Contains(normalized)) edges.Add((source.AbsoluteUri, normalized));
                }
            }
        }
        return new GraphData(project.RootUrl, nodes, edges.ToList());
    }

    private static string PageName(Uri uri, int index)
    {
        var segment = uri.Segments.LastOrDefault()?.Trim('/') ?? string.Empty;
        return string.IsNullOrWhiteSpace(segment) ? $"صفحه {index + 1}" : Uri.UnescapeDataString(segment).Replace('-', ' ');
    }

    private static Uri? SourceUrl(string file, string root, IReadOnlyList<DownloadItem> pages, string rootUrl)
    {
        var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
        if (relative.Equals("index.html", StringComparison.OrdinalIgnoreCase)) return Uri.TryCreate(rootUrl, UriKind.Absolute, out var r) ? r : null;
        return pages.FirstOrDefault(x => relative.Contains(UrlTools.Hash(x.Url), StringComparison.OrdinalIgnoreCase))?.Uri;
    }

    private sealed class GraphCanvas : Panel
    {
        private const int GroupWidth = 250;
        private readonly Dictionary<string, bool> _expanded = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Rectangle> _groupHitBoxes = new(StringComparer.OrdinalIgnoreCase);
        private GraphData _graph = new(string.Empty, [], []);

        public GraphCanvas()
        {
            AutoScroll = true;
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        public void SetGraph(GraphData graph)
        {
            _graph = graph;
            _expanded.Clear();
            AutoScrollPosition = Point.Empty;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Cursor = _groupHitBoxes.Values.Any(bounds => bounds.Contains(e.Location)) ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            var key = _groupHitBoxes.FirstOrDefault(pair => pair.Value.Contains(e.Location)).Key;
            if (string.IsNullOrWhiteSpace(key)) return;
            _expanded[key] = !_expanded.GetValueOrDefault(key);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            if (_graph.Nodes.Count == 0) return;

            var groups = BuildGroups();
            var columns = Math.Max(1, Math.Min(4, (Math.Max(ClientSize.Width, 360) - 70) / (GroupWidth + 28)));
            var contentWidth = Math.Max(ClientSize.Width - 4, columns * GroupWidth + (columns - 1) * 28 + 64);
            var rootBounds = new Rectangle((contentWidth - 270) / 2, 28, 270, 58);
            var layouts = LayoutGroups(groups, columns, 132, contentWidth);
            var contentHeight = layouts.Count == 0 ? 180 : layouts.Max(x => x.Bounds.Bottom) + 44;
            var requested = new Size(contentWidth, contentHeight);
            if (AutoScrollMinSize != requested) AutoScrollMinSize = requested;

            var offset = AutoScrollPosition;
            e.Graphics.TranslateTransform(offset.X, offset.Y);
            DrawRoot(e.Graphics, rootBounds);
            using var connector = new Pen(Color.FromArgb(72, 96, 175), 1.5F) { LineJoin = System.Drawing.Drawing2D.LineJoin.Round };
            foreach (var layout in layouts)
            {
                var rootCenter = new Point(rootBounds.Left + rootBounds.Width / 2, rootBounds.Bottom);
                var groupCenter = new Point(layout.Bounds.Left + layout.Bounds.Width / 2, layout.Bounds.Top);
                var jointY = rootBounds.Bottom + 22;
                e.Graphics.DrawLines(connector, [rootCenter, new Point(rootCenter.X, jointY), new Point(groupCenter.X, jointY), groupCenter]);
            }

            _groupHitBoxes.Clear();
            foreach (var layout in layouts) DrawGroup(e.Graphics, layout, offset);
        }

        private List<MapGroup> BuildGroups()
        {
            return _graph.Nodes
                .Where(node => !string.Equals(node.Url.TrimEnd('/'), _graph.RootUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                .GroupBy(node => GroupKey(node.Url), StringComparer.OrdinalIgnoreCase)
                .Select(group => new MapGroup(group.Key, GroupTitle(group.Key), group.OrderBy(x => x.Url).ToList()))
                .OrderBy(group => group.Key == "/" ? 0 : 1)
                .ThenBy(group => group.Title, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private List<GroupLayout> LayoutGroups(IReadOnlyList<MapGroup> groups, int columns, int startY, int contentWidth)
        {
            var result = new List<GroupLayout>();
            const int gap = 28;
            var left = (contentWidth - (columns * GroupWidth + (columns - 1) * gap)) / 2;
            var y = startY;
            for (var index = 0; index < groups.Count; index += columns)
            {
                var row = groups.Skip(index).Take(columns).ToList();
                var rowHeight = row.Max(GroupHeight);
                for (var column = 0; column < row.Count; column++)
                    result.Add(new GroupLayout(row[column], new Rectangle(left + column * (GroupWidth + gap), y, GroupWidth, GroupHeight(row[column]))));
                y += rowHeight + 34;
            }
            return result;
        }

        private int GroupHeight(MapGroup group)
        {
            var visible = _expanded.GetValueOrDefault(group.Key) ? group.Pages.Count : Math.Min(group.Pages.Count, 7);
            return 50 + visible * 34 + (visible < group.Pages.Count ? 34 : 12);
        }

        private void DrawRoot(Graphics graphics, Rectangle bounds)
        {
            FillRounded(graphics, bounds, 12, Color.FromArgb(78, 62, 176), Color.FromArgb(126, 90, 245));
            var host = Uri.TryCreate(_graph.RootUrl, UriKind.Absolute, out var uri) ? uri.Host : _graph.RootUrl;
            using var font = new Font(Font, FontStyle.Bold);
            TextRenderer.DrawText(graphics, host, font, bounds, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        private void DrawGroup(Graphics graphics, GroupLayout layout, Point offset)
        {
            var group = layout.Group;
            var bounds = layout.Bounds;
            FillRounded(graphics, bounds, 11, Color.FromArgb(20, 29, 67), Color.FromArgb(20, 29, 67), Color.FromArgb(48, 62, 113));
            var header = new Rectangle(bounds.Left, bounds.Top, bounds.Width, 44);
            FillRounded(graphics, header, 10, Color.FromArgb(31, 42, 88), Color.FromArgb(37, 48, 100));
            var marker = _expanded.GetValueOrDefault(group.Key) ? "−" : "+";
            using var headerFont = new Font(Font, FontStyle.Bold);
            TextRenderer.DrawText(graphics, $"{marker}   {group.Title}   ({group.Pages.Count:N0})", headerFont, Rectangle.Inflate(header, -12, 0), Color.White, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.RightToLeft);
            _groupHitBoxes[group.Key] = new Rectangle(header.X + offset.X, header.Y + offset.Y, header.Width, header.Height);

            var count = _expanded.GetValueOrDefault(group.Key) ? group.Pages.Count : Math.Min(group.Pages.Count, 7);
            for (var index = 0; index < count; index++)
            {
                var item = new Rectangle(bounds.Left + 10, bounds.Top + 50 + index * 34, bounds.Width - 20, 28);
                FillRounded(graphics, item, 7, Color.FromArgb(26, 36, 76), Color.FromArgb(26, 36, 76), Color.FromArgb(42, 55, 101));
                TextRenderer.DrawText(graphics, PageLabel(group.Pages[index]), Font, Rectangle.Inflate(item, -9, 0), Color.FromArgb(213, 222, 242), TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.RightToLeft);
            }
            if (count < group.Pages.Count)
            {
                var more = new Rectangle(bounds.Left + 10, bounds.Top + 50 + count * 34, bounds.Width - 20, 26);
                TextRenderer.DrawText(graphics, $"نمایش {group.Pages.Count - count:N0} صفحه دیگر…", Font, more, Color.FromArgb(139, 154, 196), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.RightToLeft);
            }
        }

        private static string GroupKey(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return "/";
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments.Length == 0 ? "/" : segments[0];
        }

        private static string GroupTitle(string key) => key == "/" ? "صفحه اصلی" : Uri.UnescapeDataString(key).Replace('-', ' ');
        private static string PageLabel(GraphNode node) => string.IsNullOrWhiteSpace(node.Title) ? node.Url : node.Title;

        private static void FillRounded(Graphics graphics, Rectangle bounds, int radius, Color start, Color end, Color? border = null)
        {
            using var path = Rounded(bounds, radius);
            using var fill = new System.Drawing.Drawing2D.LinearGradientBrush(bounds, start, end, 0F);
            graphics.FillPath(fill, path);
            if (border.HasValue) using (var pen = new Pen(border.Value, 1F)) graphics.DrawPath(pen, path);
        }

        private static System.Drawing.Drawing2D.GraphicsPath Rounded(Rectangle bounds, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    private sealed record GraphData(string RootUrl, IReadOnlyList<GraphNode> Nodes, IReadOnlyList<(string From, string To)> Edges);
    private sealed record GraphNode(string Url, string Title, int Index);
    private sealed record MapGroup(string Key, string Title, IReadOnlyList<GraphNode> Pages);
    private sealed record GroupLayout(MapGroup Group, Rectangle Bounds);
}
