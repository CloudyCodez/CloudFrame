using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace FrameBoost.Ui;

/// <summary>
/// Small animated arc gauge for CPU / GPU / FPS.
/// Renders a sweeping arc, a large value label, and a small caption.
/// Supports an optional unit string (e.g. "%" or "FPS").
/// The arc animates smoothly to the target value via a spring-damped timer.
/// </summary>
internal sealed class LiveStatRing : Control
{
    private float _targetValue;
    private float _displayValue;
    private string _unit = "%";
    private string _caption = string.Empty;
    private Color _accentColor = AppTheme.AccentStrong;
    private Color _lowColor = AppTheme.Success;
    private Color _highColor = AppTheme.Warning;
    private bool _useTrafficLight = true;  // lerp accent based on load
    private float _maxValue = 100f;
    private readonly System.Windows.Forms.Timer _animTimer = new() { Interval = 16 };

    // Hover tilt / spotlight
    private float _hoverAnim;
    private bool _isHovered;
    private PointF _glowPos = new(0.5f, 0.5f);
    private PointF _glowTarget = new(0.5f, 0.5f);

    public event EventHandler? Clicked;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float Value
    {
        get => _targetValue;
        set
        {
            var clamped = Math.Clamp(value, 0f, _maxValue);
            if (MathF.Abs(_targetValue - clamped) < 0.1f) return;
            _targetValue = clamped;
            _animTimer.Start();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float MaxValue
    {
        get => _maxValue;
        set { _maxValue = Math.Max(1f, value); Invalidate(); }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Unit
    {
        get => _unit;
        set { _unit = value ?? string.Empty; Invalidate(); }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Caption
    {
        get => _caption;
        set { _caption = value ?? string.Empty; Invalidate(); }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color AccentColor
    {
        get => _accentColor;
        set { _accentColor = value; Invalidate(); }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool UseTrafficLight
    {
        get => _useTrafficLight;
        set { _useTrafficLight = value; Invalidate(); }
    }

    public LiveStatRing()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor, true);

        Size = new Size(110, 110);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;

        _animTimer.Tick += (_, _) =>
        {
            float targetHover = _isHovered ? 1f : 0f;
            _hoverAnim += (targetHover - _hoverAnim) * 0.13f;
            _glowPos = new PointF(
                _glowPos.X + (_glowTarget.X - _glowPos.X) * 0.10f,
                _glowPos.Y + (_glowTarget.Y - _glowPos.Y) * 0.10f);

            float diff = _targetValue - _displayValue;
            _displayValue += diff * 0.12f;

            bool settled = MathF.Abs(diff) < 0.05f
                        && MathF.Abs(_hoverAnim - targetHover) < 0.003f;
            if (settled) { _displayValue = _targetValue; _animTimer.Stop(); }
            Invalidate();
        };
    }

    protected override void OnMouseEnter(EventArgs e)  { base.OnMouseEnter(e); _isHovered = true;  _animTimer.Start(); }
    protected override void OnMouseLeave(EventArgs e)  { base.OnMouseLeave(e); _isHovered = false; _glowTarget = new PointF(0.5f, 0.5f); _animTimer.Start(); }
    protected override void OnClick(EventArgs e)       { base.OnClick(e); Clicked?.Invoke(this, EventArgs.Empty); }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _glowTarget = new PointF(
            Math.Clamp((float)e.X / Math.Max(Width, 1), 0f, 1f),
            Math.Clamp((float)e.Y / Math.Max(Height, 1), 0f, 1f));
        if (!_animTimer.Enabled) _animTimer.Start();
    }

    protected override void OnPaintBackground(PaintEventArgs e) { }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode     = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // Resolve parent bg
        var bg = ResolveBackground();
        using var bgBrush = new SolidBrush(bg);
        g.FillRectangle(bgBrush, ClientRectangle);

        int sz    = Math.Min(Width, Height);
        if (sz < 10) return;  // too small to paint safely
        int pad   = (int)(sz * 0.08f + 2 * _hoverAnim);
        var ringRect = new Rectangle(pad, pad, sz - pad * 2, sz - pad * 2);
        const float StartAngle = 135f;
        const float SweepMax   = 270f;

        float frac = Math.Clamp(_displayValue / _maxValue, 0f, 1f);

        // Track
        using var trackPen = new Pen(Color.FromArgb(55, 70, 85), (int)(sz * 0.085f));
        trackPen.StartCap = LineCap.Round;
        trackPen.EndCap   = LineCap.Round;
        g.DrawArc(trackPen, ringRect, StartAngle, SweepMax);

        // Arc fill with gradient
        if (frac > 0.002f)
        {
            var arcColor = _useTrafficLight ? TrafficLightColor(frac) : _accentColor;
            float glowBoost = _hoverAnim * 0.15f;
            var arcColorBoosted = BrightenColor(arcColor, glowBoost);

            using var arcPen = new Pen(arcColorBoosted, (int)(sz * 0.085f));
            arcPen.StartCap = LineCap.Round;
            arcPen.EndCap   = LineCap.Round;
            g.DrawArc(arcPen, ringRect, StartAngle, SweepMax * frac);

            // Tip glow dot
            float tipAngle = (StartAngle + SweepMax * frac) * MathF.PI / 180f;
            float cx2 = ringRect.X + ringRect.Width  / 2f + (ringRect.Width  / 2f) * MathF.Cos(tipAngle);
            float cy2 = ringRect.Y + ringRect.Height / 2f + (ringRect.Height / 2f) * MathF.Sin(tipAngle);
            int dotR = (int)(sz * 0.065f);
            var dotRect = new RectangleF(cx2 - dotR, cy2 - dotR, dotR * 2, dotR * 2);
            using var dotBrush = new SolidBrush(Color.FromArgb((int)(200 + 55 * _hoverAnim), arcColorBoosted));
            g.FillEllipse(dotBrush, dotRect);
        }

        // Spotlight overlay
        if (_hoverAnim > 0.01f)
        {
            float gx = _glowPos.X * sz;
            float gy = _glowPos.Y * sz;
            float glowR = sz * 0.55f;
            var glowRect = new RectangleF(gx - glowR, gy - glowR, glowR * 2, glowR * 2);
            using var glowBrush = new PathGradientBrush(new PointF[]
            {
                new(gx - glowR, gy - glowR),
                new(gx + glowR, gy - glowR),
                new(gx + glowR, gy + glowR),
                new(gx - glowR, gy + glowR)
            })
            {
                CenterPoint    = new PointF(gx, gy),
                CenterColor    = Color.FromArgb((int)(30 * _hoverAnim), Color.White),
                SurroundColors = [Color.Transparent]
            };
            g.FillEllipse(glowBrush, glowRect);
        }

        // Value text (centre)
        float cx = sz / 2f;
        float cy = sz / 2f;
        string valStr = _maxValue <= 100f
            ? $"{_displayValue:0}"
            : $"{_displayValue:0}";
        using var valFont  = AppTheme.TitleFont(sz * 0.20f);
        using var valBrush = new SolidBrush(_useTrafficLight
            ? TrafficLightColor(frac)
            : _accentColor);
        var valSize = g.MeasureString(valStr, valFont);
        g.DrawString(valStr, valFont, valBrush,
            new PointF(cx - valSize.Width / 2f, cy - valSize.Height / 2f - sz * 0.04f));

        // Unit
        using var unitFont  = AppTheme.CaptionFont(sz * 0.085f);
        using var unitBrush = new SolidBrush(AppTheme.TextSecondary);
        var unitSize = g.MeasureString(_unit, unitFont);
        g.DrawString(_unit, unitFont, unitBrush,
            new PointF(cx - unitSize.Width / 2f, cy + valSize.Height / 2f - sz * 0.06f));

        // Caption below ring
        if (!string.IsNullOrWhiteSpace(_caption))
        {
            using var capFont  = AppTheme.CaptionFont(sz * 0.082f);
            using var capBrush = new SolidBrush(AppTheme.TextSecondary);
            var capSize = g.MeasureString(_caption, capFont);
            g.DrawString(_caption, capFont, capBrush,
                new PointF(cx - capSize.Width / 2f, sz - pad - capSize.Height));
        }
    }

    private Color TrafficLightColor(float frac)
    {
        // 0-60% green, 60-85% lerp to warning, 85%+ lerp to danger
        if (frac < 0.6f)  return LerpColor(_lowColor, _lowColor, 0f);
        if (frac < 0.85f) return LerpColor(_lowColor, _highColor, (frac - 0.6f) / 0.25f);
        return LerpColor(_highColor, AppTheme.Danger, (frac - 0.85f) / 0.15f);
    }

    private static Color BrightenColor(Color c, float amount)
    {
        return Color.FromArgb(
            Math.Min(255, (int)(c.R + 255 * amount)),
            Math.Min(255, (int)(c.G + 255 * amount)),
            Math.Min(255, (int)(c.B + 255 * amount)));
    }

    private static Color LerpColor(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return Color.FromArgb(
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));
    }

    private Color ResolveBackground()
    {
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
        if (disposing) _animTimer.Dispose();
        base.Dispose(disposing);
    }
}
