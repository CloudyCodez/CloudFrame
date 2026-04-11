using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace FrameBoost.Ui;

internal class CardPanel : Panel
{
    private Point _mousePos = new(-1, -1);
    private bool _isHovered;
    private float _hoverGlow;
    private readonly System.Windows.Forms.Timer _glowTimer;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color FillColor { get; set; } = AppTheme.Surface;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BorderColor { get; set; } = AppTheme.Border;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CornerRadius { get; set; } = 18;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Padding InnerPadding { get; set; } = new(18);

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool EnableSpotlight { get; set; } = true;

    public CardPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        BackColor = AppTheme.Canvas;
        Padding = InnerPadding;

        _glowTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _glowTimer.Tick += (_, _) =>
        {
            var target = _isHovered ? 1f : 0f;
            var diff = target - _hoverGlow;
            if (Math.Abs(diff) < 0.01f)
            {
                _hoverGlow = target;
                if (!_isHovered) _glowTimer.Stop();
            }
            else
            {
                _hoverGlow += diff * 0.14f;
            }
            Invalidate();
        };
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        _glowTimer.Start();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        _mousePos = new Point(-1, -1);
        _glowTimer.Start();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _mousePos = e.Location;
        if (EnableSpotlight) Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 4 || Height < 4) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using var canvasBrush = new SolidBrush(ResolveBackgroundColor());
        g.FillRectangle(canvasBrush, ClientRectangle);

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = AppTheme.CreateRoundedRectangle(rect, CornerRadius);

        // Base fill
        using var fillBrush = new SolidBrush(FillColor);
        g.FillPath(fillBrush, path);

        // Spotlight glow following cursor
        if (EnableSpotlight && _hoverGlow > 0.01f && _mousePos.X >= 0)
        {
            var spotRadius = Math.Max(Width, Height) * 0.75f;
            var cx = _mousePos.X;
            var cy = _mousePos.Y;
            var spotBounds = new RectangleF(cx - spotRadius, cy - spotRadius, spotRadius * 2, spotRadius * 2);

            using var spotBrush = new PathGradientBrush(path)
            {
                CenterPoint = new PointF(cx, cy),
                CenterColor = Color.FromArgb((int)(28 * _hoverGlow), 13, 148, 136),
                SurroundColors = new[] { Color.FromArgb(0, 13, 148, 136) },
                FocusScales = new PointF(0f, 0f)
            };
            g.FillPath(spotBrush, path);
        }

        // Border — brightens slightly on hover
        var borderAlpha = (int)(255 * (0.55f + 0.45f * _hoverGlow));
        var borderColor = Color.FromArgb(borderAlpha, AppTheme.AccentStrong);
        using var borderPen = new Pen(BorderColor);
        if (_hoverGlow > 0.01f)
        {
            borderPen.Color = Color.FromArgb(
                (int)(AppTheme.Border.A + (AppTheme.AccentStrong.A - AppTheme.Border.A) * _hoverGlow * 0.6f),
                (int)(AppTheme.Border.R + (AppTheme.AccentStrong.R - AppTheme.Border.R) * _hoverGlow * 0.6f),
                (int)(AppTheme.Border.G + (AppTheme.AccentStrong.G - AppTheme.Border.G) * _hoverGlow * 0.6f),
                (int)(AppTheme.Border.B + (AppTheme.AccentStrong.B - AppTheme.Border.B) * _hoverGlow * 0.6f)
            );
        }
        g.DrawPath(borderPen, path);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _glowTimer.Dispose();
        base.Dispose(disposing);
    }

    private Color ResolveBackgroundColor()
    {
        var current = Parent;
        while (current is not null)
        {
            if (current.BackColor != Color.Transparent)
                return current.BackColor;
            current = current.Parent;
        }
        return AppTheme.Canvas;
    }
}
