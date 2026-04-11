using System.Drawing.Drawing2D;

namespace FrameBoost.Ui;

internal sealed class HeroPanel : Panel
{
    private float _shimmerX = -1f;
    private readonly System.Windows.Forms.Timer _shimmerTimer;

    public HeroPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        BackColor = AppTheme.Canvas;
        Padding = new Padding(22);
        Height = 148;
        Dock = DockStyle.Top;

        _shimmerTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _shimmerTimer.Tick += (_, _) =>
        {
            _shimmerX += 0.004f;
            if (_shimmerX > 1.4f) _shimmerX = -0.4f;
            Invalidate();
        };
        _shimmerTimer.Start();
    }

    protected override void OnPaintBackground(PaintEventArgs e) { }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using var canvasBrush = new SolidBrush(ResolveBackgroundColor());
        g.FillRectangle(canvasBrush, ClientRectangle);

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = AppTheme.CreateRoundedRectangle(rect, 22);

        // Base gradient
        using var brush = new LinearGradientBrush(
            rect,
            Color.FromArgb(16, 160, 148),
            Color.FromArgb(12, 108, 136),
            LinearGradientMode.ForwardDiagonal);
        g.FillPath(brush, path);

        // Shimmer sweep
        var shimmerCenterX = (int)(_shimmerX * Width);
        var shimmerW = Width / 3;
        var shimmerBounds = new Rectangle(shimmerCenterX - shimmerW / 2, 0, shimmerW, Height);
        using var shimmerBrush = new LinearGradientBrush(
            new Rectangle(shimmerBounds.X - 20, 0, shimmerBounds.Width + 40, Height),
            Color.FromArgb(0, 255, 255, 255),
            Color.FromArgb(0, 255, 255, 255),
            LinearGradientMode.Horizontal);
        var blend = new ColorBlend(3)
        {
            Colors = new[]
            {
                Color.FromArgb(0, 255, 255, 255),
                Color.FromArgb(18, 255, 255, 255),
                Color.FromArgb(0, 255, 255, 255)
            },
            Positions = new[] { 0f, 0.5f, 1f }
        };
        shimmerBrush.InterpolationColors = blend;
        g.FillPath(shimmerBrush, path);

        // Border
        using var borderPen = new Pen(Color.FromArgb(22, 185, 175), 1f);
        g.DrawPath(borderPen, path);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _shimmerTimer.Dispose();
        base.Dispose(disposing);
    }

    private Color ResolveBackgroundColor()
    {
        var current = Parent;
        while (current is not null)
        {
            if (current.BackColor != Color.Transparent) return current.BackColor;
            current = current.Parent;
        }
        return AppTheme.Canvas;
    }
}
