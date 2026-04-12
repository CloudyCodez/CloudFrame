using System.Drawing.Drawing2D;

namespace FrameBoost.Ui;

internal sealed class DashboardBackdropPanel : Panel
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 40 };
    private float _phase;
    private bool _animationEnabled = true;

    public DashboardBackdropPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        BackColor = AppTheme.Canvas;
        _timer.Tick += (_, _) =>
        {
            _phase += 0.018f;
            if (_phase > MathF.PI * 2f)
            {
                _phase = 0f;
            }
            Invalidate();
        };
        UpdateAnimationState();
    }

    public void SetAnimationEnabled(bool enabled)
    {
        _animationEnabled = enabled;
        UpdateAnimationState();
        if (!enabled)
        {
            Invalidate();
        }
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        UpdateAnimationState();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new LinearGradientBrush(ClientRectangle, AppTheme.Canvas, Color.FromArgb(24, 32, 48), LinearGradientMode.Vertical);
        g.FillRectangle(brush, ClientRectangle);

        using var particleBrush = new SolidBrush(Color.FromArgb(18, AppTheme.AccentStrong));
        for (var i = 0; i < 24; i++)
        {
            var x = 24 + ((i * 83) % Math.Max(Width - 48, 48));
            var offset = (MathF.Sin(_phase + i * 0.52f) + 1f) * 18f;
            var y = 18 + ((i * 51) % Math.Max(Height - 36, 36)) + offset;
            var size = 2 + (i % 3);
            g.FillEllipse(particleBrush, x, y, size, size);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
        }
        base.Dispose(disposing);
    }

    private void UpdateAnimationState()
    {
        if (_animationEnabled && Visible)
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }
}
