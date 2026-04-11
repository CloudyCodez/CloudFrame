using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace FrameBoost.Ui;

/// <summary>
/// Pill-shaped animated toggle chip. Replaces plain CheckBox in boost controls.
/// Tracks cursor for a subtle spotlight glow. Smooth fill + text colour animation.
/// </summary>
internal sealed class ToggleChip : Control
{
    private bool _checked;
    private float _anim;
    private float _hoverAnim;
    private bool _isHovered;
    private PointF _glowPos = new(0.5f, 0.5f);
    private PointF _glowTarget = new(0.5f, 0.5f);
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 16 };

    public event EventHandler? CheckedChanged;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            _timer.Start();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color AccentColor { get; set; } = AppTheme.AccentStrong;

    public ToggleChip()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor, true);

        Height = 32;
        Width = 120;
        AutoSize = false;
        Cursor = Cursors.Hand;
        Margin = new Padding(0, 0, 10, 8);
        Font = AppTheme.CaptionFont(9.5f);
        ForeColor = AppTheme.TextPrimary;
        BackColor = Color.Transparent;

        _timer.Tick += (_, _) =>
        {
            float targetAnim = _checked ? 1f : 0f;
            float targetHover = _isHovered ? 1f : 0f;
            _anim += (targetAnim - _anim) * 0.16f;
            _hoverAnim += (targetHover - _hoverAnim) * 0.14f;
            _glowPos = new PointF(
                _glowPos.X + (_glowTarget.X - _glowPos.X) * 0.12f,
                _glowPos.Y + (_glowTarget.Y - _glowPos.Y) * 0.12f);

            bool settled = MathF.Abs(_anim - targetAnim) < 0.003f
                        && MathF.Abs(_hoverAnim - targetHover) < 0.003f;
            if (settled) _timer.Stop();
            Invalidate();
        };
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        _timer.Start();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        _glowTarget = new PointF(0.5f, 0.5f);
        _timer.Start();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _glowTarget = new PointF(
            Math.Clamp((float)e.X / Math.Max(Width, 1), 0f, 1f),
            Math.Clamp((float)e.Y / Math.Max(Height, 1), 0f, 1f));
        if (!_timer.Enabled) _timer.Start();
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Checked = !_checked;
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        RecalcWidth();
        Invalidate();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        RecalcWidth();
    }

    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        RecalcWidth();
    }

    private void RecalcWidth()
    {
        var measureFont = Font ?? SystemFonts.DefaultFont;
        var proposedHeight = Math.Max(Height, 32);
        var sz = TextRenderer.MeasureText(
            Text ?? string.Empty,
            measureFont,
            new Size(int.MaxValue, proposedHeight),
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);

        Width = Math.Max(72, sz.Width + 40);
        Height = Math.Max(32, sz.Height + 10);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 4 || Height < 4) return;

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        using var chipFont = AppTheme.CaptionFont(9.5f);
        var textSize = TextRenderer.MeasureText(Text ?? string.Empty, chipFont,
            new Size(int.MaxValue, Height), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);

        const int PadH = 14, PadV = 5;
        int pillH = Math.Max(26, textSize.Height + PadV * 2);
        int pillW = Math.Max(textSize.Width + PadH * 2, Width);
        int pillY = (Height - pillH) / 2;
        var pillRect = new Rectangle(0, pillY, pillW - 1, pillH);
        using var pillPath = RoundRect(pillRect, pillH / 2);

        // Background
        var bgColor = LerpColor(Color.FromArgb(54, 59, 68), AccentColor, _anim);
        using var bgBrush = new SolidBrush(bgColor);
        g.FillPath(bgBrush, pillPath);

        // Spotlight glow
        if (_hoverAnim > 0.01f)
        {
            float gx = _glowPos.X * pillW;
            float gy = _glowPos.Y * pillH + pillY;
            using var glowPath = RoundRect(pillRect, pillH / 2);
            using var glowBrush = new PathGradientBrush(glowPath)
            {
                CenterPoint    = new PointF(gx, gy),
                CenterColor    = Color.FromArgb((int)(45 * _hoverAnim), Color.White),
                SurroundColors = [Color.Transparent]
            };
            g.FillPath(glowBrush, glowPath);
        }

        // Border
        var borderColor = LerpColor(AppTheme.Border, AccentColor, _anim);
        int borderAlpha = Math.Min(255, (int)(80 + 160 * _anim + 60 * _hoverAnim));
        using var borderPen = new Pen(Color.FromArgb(borderAlpha, borderColor), 1f);
        g.DrawPath(borderPen, pillPath);

        // Text
        var textColor = LerpColor(AppTheme.TextSecondary, Color.White, _anim * 0.85f + _hoverAnim * 0.15f);
        var textRect = new Rectangle(PadH, pillY, textSize.Width, pillH);
        TextRenderer.DrawText(g, Text, chipFont, textRect, textColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
    }

    private static GraphicsPath RoundRect(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Color LerpColor(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return Color.FromArgb(
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}
