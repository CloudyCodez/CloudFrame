using System.ComponentModel;
using System.Drawing.Drawing2D;
using FrameBoost.Models;

namespace FrameBoost.Ui;

/// <summary>
/// A self-contained boost-preset card with a mouse-tracking spotlight glow,
/// a GDI+ shear-matrix tilt effect, and a smooth animated hover transition.
/// </summary>
internal sealed class BoostPresetCard : Control
{
    // ── Animation ─────────────────────────────────────────────────────────────
    private readonly System.Windows.Forms.Timer _anim = new() { Interval = 16 };
    private float  _hoverProgress;
    private PointF _glowCurrent = new(0.5f, 0.5f);
    private PointF _glowTarget  = new(0.5f, 0.5f);
    private bool   _isHovered;

    // Shared constant — used in both OnPaint and DrawContent
    private const float CornerRadius = 16f;

    // ── Public properties (hidden from WinForms designer serialization) ────────
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public BoostPresetOption Preset { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? PresetTitle { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? Description { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int ImpactScore { get; set; } = 65;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsSelected { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color AccentColor { get; set; } = Color.FromArgb(13, 148, 136);

    public event EventHandler? CardSelected;

    // ── Constructor ───────────────────────────────────────────────────────────
    public BoostPresetCard()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint  |
            ControlStyles.UserPaint             |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw, true);

        Size   = new Size(220, 180);
        Cursor = Cursors.Hand;
        Margin = new Padding(0, 0, 12, 12);
        _anim.Tick += OnAnimTick;
    }

    // ── Animation tick ────────────────────────────────────────────────────────
    private void OnAnimTick(object? sender, EventArgs e)
    {
        float targetHover = _isHovered ? 1f : 0f;
        _hoverProgress += (targetHover - _hoverProgress) * 0.14f;
        _glowCurrent = new PointF(
            _glowCurrent.X + (_glowTarget.X - _glowCurrent.X) * 0.10f,
            _glowCurrent.Y + (_glowTarget.Y - _glowCurrent.Y) * 0.10f);

        bool settled = MathF.Abs(_hoverProgress - targetHover)   < 0.004f
                    && MathF.Abs(_glowCurrent.X  - _glowTarget.X) < 0.002f
                    && MathF.Abs(_glowCurrent.Y  - _glowTarget.Y) < 0.002f;
        if (settled) _anim.Stop();
        Invalidate();
    }

    // ── Mouse ─────────────────────────────────────────────────────────────────
    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        _anim.Start();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered  = false;
        _glowTarget = new PointF(0.5f, 0.5f);
        _anim.Start();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _glowTarget = new PointF(
            Math.Clamp((float)e.X / Math.Max(Width,  1), 0f, 1f),
            Math.Clamp((float)e.Y / Math.Max(Height, 1), 0f, 1f));
        if (!_anim.Enabled) _anim.Start();
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        CardSelected?.Invoke(this, EventArgs.Empty);
    }

    // ── Paint ─────────────────────────────────────────────────────────────────
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode     = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // Tilt shear from normalised cursor offset
        float mx = (_glowCurrent.X - 0.5f) * 2f * _hoverProgress;
        float my = (_glowCurrent.Y - 0.5f) * 2f * _hoverProgress;
        const float MaxShear       = 0.028f;
        const float MaxShadowShift = 6f;

        float pivotX = Width  / 2f;
        float pivotY = Height / 2f;
        var tiltMatrix = new Matrix();
        tiltMatrix.Translate(pivotX, pivotY);
        tiltMatrix.Shear(mx * MaxShear, my * MaxShear);
        tiltMatrix.Translate(-pivotX, -pivotY);

        // Card bounds
        const float EdgeInset = 2f;
        var cardRect = new RectangleF(
            EdgeInset, EdgeInset,
            Width  - EdgeInset * 2,
            Height - EdgeInset * 2);
        using var cardPath = RoundedRect(cardRect, CornerRadius);

        // Drop shadow (shifts opposite to tilt)
        DrawDropShadow(g, cardRect, CornerRadius,
            dx: -mx * MaxShadowShift,
            dy: -my * MaxShadowShift + 4f,
            intensity: _hoverProgress);

        // Apply tilt to card contents
        g.Transform = tiltMatrix;

        // Background
        var baseBg = IsSelected
            ? InterpolateColor(Color.FromArgb(38, 42, 50), AccentColor, 0.12f)
            : Color.FromArgb(38, 42, 50);
        using (var bgBrush = new SolidBrush(baseBg))
            g.FillPath(bgBrush, cardPath);

        // Spotlight glow that follows cursor
        if (_hoverProgress > 0.01f)
        {
            float glowX = _glowCurrent.X * Width;
            float glowY = _glowCurrent.Y * Height;
            using var glowPath = RoundedRect(cardRect, CornerRadius);
            using var glowBrush = new PathGradientBrush(glowPath)
            {
                CenterPoint    = new PointF(glowX, glowY),
                CenterColor    = Color.FromArgb((int)(55 * _hoverProgress), AccentColor),
                SurroundColors = [Color.Transparent]
            };
            g.FillPath(glowBrush, glowPath);
        }

        // Specular highlight at top edge (slides with tilt)
        if (_hoverProgress > 0.02f)
        {
            float specOffset = mx * 30f;
            var specRect = new RectangleF(
                cardRect.X + 20 + specOffset, cardRect.Y + 1,
                cardRect.Width - 40, 2);
            using var specBrush = new LinearGradientBrush(
                new PointF(specRect.Left,  specRect.Y),
                new PointF(specRect.Right, specRect.Y),
                Color.Transparent, Color.Transparent)
            {
                InterpolationColors = new ColorBlend
                {
                    Positions = [0f, 0.3f, 0.7f, 1f],
                    Colors    =
                    [
                        Color.Transparent,
                        Color.FromArgb((int)(160 * _hoverProgress), Color.White),
                        Color.FromArgb((int)(160 * _hoverProgress), Color.White),
                        Color.Transparent
                    ]
                }
            };
            g.FillRectangle(specBrush, specRect);
        }

        // Border
        int borderAlpha = IsSelected ? 220 : (int)(70 + 150 * _hoverProgress);
        using (var borderPen = new Pen(Color.FromArgb(borderAlpha, AccentColor), 1.5f))
            g.DrawPath(borderPen, cardPath);

        // Text content
        DrawContent(g, cardRect);

        g.ResetTransform();
    }

    private void DrawContent(Graphics g, RectangleF cardRect)
    {
        const int Pad = 18;
        float cx = cardRect.X + Pad;
        float cy = cardRect.Y + Pad;

        // Accent bar on left edge
        using var accentBarBrush = new LinearGradientBrush(
            new PointF(cardRect.X, cardRect.Y),
            new PointF(cardRect.X, cardRect.Bottom),
            Color.FromArgb(200, AccentColor),
            Color.FromArgb(40,  AccentColor))
        { WrapMode = WrapMode.TileFlipX };
        g.FillRectangle(accentBarBrush,
            cardRect.X + 1, cardRect.Y + CornerRadius,
            3, cardRect.Height - CornerRadius * 2);

        // Preset icon glyph
        string icon = Preset switch
        {
            BoostPresetOption.Balanced    => "⬡",
            BoostPresetOption.Performance => "⚡",
            BoostPresetOption.MaxFps      => "🔥",
            _                            => "◆"
        };
        using var iconFont  = AppTheme.TitleFont(22f);
        using var iconBrush = new SolidBrush(Color.FromArgb(220, AccentColor));
        g.DrawString(icon, iconFont, iconBrush, new PointF(cx + 2, cy - 2));

        // Title
        float titleY = cy + 30f;
        using var titleFont  = AppTheme.TitleFont(13.5f);
        using var titleBrush = new SolidBrush(Color.FromArgb(245, 248, 252));
        g.DrawString(PresetTitle ?? Preset.ToString(), titleFont, titleBrush, new PointF(cx, titleY));

        // Active badge (top-right corner)
        if (IsSelected)
        {
            using var selFont  = AppTheme.CaptionFont(9f);
            using var selBrush = new SolidBrush(AccentColor);
            const string SelStr = "● ACTIVE";
            var selSize = g.MeasureString(SelStr, selFont);
            g.DrawString(SelStr, selFont, selBrush,
                new PointF(cardRect.Right - selSize.Width - Pad, cardRect.Y + Pad));
        }

        // Description
        float descY = titleY + 22f;
        using var descFont  = AppTheme.BodyFont(8.8f);
        using var descBrush = new SolidBrush(Color.FromArgb(175, 185, 200));
        var descRect = new RectangleF(cx, descY, cardRect.Width - Pad * 2 - 4, 38f);
        g.DrawString(Description ?? string.Empty, descFont, descBrush, descRect);

        // Impact progress bar
        float barY  = cardRect.Bottom - 28f;
        float barW  = cardRect.Width - Pad * 2;
        float fillW = barW * (ImpactScore / 100f);
        float barX  = cx;
        const float BarH = 5f;

        using var trackBrush = new SolidBrush(Color.FromArgb(55, 65, 80));
        g.FillRectangle(trackBrush, barX, barY, barW, BarH);

        if (fillW > 0)
        {
            using var fillBrush = new LinearGradientBrush(
                new PointF(barX, barY), new PointF(barX + fillW, barY),
                Color.FromArgb(100, AccentColor), AccentColor);
            g.FillRectangle(fillBrush, barX, barY, fillW, BarH);
        }

        using var scoreFont  = AppTheme.CaptionFont(8.5f);
        using var scoreBrush = new SolidBrush(Color.FromArgb(140, AccentColor));
        g.DrawString($"{ImpactScore}% scope", scoreFont, scoreBrush, new PointF(barX, barY - 16f));
    }

    // ── Static helpers ────────────────────────────────────────────────────────
    private static void DrawDropShadow(Graphics g, RectangleF cardRect, float radius,
                                       float dx, float dy, float intensity)
    {
        const int Passes = 5;
        for (int i = Passes; i >= 1; i--)
        {
            float spread = i * 2.5f;
            float alpha  = (float)(Passes - i + 1) / Passes * 0.18f * intensity;
            var shadow = new RectangleF(
                cardRect.X + dx - spread * 0.5f,
                cardRect.Y + dy - spread * 0.5f,
                cardRect.Width  + spread,
                cardRect.Height + spread);
            using var shadowPath  = RoundedRect(shadow, radius + spread * 0.5f);
            using var shadowBrush = new SolidBrush(Color.FromArgb((int)(alpha * 255), Color.Black));
            g.FillPath(shadowBrush, shadowPath);
        }
    }

    private static Color InterpolateColor(Color a, Color b, float t)
        => Color.FromArgb(
            (int)(a.A + (b.A - a.A) * t),
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));

    private static GraphicsPath RoundedRect(RectangleF rect, float r)
    {
        var path = new GraphicsPath();
        path.AddArc(rect.X,             rect.Y,              r * 2, r * 2, 180, 90);
        path.AddArc(rect.Right - r * 2, rect.Y,              r * 2, r * 2, 270, 90);
        path.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2,   0, 90);
        path.AddArc(rect.X,             rect.Bottom - r * 2, r * 2, r * 2,  90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _anim.Dispose();
        base.Dispose(disposing);
    }
}
