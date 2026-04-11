using System.Drawing.Drawing2D;
using System.Drawing.Text;
using FrameBoost.Models;

namespace FrameBoost.Ui;

/// <summary>
/// Fully custom-painted performance overlay.
///
/// Two visual styles driven by <see cref="AppSettings.OverlayStyle"/>:
///   Card    — semi-opaque dark rounded card with accent glow (original look)
///   Minimal — fully transparent background, text + drop-shadows only
///
/// Which metrics appear is controlled by OverlayShowFps / ShowCpu / ShowGpu.
/// Accent colour is read from OverlayAccentArgb.
/// </summary>
internal sealed class PerformanceOverlayForm : Form
{
    // ── Fixed geometry constants ──────────────────────────────────────────────
    private const int   EdgeMargin = 18;
    private const float Corner     = 10f;
    private const int   PadX       = 13;
    private const int   PadY       = 10;

    // ── Palette (non-accent colours) ──────────────────────────────────────────
    private static readonly Color Bg        = Color.FromArgb(210, 14, 14, 18);
    private static readonly Color White     = Color.FromArgb(255, 248, 250, 255);
    private static readonly Color SubText   = Color.FromArgb(200, 210, 200, 225);
    private static readonly Color WarnAmber = Color.FromArgb(255, 251, 146,  60);
    private static readonly Color Shadow    = Color.FromArgb(160,   0,   0,   0);

    // ── Live state ────────────────────────────────────────────────────────────
    private readonly AppSettings _settings;
    private TelemetrySnapshot   _snap        = new();
    private readonly string     _pinnedScreen;
    private Size                _lastComputedSize;

    // ── Constructor ───────────────────────────────────────────────────────────
    public PerformanceOverlayForm(AppSettings settings)
    {
        _settings     = settings;
        _pinnedScreen = (Screen.PrimaryScreen ?? Screen.FromPoint(Location)).DeviceName;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar   = false;
        TopMost         = true;
        StartPosition   = FormStartPosition.Manual;
        BackColor       = Color.Fuchsia;
        TransparencyKey = Color.Fuchsia;

        SetStyle(
            ControlStyles.AllPaintingInWmPaint  |
            ControlStyles.UserPaint             |
            ControlStyles.OptimizedDoubleBuffer, true);

        ApplySize();
        Location = ComputeLocation();
    }

    // ── Window style flags ────────────────────────────────────────────────────
    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00000020   // WS_EX_TRANSPARENT
                        | 0x00080000   // WS_EX_LAYERED
                        | 0x00000080   // WS_EX_TOOLWINDOW
                        | 0x08000000;  // WS_EX_NOACTIVATE
            return cp;
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────
    public void UpdateSnapshot(TelemetrySnapshot snapshot)
    {
        if (InvokeRequired) { BeginInvoke(() => UpdateSnapshot(snapshot)); return; }
        _snap = snapshot;
        ApplySize();
        EnsurePinnedLocation();
        Invalidate();
    }

    // ── Size management ───────────────────────────────────────────────────────
    private void ApplySize()
    {
        var sz = ComputeSize();
        if (sz == _lastComputedSize) return;
        _lastComputedSize = sz;
        Size     = sz;
        Location = ComputeLocation();   // reposition after resize
    }

    private Size ComputeSize()
    {
        var showFps = _settings.OverlayShowFps;
        var showCpu = _settings.OverlayShowCpu;
        var showGpu = _settings.OverlayShowGpu;

        if (_settings.OverlayStyle == OverlayStyle.Minimal)
        {
            // Height: top-pad + wordmark + gap + [fps block] + [cpu/gpu row] + bottom-pad
            int h = PadY + 12 + 3;
            if (showFps)              h += 33;
            if (showCpu || showGpu)   h += 3 + 15;
            h += 8;

            // Width: at least enough for the wordmark, then widen for content
            int w = 72;  // "CLOUDFRAME" at 7pt ≈ 65px + small pad
            if (showFps && (showCpu || showGpu)) w = Math.Max(w, 155);
            else if (showFps)                    w = Math.Max(w,  88);
            if (showCpu && showGpu)              w = Math.Max(w, 160);
            else if (showCpu || showGpu)         w = Math.Max(w, 110);

            return new Size(w + 8, Math.Max(36, h));
        }
        else  // Card
        {
            int h = 78;
            if (!showFps)              h -= 22;
            if (!showCpu && !showGpu)  h -= 16;
            return new Size(195, Math.Max(44, h));
        }
    }

    // ── Paint dispatch ────────────────────────────────────────────────────────
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode      = SmoothingMode.AntiAlias;
        g.TextRenderingHint  = TextRenderingHint.ClearTypeGridFit;
        g.CompositingQuality = CompositingQuality.HighQuality;

        if (_settings.OverlayStyle == OverlayStyle.Minimal)
            PaintMinimal(g);
        else
            PaintCard(g);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // MINIMAL — fully transparent, text + drop-shadows only
    // ═══════════════════════════════════════════════════════════════════════════

    private void PaintMinimal(Graphics g)
    {
        var accent      = Color.FromArgb(_settings.OverlayAccentArgb);
        var accentMuted = Color.FromArgb(140, accent.R, accent.G, accent.B);

        float x = 4f;
        float y = (float)PadY;

        // ── CLOUDFRAME wordmark ───────────────────────────────────────────────
        using var wFont = new Font("Bahnschrift SemiBold", 7f, FontStyle.Regular, GraphicsUnit.Point);
        using var wBrush = new SolidBrush(accentMuted);
        DrawShadowed(g, "CLOUDFRAME", wFont, wBrush, x, y);
        y += 14f;

        // ── FPS block ─────────────────────────────────────────────────────────
        if (_settings.OverlayShowFps)
        {
            bool   hasFps = _snap.FramesPerSecond is double d && d > 0;
            string fpsStr = hasFps ? $"{_snap.FramesPerSecond!.Value:0}" : "--";
            bool   lowFps = hasFps && _snap.FramesPerSecond!.Value < 30;

            using var fpsFont  = new Font("Bahnschrift SemiBold", 27f, FontStyle.Regular, GraphicsUnit.Point);
            using var unitFont = new Font("Bahnschrift SemiBold",  9f, FontStyle.Regular, GraphicsUnit.Point);
            using var fpsBrush = new SolidBrush(lowFps ? WarnAmber : accent);

            DrawShadowed(g, fpsStr, fpsFont, fpsBrush, x - 2f, y);
            var numW = g.MeasureString(fpsStr, fpsFont).Width;
            using var unitBrush = new SolidBrush(accentMuted);
            DrawShadowed(g, "FPS", unitFont, unitBrush, x + numW - 6f, y + 17f);

            y += g.MeasureString(fpsStr, fpsFont).Height + 1f;
        }

        // ── CPU / GPU row ─────────────────────────────────────────────────────
        if (_settings.OverlayShowCpu || _settings.OverlayShowGpu)
        {
            y += 3f;
            using var dimFont  = new Font("Bahnschrift SemiBold", 7.5f, FontStyle.Regular, GraphicsUnit.Point);
            using var valFont  = new Font("Bahnschrift SemiBold", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
            using var dimBrush = new SolidBrush(accentMuted);
            using var valBrush = new SolidBrush(accent);

            float cx = x;
            if (_settings.OverlayShowCpu)
            {
                DrawShadowed(g, "CPU", dimFont, dimBrush, cx, y + 1.5f);
                cx += g.MeasureString("CPU", dimFont).Width;
                var cpuStr = $"{_snap.CpuPercent:0}%";
                DrawShadowed(g, cpuStr, valFont, valBrush, cx - 1f, y);
                cx += g.MeasureString(cpuStr, valFont).Width + 8f;
            }
            if (_settings.OverlayShowGpu)
            {
                DrawShadowed(g, "GPU", dimFont, dimBrush, cx, y + 1.5f);
                cx += g.MeasureString("GPU", dimFont).Width;
                var gpuStr = _snap.GpuPercent is double gpu ? $"{gpu:0}%" : "--%";
                DrawShadowed(g, gpuStr, valFont, valBrush, cx - 1f, y);
            }
        }
    }

    private static void DrawShadowed(Graphics g, string text, Font font, Brush brush, float x, float y)
    {
        using var shadow = new SolidBrush(Shadow);
        g.DrawString(text, font, shadow, new PointF(x + 1.5f, y + 1.5f));
        g.DrawString(text, font,  brush, new PointF(x,        y));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CARD — semi-opaque dark card with accent glow
    // ═══════════════════════════════════════════════════════════════════════════

    private void PaintCard(Graphics g)
    {
        var accent     = Color.FromArgb(_settings.OverlayAccentArgb);
        var accentDim  = Color.FromArgb( 70, accent.R, accent.G, accent.B);
        var accentMuted= Color.FromArgb(130, accent.R, accent.G, accent.B);

        var bounds = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        using var cardPath = RoundRect(bounds, Corner);

        // ── Dark background card ──────────────────────────────────────────────
        using (var bgBrush = new SolidBrush(Bg))
            g.FillPath(bgBrush, cardPath);

        // ── Accent top-edge glow ──────────────────────────────────────────────
        using (var glowBrush = new LinearGradientBrush(
            new PointF(bounds.X + Corner, bounds.Y),
            new PointF(bounds.Right - Corner, bounds.Y),
            Color.Transparent, Color.Transparent))
        {
            glowBrush.InterpolationColors = new ColorBlend
            {
                Positions = [0f, 0.25f, 0.75f, 1f],
                Colors    = [Color.Transparent, accentDim, accentDim, Color.Transparent]
            };
            g.FillRectangle(glowBrush, bounds.X + Corner, bounds.Y, bounds.Width - Corner * 2, 1.5f);
        }

        // ── Left accent bar ───────────────────────────────────────────────────
        using var barBrush = new LinearGradientBrush(
            new PointF(bounds.X, bounds.Y + Corner),
            new PointF(bounds.X, bounds.Bottom - Corner),
            accent, Color.FromArgb(30, accent.R, accent.G, accent.B))
        { WrapMode = WrapMode.TileFlipX };
        g.FillRectangle(barBrush, bounds.X + 1, bounds.Y + Corner, 2.5f, bounds.Height - Corner * 2);

        // ── Subtle border ─────────────────────────────────────────────────────
        using var borderPen = new Pen(Color.FromArgb(45, 255, 255, 255), 0.75f);
        g.DrawPath(borderPen, cardPath);

        // ── Content ───────────────────────────────────────────────────────────
        DrawCardContent(g, bounds, accent, accentMuted);
    }

    private void DrawCardContent(Graphics g, RectangleF bounds, Color accent, Color accentMuted)
    {
        float x = bounds.X + PadX + 7f; // leave space for accent bar
        float y = bounds.Y + PadY;

        // ── "CLOUDFRAME" wordmark ─────────────────────────────────────────────
        using var labelFont  = new Font("Bahnschrift SemiBold", 7f, FontStyle.Regular, GraphicsUnit.Point);
        using var labelBrush = new SolidBrush(accentMuted);
        g.DrawString("CLOUDFRAME", labelFont, labelBrush, new PointF(x, y));

        y += 13f;

        // ── FPS number + unit ─────────────────────────────────────────────────
        if (_settings.OverlayShowFps)
        {
            bool   hasFps = _snap.FramesPerSecond is double d && d > 0;
            string fpsNum = hasFps ? $"{_snap.FramesPerSecond!.Value:0}" : "--";
            bool   lowFps = hasFps && _snap.FramesPerSecond!.Value < 30;

            using var fpsFont  = new Font("Bahnschrift SemiBold", 27f, FontStyle.Regular, GraphicsUnit.Point);
            using var unitFont = new Font("Bahnschrift SemiBold",  9f, FontStyle.Regular, GraphicsUnit.Point);
            using var fpsBrush = new SolidBrush(lowFps ? WarnAmber : accent);
            var numSize = g.MeasureString(fpsNum, fpsFont);

            g.DrawString(fpsNum, fpsFont, fpsBrush, new PointF(x - 2f, y));
            using var unitBrush = new SolidBrush(accentMuted);
            g.DrawString("FPS", unitFont, unitBrush,
                new PointF(x + numSize.Width - 5f, y + numSize.Height - 15f));

            y += numSize.Height + 2f;
        }

        // ── CPU / GPU row ─────────────────────────────────────────────────────
        if (_settings.OverlayShowCpu || _settings.OverlayShowGpu)
        {
            using var dimFont  = new Font("Bahnschrift SemiBold", 7.5f, FontStyle.Regular, GraphicsUnit.Point);
            using var valFont  = new Font("Bahnschrift SemiBold", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
            using var dimBrush = new SolidBrush(accentMuted);
            using var valBrush = new SolidBrush(accent);

            float cx = x;
            if (_settings.OverlayShowCpu)
            {
                g.DrawString("CPU", dimFont, dimBrush, new PointF(cx, y + 1.5f));
                cx += g.MeasureString("CPU", dimFont).Width;
                var cpuStr = $"{_snap.CpuPercent:0}%";
                g.DrawString(cpuStr, valFont, valBrush, new PointF(cx - 1f, y));
                cx += g.MeasureString(cpuStr, valFont).Width + 8f;
            }
            if (_settings.OverlayShowGpu)
            {
                g.DrawString("GPU", dimFont, dimBrush, new PointF(cx, y + 1.5f));
                cx += g.MeasureString("GPU", dimFont).Width;
                var gpuStr = _snap.GpuPercent is double gpu ? $"{gpu:0}%" : "--%";
                g.DrawString(gpuStr, valFont, valBrush, new PointF(cx - 1f, y));
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static GraphicsPath RoundRect(RectangleF r, float rad)
    {
        var p = new GraphicsPath();
        p.AddArc(r.X,             r.Y,              rad * 2, rad * 2, 180, 90);
        p.AddArc(r.Right - rad*2, r.Y,              rad * 2, rad * 2, 270, 90);
        p.AddArc(r.Right - rad*2, r.Bottom - rad*2, rad * 2, rad * 2,   0, 90);
        p.AddArc(r.X,             r.Bottom - rad*2, rad * 2, rad * 2,  90, 90);
        p.CloseFigure();
        return p;
    }

    private Point ComputeLocation()
    {
        try
        {
            var screen = Screen.AllScreens
                .FirstOrDefault(s => string.Equals(s.DeviceName, _pinnedScreen, StringComparison.OrdinalIgnoreCase))
                ?? Screen.PrimaryScreen
                ?? Screen.FromPoint(Location);

            var wa = screen.WorkingArea;
            return _settings.OverlayPosition switch
            {
                OverlayPosition.TopRight    => new Point(wa.Right  - Width  - EdgeMargin, wa.Top    + EdgeMargin),
                OverlayPosition.BottomLeft  => new Point(wa.Left   + EdgeMargin,          wa.Bottom - Height - EdgeMargin),
                OverlayPosition.BottomRight => new Point(wa.Right  - Width  - EdgeMargin, wa.Bottom - Height - EdgeMargin),
                _                           => new Point(wa.Left   + EdgeMargin,          wa.Top    + EdgeMargin)  // TopLeft
            };
        }
        catch { return new Point(EdgeMargin, EdgeMargin); }
    }

    private void EnsurePinnedLocation()
    {
        var target = ComputeLocation();
        if (Location != target) Location = target;
    }
}
