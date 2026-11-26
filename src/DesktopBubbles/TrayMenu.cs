using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using Forms = System.Windows.Forms;

using DesktopBubbles.Core;

namespace DesktopBubbles;

// The tray menu in DesktopBubbles' own look: the app's soft gradient across the whole menu (no grey
// image margin), a bubble with the version on top, red "Beenden" at the foot, rounded corners.
// Same hand as the author's Pi Relay menu: on hover the version letters dance and "Beenden" trembles;
// clicking the version line looks for a newer release.
internal static class TrayMenu
{
    internal static Color Surface, SurfaceEnd, Hover, Line, Text, Muted;
    internal static readonly Color Danger = Color.FromArgb(206, 44, 70), Green = Color.FromArgb(36, 166, 92), Amber = Color.FromArgb(210, 124, 20);
    internal const int Width = 268, EdgeHeight = 48;

    // Follows the app's current light or dark palette, refreshed on every opening.
    internal static void Theme()
    {
        Surface = Gdi("Surface"); Text = Gdi("Ink"); Muted = Gdi("Muted"); Line = Gdi("Line"); Hover = Gdi("HoverFill");
        SurfaceEnd = Mix(Surface, Gdi("Accent"), .14);
    }
    private static Color Gdi(string key) { var c = Ui.Resource(key).Color; return Color.FromArgb(255, c.R, c.G, c.B); }
    private static Color Mix(Color a, Color b, double t)
        => Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

    internal static Forms.ContextMenuStrip Create()
    {
        Theme();
        var menu = new Forms.ContextMenuStrip { Renderer = new Renderer(), ShowImageMargin = false, ShowCheckMargin = false, BackColor = Surface, ForeColor = Text, Font = new Font("Segoe UI", 9.5f), Padding = new Forms.Padding(0, 2, 0, 2) };
        menu.HandleCreated += (_, _) => Round(menu.Handle);
        menu.Opening += (_, _) => { Theme(); menu.BackColor = Surface; menu.Invalidate(); };
        return menu;
    }
    internal static Forms.ToolStripMenuItem Item(string text, EventHandler click) => new(text, null, click) { ForeColor = Text, AutoSize = false, Size = new Size(Width, 34) };
    internal static Forms.ToolStripMenuItem Exit(string text, EventHandler click)
    {
        var item = new Forms.ToolStripMenuItem(text, null, click) { ForeColor = Danger, AutoSize = false, Size = new Size(Width, EdgeHeight - 4) };
        item.Tag = new LetterMotion(item, LetterMotion.Style.Shake); return item;
    }
    internal static Forms.ToolStripSeparator Spacer() => new() { AutoSize = false, Size = new Size(Width, 6) };
    internal static int VisibleWidth(Forms.ToolStripItem item) { var owner = item.Owner; return owner is null ? item.Width : Math.Max(40, Math.Min(item.Width, owner.ClientSize.Width - item.Bounds.Left - 1)); }

    // One gradient spans the whole menu; every item paints its own slice of it, so nothing shows seams.
    internal static void PaintBackground(Graphics g, Forms.ToolStripItem item)
    {
        var owner = item.Owner; if (owner is null) { g.Clear(Surface); return; }
        var whole = new Rectangle(-item.Bounds.Left, -item.Bounds.Top, Math.Max(1, owner.Width), Math.Max(1, owner.Height));
        using var brush = new LinearGradientBrush(whole, Surface, SurfaceEnd, LinearGradientMode.ForwardDiagonal);
        g.FillRectangle(brush, new Rectangle(0, 0, item.Width, item.Height));
    }
    private static void Round(IntPtr handle) { try { int preference = 2; DwmSetWindowAttribute(handle, 33, ref preference, sizeof(int)); } catch (DllNotFoundException) { } }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    private sealed class Renderer : Forms.ToolStripProfessionalRenderer
    {
        public Renderer() : base(new Colors()) { RoundedEdges = false; }
        protected override void OnRenderToolStripBackground(Forms.ToolStripRenderEventArgs e)
        {
            using var brush = new LinearGradientBrush(new Rectangle(Point.Empty, e.ToolStrip.Size), Surface, SurfaceEnd, LinearGradientMode.ForwardDiagonal);
            e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.ToolStrip.Size));
        }
        protected override void OnRenderToolStripBorder(Forms.ToolStripRenderEventArgs e) { }
        protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; int w = VisibleWidth(e.Item);
            PaintBackground(g, e.Item);
            if (e.Item.Tag is LetterMotion) { using var pen = new Pen(Line); g.DrawLine(pen, 14, 0, w - 14, 0); Door(g, new Rectangle(20, (e.Item.Height - 16) / 2 + 1, 16, 16)); return; }
            if (e.Item.Selected && e.Item.Enabled) { using var path = RoundRect(new Rectangle(6, 1, w - 12, e.Item.Height - 2), 8); using var fill = new SolidBrush(Color.FromArgb(150, Hover)); g.FillPath(fill, path); }
        }
        protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
        {
            if (e.Item.Tag is LetterMotion motion)
            {
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                motion.Draw(e.Graphics, e.Item.Text ?? "", e.Item.Font, e.Item.ForeColor, new RectangleF(44, 1, VisibleWidth(e.Item) - 60, e.Item.Height - 1)); return;
            }
            e.TextColor = e.Item.ForeColor; e.TextRectangle = new Rectangle(20, 0, VisibleWidth(e.Item) - 36, e.Item.Height);
            e.TextFormat = Forms.TextFormatFlags.VerticalCenter | Forms.TextFormatFlags.Left | Forms.TextFormatFlags.NoPrefix | Forms.TextFormatFlags.EndEllipsis;
            base.OnRenderItemText(e);
        }
        protected override void OnRenderSeparator(Forms.ToolStripSeparatorRenderEventArgs e) => PaintBackground(e.Graphics, e.Item);
    }
    // A door frame open to the right with an arrow walking out of it.
    private static void Door(Graphics g, Rectangle r)
    {
        using var pen = new Pen(Danger, 1.6f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLines(pen, new[] { new PointF(r.X + 9, r.Y + 1), new PointF(r.X + 2, r.Y + 1), new PointF(r.X + 2, r.Bottom - 1), new PointF(r.X + 9, r.Bottom - 1) });
        float mid = r.Y + r.Height / 2f;
        g.DrawLine(pen, r.X + 6, mid, r.Right - 1, mid);
        g.DrawLines(pen, new[] { new PointF(r.Right - 4, mid - 3), new PointF(r.Right - 1, mid), new PointF(r.Right - 4, mid + 3) });
    }
    private sealed class Colors : Forms.ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Surface;
        public override Color MenuBorder => Line;
        public override Color MenuItemBorder => Color.Transparent;
        public override Color MenuItemSelected => Hover;
        public override Color ImageMarginGradientBegin => Surface;
        public override Color ImageMarginGradientMiddle => Surface;
        public override Color ImageMarginGradientEnd => Surface;
    }
    internal static GraphicsPath RoundRect(Rectangle r, int radius)
    {
        var path = new GraphicsPath(); int d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90); path.AddArc(r.Right - d, r.Y, d, d, 270, 90); path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure(); return path;
    }
}

// The head line: the bubble logo and "DesktopBubbles 1.8.x". Fixed size, so the menu never re-lays out.
// Clicking looks for a newer GitHub release: the text slides to the state, a green glow confirms
// "aktuell", a newer release shows a download button that opens its release page.
internal sealed class VersionItem : Forms.ToolStripMenuItem
{
    private string text = "", oldText = "";
    private Color color, oldColor;
    private readonly Stopwatch slide = new(), glow = new();
    private readonly Forms.Timer frame = new() { Interval = 16 };
    private readonly LetterMotion dance;
    private readonly Bitmap? logo;
    private string? releasePage;
    private bool checking;
    private const double OutMs = 170, InMs = 230, GlowMs = 1100;
    internal string Home { get; }

    public VersionItem(string version)
    {
        AutoSize = false; Size = new Size(TrayMenu.Width, TrayMenu.EdgeHeight); ToolTipText = Loc.T("Klicken: nach Updates suchen", "Click: check for updates");
        Home = "DesktopBubbles " + version; text = Home; color = TrayMenu.Text;
        dance = new LetterMotion(this, LetterMotion.Style.Dance);
        frame.Tick += (_, _) => { if (!Animating) frame.Stop(); Invalidate(); };
        try
        {
            using var stream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/DesktopBubbles.ico")).Stream;
            using var icon = new Icon(stream, 32, 32); logo = icon.ToBitmap();
        }
        catch (Exception ex) when (ex is System.IO.IOException or ArgumentException) { logo = null; }
    }
    private bool Animating => slide.IsRunning && slide.Elapsed.TotalMilliseconds < OutMs + InMs || glow.IsRunning && glow.Elapsed.TotalMilliseconds < GlowMs || dance.Active;
    private Rectangle UpdateButton => new(TrayMenu.VisibleWidth(this) - 42, (Height - 1 - 28) / 2, 28, 28);
    public void Reset() { text = Home; color = TrayMenu.Text; releasePage = null; slide.Reset(); glow.Reset(); Invalidate(); }
    private void SlideTo(string value, Color tint) { oldText = text; oldColor = color; text = value; color = tint; slide.Restart(); frame.Start(); }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); frame.Start(); }
    protected override void OnMouseUp(Forms.MouseEventArgs e)
    {
        if (releasePage is not null && UpdateButton.Contains(e.Location))
        {
            try { Process.Start(new ProcessStartInfo(releasePage) { UseShellExecute = true }); } catch (System.ComponentModel.Win32Exception) { }
            return;
        }
        base.OnMouseUp(e);
    }
    // The owner keeps the menu open for this click (see App.SetupTray).
    internal async Task Check(string current)
    {
        if (checking) return; checking = true;
        try
        {
            releasePage = null; SlideTo(Loc.T("Suche nach Updates …", "Checking for updates …"), TrayMenu.Text); await Task.Delay(400);
            var (latest, page) = await Latest();
            if (latest is null) { SlideTo(Loc.T("Prüfung nicht möglich (offline?)", "Check not possible (offline?)"), TrayMenu.Muted); await Task.Delay(2400); SlideTo(Home, TrayMenu.Text); return; }
            if (Version.TryParse(current, out var mine) && latest > mine) { releasePage = page; SlideTo(Loc.T($"DesktopBubbles {DateVersion(latest)} verfügbar", $"DesktopBubbles {DateVersion(latest)} available"), TrayMenu.Amber); return; }
            SlideTo(Home, TrayMenu.Text); await Task.Delay(TimeSpan.FromMilliseconds(OutMs + InMs)); glow.Restart(); frame.Start();
        }
        finally { checking = false; }
    }
    // Releases are named by date, YY.MM.DD (26.12.01); Version drops the zeros (26.12.1), so pad them back.
    // Old numbers such as 1.9.0 stay as they are.
    internal static string DateVersion(Version v) => v.Major < 20 || v.Build < 0 ? v.ToString()
        : $"{v.Major}.{v.Minor:00}.{v.Build:00}" + (v.Revision > 0 ? "." + v.Revision : "");
    private static async Task<(Version?, string?)> Latest()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("DesktopBubbles");
            using var json = JsonDocument.Parse(await http.GetStringAsync("https://api.github.com/repos/Hendrik4K/DesktopBubbles/releases/latest"));
            var tag = json.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v', 'V');
            var page = json.RootElement.GetProperty("html_url").GetString();
            return Version.TryParse(tag, out var version) && page is not null && page.StartsWith("https://github.com/Hendrik4K/DesktopBubbles/", StringComparison.Ordinal) ? (version, page) : (null, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException) { return (null, null); }
    }
    protected override void OnPaint(Forms.PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        TrayMenu.PaintBackground(g, this); int w = TrayMenu.VisibleWidth(this);
        using (var pen = new Pen(TrayMenu.Line)) g.DrawLine(pen, 14, Height - 1, w - 14, Height - 1);
        if (logo is not null) g.DrawImage(logo, new Rectangle(14, (Height - 26) / 2, 26, 26));
        var area = new Rectangle(48, 0, w - (releasePage is null ? 62 : 98), Height - 1);
        var state = g.Save(); g.SetClip(area);
        double ms = slide.IsRunning ? slide.Elapsed.TotalMilliseconds : double.MaxValue;
        if (ms < OutMs) { double t = Ease(ms / OutMs); Draw(g, oldText, oldColor, area, -24 * t, 1 - t); }
        else { double t = ms < OutMs + InMs ? Ease((ms - OutMs) / InMs) : 1; Draw(g, text, Glowing(color), area, 24 * (1 - t), t); }
        g.Restore(state);
        if (releasePage is not null)
        {
            var b = UpdateButton; using var path = TrayMenu.RoundRect(b, 9); using var fill = new SolidBrush(Color.FromArgb(60, TrayMenu.Amber)); g.FillPath(fill, path);
            using var icon = new Font("Segoe Fluent Icons", 11f);
            Forms.TextRenderer.DrawText(g, "", icon, b, TrayMenu.Amber, Forms.TextFormatFlags.HorizontalCenter | Forms.TextFormatFlags.VerticalCenter | Forms.TextFormatFlags.NoPadding);
        }
    }
    private Color Glowing(Color tint)
    {
        if (!glow.IsRunning) return tint; double u = glow.Elapsed.TotalMilliseconds / GlowMs; if (u >= 1) return tint;
        double k = Math.Sin(u * Math.PI); k *= k;
        return Color.FromArgb((int)(tint.R + (TrayMenu.Green.R - tint.R) * k), (int)(tint.G + (TrayMenu.Green.G - tint.G) * k), (int)(tint.B + (TrayMenu.Green.B - tint.B) * k));
    }
    private void Draw(Graphics g, string value, Color tint, Rectangle area, double dx, double alpha)
    {
        var c = Color.FromArgb((int)(255 * Math.Clamp(alpha, 0, 1)), tint);
        dance.Draw(g, value, Font, c, new RectangleF(area.X + (float)dx, area.Y, area.Width, area.Height));
    }
    private static double Ease(double t) { t = Math.Clamp(t, 0, 1); return 1 - Math.Pow(1 - t, 3); }
    protected override void Dispose(bool disposing) { if (disposing) { frame.Dispose(); logo?.Dispose(); } base.Dispose(disposing); }
}
