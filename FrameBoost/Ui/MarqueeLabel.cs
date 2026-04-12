using System.ComponentModel;

namespace FrameBoost.Ui;

internal sealed class MarqueeLabel : Control
{
    // ── Scroll state ──────────────────────────────────────────────
    private float _offset;          // sub-pixel scroll position
    private float _speed;           // current scroll speed (px/tick)
    private float _targetSpeed;     // speed we are easing toward
    private int   _pauseTicks;      // ticks remaining in start/wrap pause
    private int   _textWidth;       // cached full text pixel width
    private bool  _scrollNeeded;    // true when text overflows the control
    private bool  _hovering;

    // ── Smoothing ─────────────────────────────────────────────────
    private const float BaseSpeed    = 0.85f;  // px per tick at full cruise
    private const float Acceleration = 0.12f;  // lerp factor toward target speed
    private const int   FadeWidth    = 28;      // px of edge fade on each side
    private const int   TextGap      = 48;      // px gap between looping copies
    private const int   StartPause   = 40;      // ticks before first scroll begins
    private const int   WrapPause    = 20;      // ticks after wrap-around

    private readonly System.Windows.Forms.Timer _timer;
    private readonly ToolTip _toolTip;

    public MarqueeLabel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint |
            ControlStyles.SupportsTransparentBackColor, true);

        AutoSize   = false;
        Height     = 20;
        BackColor  = Color.Transparent;
        ForeColor  = AppTheme.TextSecondary;
        Font       = AppTheme.BodyFont(9.5f);

        _timer = new System.Windows.Forms.Timer { Interval = 22 };  // ~45 fps
        _timer.Tick += (_, _) => Tick();

        _toolTip = new ToolTip { InitialDelay = 150, ReshowDelay = 80, AutoPopDelay = 10000, ShowAlways = true };

        MouseEnter += (_, _) => { _hovering = true;  UpdateToolTip(); };
        MouseLeave += (_, _) => { _hovering = false; _toolTip.Hide(this); };
        SizeChanged  += (_, _) => ResetScroll();
        FontChanged  += (_, _) => ResetScroll();
        TextChanged  += (_, _) => ResetScroll();
    }

    // ── Paint ─────────────────────────────────────────────────────
    protected override void OnPaint(PaintEventArgs e)
    {
        var g    = e.Graphics;
        var text = Text ?? string.Empty;
        var w    = ClientSize.Width;
        var h    = ClientSize.Height;

        // Opaque background matching parent
        using var bgBrush = new SolidBrush(ResolveBackgroundColor());
        g.FillRectangle(bgBrush, ClientRectangle);

        if (string.IsNullOrEmpty(text)) return;

        var drawFlags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
        var textH     = TextRenderer.MeasureText("Wg", Font, Size.Empty, drawFlags).Height;
        var drawY     = Math.Max(0, (h - textH) / 2);

        if (!_scrollNeeded)
        {
            // Static — just draw it left-aligned
            var staticFlags = drawFlags | TextFormatFlags.EndEllipsis;
            TextRenderer.DrawText(g, text, Font, new Rectangle(0, drawY, w, textH), ForeColor, staticFlags);
            return;
        }

        // Scroll: draw two copies of the text for seamless looping
        var x0 = -(int)_offset;
        var x1 = x0 + _textWidth + TextGap;
        TextRenderer.DrawText(g, text, Font, new Point(x0, drawY), ForeColor, drawFlags);
        if (x1 < w)
            TextRenderer.DrawText(g, text, Font, new Point(x1, drawY), ForeColor, drawFlags);

        // Fade edges using gradient rectangles
        ApplyEdgeFade(g, w, h);
    }

    private void ApplyEdgeFade(Graphics g, int w, int h)
    {
        var bg = ResolveBackgroundColor();

        // Left fade
        using var leftBlend = new System.Drawing.Drawing2D.LinearGradientBrush(
            new Rectangle(0, 0, FadeWidth, h),
            bg,
            Color.FromArgb(0, bg),
            System.Drawing.Drawing2D.LinearGradientMode.Horizontal);
        g.FillRectangle(leftBlend, 0, 0, FadeWidth, h);

        // Right fade
        using var rightBlend = new System.Drawing.Drawing2D.LinearGradientBrush(
            new Rectangle(w - FadeWidth, 0, FadeWidth, h),
            Color.FromArgb(0, bg),
            bg,
            System.Drawing.Drawing2D.LinearGradientMode.Horizontal);
        g.FillRectangle(rightBlend, w - FadeWidth, 0, FadeWidth, h);
    }

    // ── Scroll logic ──────────────────────────────────────────────
    private void Tick()
    {
        if (!_scrollNeeded) return;

        if (_pauseTicks > 0)
        {
            _pauseTicks--;
            // Ease speed up from zero during the pause release
            _targetSpeed = _pauseTicks == 0 ? BaseSpeed : 0f;
        }
        else
        {
            _targetSpeed = BaseSpeed;
        }

        // Smooth speed transition
        _speed += (_targetSpeed - _speed) * Acceleration;
        _offset += _speed;

        // Wrap seamlessly when first copy has fully scrolled out
        var loopAt = _textWidth + TextGap;
        if (_offset >= loopAt)
        {
            _offset -= loopAt;
            _pauseTicks = WrapPause;
        }

        Invalidate();
    }

    private void ResetScroll()
    {
        _textWidth = TextRenderer.MeasureText(
            Text ?? string.Empty, Font,
            new Size(int.MaxValue, Height),
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;

        _scrollNeeded = _textWidth > ClientSize.Width;
        _offset       = 0f;
        _speed        = 0f;
        _targetSpeed  = 0f;
        _pauseTicks   = StartPause;

        if (_scrollNeeded)
            _timer.Start();
        else
            _timer.Stop();

        UpdateToolTip();
        Invalidate();
    }

    private void UpdateToolTip()
    {
        var text = Text ?? string.Empty;
        _toolTip.SetToolTip(this, string.IsNullOrWhiteSpace(text) ? null : text);
        if (_hovering && !string.IsNullOrWhiteSpace(text))
            _toolTip.Show(text, this, Width / 2, Height + 8, 5000);
    }

    private Color ResolveBackgroundColor()
    {
        if (BackColor != Color.Transparent) return BackColor;
        var c = Parent;
        while (c is not null)
        {
            if (c.BackColor != Color.Transparent) return c.BackColor;
            c = c.Parent;
        }
        return AppTheme.Surface;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _timer.Dispose(); _toolTip.Dispose(); }
        base.Dispose(disposing);
    }
}
