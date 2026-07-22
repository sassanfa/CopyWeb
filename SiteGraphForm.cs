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
        Text = "نقشه گرافیکی سایت";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1000, 700);
        MinimumSize = new Size(720, 480);
        Font = UiTheme.NormalFont;
        RightToLeft = RightToLeft.Yes;
        BackColor = UiTheme.Background;
        var top = new Panel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(16, 10, 16, 6), BackColor = UiTheme.Surface };
        var title = UiTheme.Label("ارتباط صفحات داخلی آرشیو", 14, FontStyle.Bold); title.Dock = DockStyle.Left; title.Width = 320;
        _status.Dock = DockStyle.Fill; _status.TextAlign = ContentAlignment.MiddleRight;
        top.Controls.Add(_status); top.Controls.Add(title); Controls.Add(_canvas); Controls.Add(top);
        Shown += async (_, _) => await BuildGraphAsync();
    }

    private async Task BuildGraphAsync()
    {
        try
        {
            var graph = await Task.Run(() => LoadGraph());
            _canvas.SetGraph(graph);
            _status.Text = $"صفحه‌ها: {graph.Nodes.Count:N0} | ارتباط‌ها: {graph.Edges.Count:N0}";
            _canvas.Invalidate();
        }
        catch (Exception ex) { _status.Text = ex.Message; }
    }

    private GraphData LoadGraph()
    {
        var project = ProjectStorage.LoadAsync(_projectFile).GetAwaiter().GetResult();
        var root = Path.GetDirectoryName(_projectFile) ?? string.Empty;
        var pages = project.Links.Where(x => UrlTools.IsLikelyPageUrl(x.Uri)).Take(180).ToList();
        var urls = pages.Select(x => x.Url).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nodes = pages.Select((x, i) => new GraphNode(x.Url, string.IsNullOrWhiteSpace(x.Title) ? $"صفحه {i + 1}" : x.Title, i)).ToList();
        var edges = new HashSet<(string From, string To)>();
        var context = BrowsingContext.New(Configuration.Default);
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
        return new GraphData(nodes, edges.ToList());
    }

    private static Uri? SourceUrl(string file, string root, IReadOnlyList<DownloadItem> pages, string rootUrl)
    {
        var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
        if (relative.Equals("index.html", StringComparison.OrdinalIgnoreCase)) return Uri.TryCreate(rootUrl, UriKind.Absolute, out var r) ? r : null;
        return pages.FirstOrDefault(x => relative.Contains(UrlTools.Hash(x.Url), StringComparison.OrdinalIgnoreCase))?.Uri;
    }

    private sealed class GraphCanvas : Panel
    {
        private GraphData _graph = new([], []);
        public void SetGraph(GraphData graph) => _graph = graph;
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            if (_graph.Nodes.Count == 0) return;
            var center = new PointF(Width / 2f, Height / 2f); var radius = Math.Max(100, Math.Min(Width, Height) / 2f - 70); var positions = new Dictionary<string, PointF>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < _graph.Nodes.Count; i++) { var a = (float)(Math.PI * 2 * i / _graph.Nodes.Count); positions[_graph.Nodes[i].Url] = new PointF(center.X + radius * MathF.Cos(a), center.Y + radius * MathF.Sin(a)); }
            using var edgePen = new Pen(Color.FromArgb(180, 148, 163, 184), 1.2f); using var nodeBrush = new SolidBrush(Color.FromArgb(219, 234, 254)); using var nodePen = new Pen(Color.FromArgb(37, 99, 235), 1.4f); using var textBrush = new SolidBrush(UiTheme.Text);
            foreach (var edge in _graph.Edges) if (positions.TryGetValue(edge.From, out var from) && positions.TryGetValue(edge.To, out var to)) e.Graphics.DrawLine(edgePen, from, to);
            foreach (var node in _graph.Nodes) { var p = positions[node.Url]; e.Graphics.FillEllipse(nodeBrush, p.X - 18, p.Y - 18, 36, 36); e.Graphics.DrawEllipse(nodePen, p.X - 18, p.Y - 18, 36, 36); var text = node.Title.Length > 18 ? node.Title[..18] + "…" : node.Title; var size = e.Graphics.MeasureString(text, Font); e.Graphics.DrawString(text, Font, textBrush, p.X - size.Width / 2, p.Y + 22); }
        }
    }

    private sealed record GraphData(IReadOnlyList<GraphNode> Nodes, IReadOnlyList<(string From, string To)> Edges);
    private sealed record GraphNode(string Url, string Title, int Index);
}
